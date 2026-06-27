## Context

SweetPlayer 的播放窗口使用 `AppWindow` + `DesktopWindowXamlSource` 架构：mpv 通过 `wid` 模式直接 GPU 零拷贝渲染到窗口 HWND，XAML 覆盖层（PlayerWindowOverlay）悬浮在视频内容之上提供播放控制 UI。

窗口默认带有 Windows 标题栏（约 32px 高），这导致：
- 外部尺寸 1920×1080 → 客户区实际为 1920×1048（比例 ≈ 1.832，宽于 16:9 的 1.778）
- mpv `keepaspect=yes` 会在客户区内以视频原始比例渲染，对 16:9 视频产生左右 pillarbox

**约束条件**：
- `MpvKernel.Core` 和 `MpvKernel.Interop` 两个项目代码禁止修改
- mpv 默认 `keepaspect=yes` 必须保留（确保视频不变形）
- 覆盖层 TopBar 已具备完整的窗口控制能力（返回、设置、全屏切换）

## Goals / Non-Goals

**Goals:**
- 消除窗口模式下因标题栏占位导致的视频左右黑边
- 视频标题作为悬浮层显示在视频内容之上
- 悬浮标题与底部播放控件同步显示/隐藏（3 秒无操作自动隐藏）
- 保持窗口可调整大小（边框拖拽）

**Non-Goals:**
- 不实现自定义标题栏拖拽移动窗口功能（用户通过 Alt+拖拽或任务栏操作窗口）
- 不修改视频渲染逻辑（mpv keepaspect 行为不变）
- 不修改覆盖层的 TopBar XAML 布局（已满足需求）

## Decisions

### 1. 使用 OverlappedPresenter.SetBorderAndTitleBar 隐藏标题栏

**理由**：WinAppSDK 提供的 `OverlappedPresenter` 允许精确控制窗口边框和标题栏的可见性，是最轻量的方案。

**替代方案**：
- `AppWindow.TitleBar.ExtendsContentIntoTitleBar`：需要额外处理拖拽区域和按钮占位，复杂度高
- `FullScreen` presenter：会占满整个屏幕，不符合窗口模式需求
- 动态约束窗口宽高比：初始化时视频比例未知、用户拖拽体验差

**选择当前方案的原因**：一行代码解决问题，无副作用，保留窗口边框支持 resize。

### 2. 保留窗口边框（hasBorder: true）

**理由**：用户仍需通过拖拽边缘调整窗口大小。无边框窗口会丧失系统级 resize 交互。

### 3. 复用现有覆盖层 TopBar 作为悬浮标题

**理由**：`PlayerWindowOverlay.xaml` 中的 TopBar 已包含：
- 返回按钮（Esc 快捷键）
- 视频标题（NOW PLAYING + 标题文本）
- 设置按钮
- 渐变背景（从 80% 黑到透明）
- 与 BottomBar 同步的 Opacity 动画

无需新增代码，隐藏标题栏后 TopBar 自然承担标题功能。

## Design

### 架构图

```
┌─────────────────────────────────────────────────┐
│ AppWindow (hasBorder=true, hasTitleBar=false)    │
│ ┌─────────────────────────────────────────────┐ │
│ │          Client Area = Full Window          │ │
│ │ ┌─────────────────────────────────────────┐ │ │
│ │ │     mpv wid 渲染（GPU 零拷贝）         │ │ │
│ │ │     keepaspect=yes，16:9 无黑边         │ │ │
│ │ └─────────────────────────────────────────┘ │ │
│ │ ┌─────────────────────────────────────────┐ │ │
│ │ │  DesktopWindowXamlSource (透明覆盖层)   │ │ │
│ │ │  ┌─ TopBar (悬浮, 渐隐) ─────────────┐ │ │ │
│ │ │  │ [←]   NOW PLAYING / 标题   [⚙]   │ │ │ │
│ │ │  └───────────────────────────────────┘ │ │ │
│ │ │            [▶ 中央播放按钮]              │ │ │
│ │ │  ┌─ BottomBar (悬浮, 渐隐) ──────────┐ │ │ │
│ │ │  │ 时间 ─── 进度条 ─── 总时长        │ │ │ │
│ │ │  │ [⏮][⏯][⏭]  速度  字幕 音轨 音量  │ │ │ │
│ │ │  └───────────────────────────────────┘ │ │ │
│ │ └─────────────────────────────────────────┘ │ │
│ └─────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

### 关键代码

```csharp
// PlayerWindow.cs - ShowAsync()
_appWindow = AppWindow.Create();
_appWindow.Title = "SweetPlayer";

// 隐藏 Windows 标题栏，让视频铺满整个窗口区域
if (_appWindow.Presenter is OverlappedPresenter presenter)
{
    presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
}

_appWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
// 此时 ClientSize = Size = 1920×1080（无标题栏占位）
```

### 显示/隐藏同步（已有实现）

```csharp
// PlayerWindowOverlay.xaml.cs
private void ShowControlsImmediate()
{
    if (_controlsVisible) return;
    _controlsVisible = true;
    AnimateOpacity(TopBar, 1.0, 200);      // TopBar 同步淡入
    AnimateOpacity(BottomBar, 1.0, 200);
    AnimateOpacity(CenterPlayButton, 1.0, 200);
}

private void HideControls()
{
    if (!_controlsVisible) return;
    _controlsVisible = false;
    AnimateOpacity(TopBar, 0.0, 300);      // TopBar 同步淡出
    AnimateOpacity(BottomBar, 0.0, 300);
    AnimateOpacity(CenterPlayButton, 0.0, 300);
}
```
