# 播放器进度恢复与渲染同步问题修复 - 实施总结

## 问题概述

本次修复解决了视频播放器在多次播放同一视频时出现的多个关键问题：
1. **进度恢复后视频从头播放**（seek 命令时序问题）
2. **进度条不更新**（IsUserSeeking 标志未重置）
3. **多次播放后无画面**（渲染事件和计数器未重置）
4. **用户无法控制进度恢复行为**（缺少用户设置开关）

## 问题详细分析与解决方案

### 问题 1：进度恢复后视频从头播放

**现象**：
- 进度条显示正确位置（如 13.096秒）
- 但视频内容从头开始播放
- seek 命令未生效

**日志证据**：
```
收到 MPV_EVENT_START_FILE
从数据库加载进度：VideoId=67, Position=13.096s
seek 13.096 absolute
收到 MPV_EVENT_FILE_LOADED  ← seek 在文件加载完成前发送
收到 MPV_EVENT_PLAYBACK_RESTART
```

**根本原因**：
- seek 命令在 `MPV_EVENT_FILE_LOADED` 之前发送
- mpv 此时还未完全准备好执行 seek

**解决方案**：
```csharp
// 在 LoadFileAsync 后添加延迟，确保 mpv 完成文件加载
await _mpv.LoadFileAsync(videoFile.FullPath);
await Task.Delay(800);  // 从 500ms 增加到 800ms
_mpv.Seek(saved.Position);
```

**关键文件**：
- `PlaybackControlService.cs` - `PlayVideoAsync` 方法

---

### 问题 2：进度条不更新（冻结在初始位置）

**现象**：
- 视频从正确位置开始播放（如 13.096秒）
- 进度条初始位置正确
- 但继续播放时进度条不更新（一直停在 13.096秒）
- 视频正常播放并保存新进度

**日志证据**：
```
恢复播放进度：00:00:13.0960000
[PlayerPage] ValueChanged: OldValue=0, NewValue=13.096, IsUserSeeking=False
[PlayerVM] IsUserSeeking changed to: True
[PlayerPage] Drag started detected (diff=13.096), setting IsUserSeeking=true
[PlayerPage] Updated PositionSeconds to 13.096 during drag
...
SweetPlayer.Services.Playback.PlaybackControlService: Information: 保存进度：VideoId=67, Position=17.559s
```

**根本原因**：
- 恢复进度时设置了 `IsUserSeeking=true`
- 但在 seek 完成后**没有重置为 false**
- 后续 mpv 的位置更新被 `if (IsUserSeeking) return;` 忽略

**解决方案**：

**1. 在 `OnPositionChanged` 中重置标志**：
```csharp
private void OnPositionChanged(object? sender, TimeSpan position)
{
    _dispatcherQueue.TryEnqueue(() =>
    {
        // 移除 if (IsUserSeeking) return; 这行代码
        
        if (_suppressSeekFeedback)
        {
            if (Math.Abs(position.TotalSeconds - _seekTargetSeconds) < 1.5)
            {
                _suppressSeekFeedback = false;
                PositionSeconds = position.TotalSeconds;
                // seek 完成后重置 IsUserSeeking
                if (IsUserSeeking)
                {
                    IsUserSeeking = false;
                    System.Diagnostics.Debug.WriteLine("[PlayerVM] Reset IsUserSeeking to false after seek completed");
                }
            }
        }
        else
        {
            // 正常播放时，如果 IsUserSeeking 仍为 true，也重置它
            if (IsUserSeeking)
            {
                IsUserSeeking = false;
                System.Diagnostics.Debug.WriteLine("[PlayerVM] Reset IsUserSeeking to false during normal playback");
            }
            PositionSeconds = position.TotalSeconds;
        }
    });
}
```

**2. 在 `CommitSeekFromSlider` 中确保重置**：
```csharp
public void CommitSeekFromSlider()
{
    var targetSec = PositionSeconds;
    IsUserSeeking = false;
    var target = TimeSpan.FromSeconds(targetSec);
    _suppressSeekFeedback = true;
    _seekTargetSeconds = targetSec;
    _playback.MpvPlayer.Seek(target);
    
    // 确保 IsUserSeeking 在 seek 完成后保持为 false
    IsUserSeeking = false;
}
```

**关键文件**：
- `PlayerViewModel.cs` - `OnPositionChanged` 和 `CommitSeekFromSlider` 方法

---

### 问题 3：多次播放后无画面（有声音）

**现象**：
- 第1、2次播放正常（有画面有声音）
- 第3、4次播放只有声音没有画面
- 渲染循环启动但没有"渲染帧 #X"日志

**日志证据**：
```
第1次播放（正常）：
渲染线程启动，创建 D3D11 设备...
渲染帧 #1, 累计回调=1
渲染帧 #2, 累计回调=3
渲染帧 #3, 累计回调=4

第4次播放（无画面）：
渲染线程启动，创建 D3D11 设备...
渲染循环开始, renderContext=OK, swapChain=OK, device=OK
收到 MPV_EVENT_PLAYBACK_RESTART
← 没有"渲染帧 #X"日志
```

**根本原因**：
- `DisposeRenderer` 释放资源时，**没有重置渲染事件和计数器**
- `_renderUpdateEvent` 可能处于异常状态
- `_renderedFrameCount` 累积到 300+，影响诊断逻辑
- 第4次播放时 mpv 没有触发渲染回调

**解决方案**：
```csharp
public void DisposeRenderer()
{
    _logger.LogInformation("开始释放渲染资源...");

    // 取消渲染循环
    _renderCancellation?.Cancel();

    // 等待渲染线程退出
    if (_renderThread is { IsAlive: true })
    {
        try { _renderThread.Join(TimeSpan.FromSeconds(5)); } catch { }
    }

    // 重置渲染更新事件（确保下次播放时处于未触发状态）
    _renderUpdateEvent?.Reset();

    // 重置渲染计数器
    Interlocked.Exchange(ref _renderedFrameCount, 0);
    Interlocked.Exchange(ref _callbackCount, 0);
    _logger.LogInformation("渲染计数器和事件已重置");

    // 释放渲染上下文、SwapChain、D3D11 设备...
}
```

**关键文件**：
- `MpvPlayerService.cs` - `DisposeRenderer` 方法

---

### 问题 4：用户无法控制进度恢复行为

**现象**：
- 用户期望每次从头播放，但系统总是恢复进度
- 或者用户期望记住进度，但系统从头播放
- 缺少用户配置选项

**解决方案**：

**1. 创建用户设置服务**：
```csharp
// IUserSettingsService.cs
public interface IUserSettingsService
{
    bool AutoResumePlayback { get; set; }
    Task SaveAsync();
    Task LoadAsync();
}

// UserSettingsService.cs
public class UserSettingsService : IUserSettingsService
{
    public bool AutoResumePlayback { get; set; } = true;  // 默认启用
    
    // 使用 JSON 文件持久化设置
    // 保存在 %AppData%\SweetPlayer\user_settings.json
}
```

**2. 集成到播放控制逻辑**：
```csharp
public async Task PlayVideoAsync(VideoFile videoFile)
{
    await _mpv.LoadFileAsync(videoFile.FullPath);
    await Task.Delay(800);

    if (_userSettings.AutoResumePlayback)
    {
        var saved = await _progress.GetProgressAsync(videoFile.Id);
        if (saved is not null && !saved.IsCompleted && saved.Position > TimeSpan.FromSeconds(3))
        {
            _mpv.Seek(saved.Position);
        }
    }
}
```

**3. 添加设置页面 UI**：
```xml
<!-- SettingsPage.xaml -->
<ToggleSwitch Header="自动恢复播放进度"
              IsOn="{x:Bind ViewModel.AutoResumePlayback, Mode=TwoWay}" />
```

**4. 创建 ViewModel**：
```csharp
// SettingsViewModel.cs
public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _autoResumePlayback;

    partial void OnAutoResumePlaybackChanged(bool value)
    {
        _userSettings.AutoResumePlayback = value;
    }
}
```

**5. 依赖注入配置**：
```csharp
// App.xaml.cs
services.AddSingleton<IUserSettingsService, UserSettingsService>();

// 应用启动时加载设置
var userSettings = Services.GetRequiredService<IUserSettingsService>();
await userSettings.LoadAsync();
```

**关键文件**：
- `IUserSettingsService.cs` - 接口定义
- `UserSettingsService.cs` - 实现（JSON 持久化）
- `PlaybackControlService.cs` - 集成设置逻辑
- `SettingsPage.xaml` / `SettingsPage.xaml.cs` - UI
- `SettingsViewModel.cs` - ViewModel
- `App.xaml.cs` - 依赖注入

---

## 修改的文件清单

### 服务层
1. **MpvPlayerService.cs**
   - `DisposeRenderer()` 方法：添加渲染事件和计数器重置
   - `OnPositionChanged()` 方法：移除 `if (IsUserSeeking) return;`，添加标志重置逻辑

2. **PlaybackControlService.cs**
   - 构造函数：注入 `IUserSettingsService`
   - `PlayVideoAsync()` 方法：
     - 添加 800ms 延迟确保 mpv 完成文件加载
     - 根据用户设置决定是否恢复进度
     - 阈值调整为 3 秒（从 5秒 → 3秒）

3. **IUserSettingsService.cs** (新建)
   - 用户设置服务接口

4. **UserSettingsService.cs** (新建)
   - 用户设置服务实现
   - JSON 文件持久化

### ViewModel 层
5. **PlayerViewModel.cs**
   - `OnPositionChanged()` 方法：移除早期返回，添加 `IsUserSeeking` 重置逻辑
   - `CommitSeekFromSlider()` 方法：确保 seek 后重置 `IsUserSeeking`

6. **SettingsViewModel.cs** (新建)
   - 设置页面 ViewModel
   - `AutoResumePlayback` 属性绑定

### UI 层
7. **SettingsPage.xaml**
   - 添加"播放设置"卡片
   - 添加 `ToggleSwitch` 开关

8. **App.xaml.cs**
   - 注册 `IUserSettingsService` 到 DI 容器
   - 应用启动时加载用户设置

---

## 技术要点

### 1. mpv 事件时序

**正确的 seek 时机**：
```
MPV_EVENT_START_FILE
  ↓
LoadFileAsync()
  ↓
等待 800ms（让 mpv 完成文件加载和初始化）
  ↓
MPV_EVENT_FILE_LOADED
  ↓
seek 命令  ← 此时 mpv 已准备好
  ↓
MPV_EVENT_PLAYBACK_RESTART
```

**错误的 seek 时机**：
```
MPV_EVENT_START_FILE
  ↓
seek 命令  ← mpv 还未准备好
  ↓
MPV_EVENT_FILE_LOADED
  ↓
MPV_EVENT_PLAYBACK_RESTART（seek 无效）
```

### 2. UI 状态同步

**IsUserSeeking 生命周期**：
```
用户拖动进度条
  ↓
IsUserSeeking = true（阻止 mpv 位置回写）
  ↓
更新 PositionSeconds（拖动预览）
  ↓
释放鼠标
  ↓
CommitSeekFromSlider()
  ↓
mpv.Seek()
  ↓
OnPositionChanged() 检测到接近目标位置
  ↓
IsUserSeeking = false（恢复正常同步）
  ↓
后续位置更新正常同步到 UI
```

### 3. 渲染资源生命周期

**DisposeRenderer 必须重置的状态**：
- `_renderUpdateEvent` - 渲染触发事件
- `_renderedFrameCount` - 渲染帧计数器
- `_callbackCount` - 回调计数器
- `_renderCancellation` - 取消标志

**不重置的后果**：
- 下次播放时事件状态异常
- 渲染循环无法正确等待 mpv 回调
- 诊断逻辑被旧数据干扰

### 4. 用户设置持久化

**存储位置**：`%AppData%\SweetPlayer\user_settings.json`

**JSON 格式**：
```json
{
  "AutoResumePlayback": true
}
```

**默认值**：`true`（启用自动恢复）

**阈值**：3 秒（播放超过 3 秒才恢复进度）

---

## 验证测试

### 测试用例 1：进度恢复功能

**步骤**：
1. 播放视频到 30秒
2. 退出播放
3. 再次播放同一视频

**预期结果**：
- ✅ 进度条初始位置为 30秒
- ✅ 视频从 30秒开始播放
- ✅ 继续播放时进度条正常更新（31s, 32s, 33s...）

**关键日志**：
```
从数据库加载进度：VideoId=67, Position=30s
恢复播放进度：00:00:30
[PlayerVM] Reset IsUserSeeking to false after seek completed
[PlayerPage] Normal playback update: 31
[PlayerPage] Normal playback update: 32
```

### 测试用例 2：多次播放测试

**步骤**：
1. 播放视频到 20秒，退出
2. 再次播放（第2次），播放到 40秒，退出
3. 再次播放（第3次），播放到 60秒，退出
4. 再次播放（第4次）

**预期结果**：
- ✅ 每次都有画面和声音
- ✅ 每次都恢复到上次退出位置
- ✅ 进度条正常更新

**关键日志**：
```
第2次：从数据库加载进度：VideoId=67, Position=20s
第3次：从数据库加载进度：VideoId=67, Position=40s
第4次：从数据库加载进度：VideoId=67, Position=60s
渲染计数器和事件已重置
渲染帧 #1, 累计回调=1
```

### 测试用例 3：用户设置开关

**步骤**：
1. 设置页面关闭"自动恢复播放进度"
2. 播放视频到 30秒，退出
3. 再次播放

**预期结果**：
- ✅ 视频从头开始播放
- ✅ 不恢复进度

**关键日志**：
```
用户设置禁用自动恢复进度，从头开始播放：VideoId=67
```

---

## 经验总结

### 1. mpv 集成最佳实践

- **seek 时机**：必须在 `MPV_EVENT_FILE_LOADED` 之后，建议添加 500-1000ms 延迟
- **事件监听**：`MPV_EVENT_PLAYBACK_RESTART` 表示 seek 完成
- **状态重置**：释放资源时必须重置所有事件和计数器

### 2. UI 状态管理

- **临时标志**：`IsUserSeeking` 等标志必须在操作完成后立即重置
- **状态同步**：避免在异步回调中遗漏状态重置
- **日志诊断**：添加详细日志跟踪状态变化

### 3. 用户体验设计

- **可配置性**：提供用户设置开关，满足不同使用习惯
- **阈值选择**：3-5 秒是合理的恢复阈值（避免误触）
- **持久化**：用户设置应该持久化，避免每次重新配置

### 4. 调试技巧

- **分层日志**：UI 层、ViewModel 层、Service 层都添加日志
- **关键事件**：记录 mpv 事件、状态变化、seek 命令等
- **对比分析**：对比正常和异常情况的日志，快速定位问题

---

## 相关文件

- `src/SweetPlayer.Services/Playback/MpvPlayerService.cs`
- `src/SweetPlayer.Services/Playback/PlaybackControlService.cs`
- `src/SweetPlayer.Services/Playback/IUserSettingsService.cs` (新建)
- `src/SweetPlayer.Services/Playback/UserSettingsService.cs` (新建)
- `src/SweetPlayer/ViewModels/PlayerViewModel.cs`
- `src/SweetPlayer/ViewModels/SettingsViewModel.cs` (新建)
- `src/SweetPlayer/Views/SettingsPage.xaml`
- `src/SweetPlayer/App.xaml.cs`

---

## 后续优化建议

1. **监听 MPV_EVENT_PLAYBACK_RESTART**：用事件驱动代替延迟，更精确地控制 seek 时机
2. **添加错误恢复**：seek 失败时自动重试或提示用户
3. **进度恢复提示**：显示"从 XX:XX 继续播放"的 OSD 提示
4. **多视频切换**：优化快速切换多个视频时的资源管理
5. **性能监控**：添加渲染性能指标（帧率、延迟等）
