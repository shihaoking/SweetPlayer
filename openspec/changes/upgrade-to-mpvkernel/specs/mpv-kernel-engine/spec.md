## ADDED Requirements

### Requirement: NuGet 依赖替换
系统 SHALL 移除 LibMpv.Client、SharpDX.Direct3D11、SharpDX.DXGI 包依赖，替换为 Richasy.MpvKernel.Interop、Richasy.MpvKernel.Core、Richasy.MpvKernel.WinUI 包。

#### Scenario: SweetPlayer.Services 包依赖替换
- **WHEN** 编辑 SweetPlayer.Services.csproj
- **THEN** 移除 `LibMpv.Client`、`SharpDX.Direct3D11`、`SharpDX.DXGI` PackageReference
- **THEN** 添加 `Richasy.MpvKernel.Interop` 和 `Richasy.MpvKernel.Core` PackageReference

#### Scenario: SweetPlayer 主项目包依赖替换
- **WHEN** 编辑 SweetPlayer.csproj
- **THEN** 添加 `Richasy.MpvKernel.WinUI` PackageReference

#### Scenario: SweetPlayer.Services TargetFramework 升级
- **WHEN** 编辑 SweetPlayer.Services.csproj
- **THEN** TargetFramework 从 `net8.0` 升级为 `net8.0-windows10.0.22621.0`

### Requirement: IMpvPlayerService 接口重写
系统 SHALL 重写 IMpvPlayerService 接口，移除所有渲染器相关方法，新增基于 mpv-kernel 的初始化和配置方法。

#### Scenario: 移除渲染器相关方法
- **WHEN** 重写 IMpvPlayerService 接口
- **THEN** 移除 `InitializeRenderer(IntPtr, int, int)` 方法
- **THEN** 移除 `IsRendererReady` 属性
- **THEN** 移除 `WaitForRendererReadyAsync(TimeSpan)` 方法
- **THEN** 移除 `Resize(int, int)` 方法
- **THEN** 移除 `DisposeRenderer()` 方法

#### Scenario: 新增 mpv-kernel 初始化方法
- **WHEN** 重写 IMpvPlayerService 接口
- **THEN** 新增 `Task InitializeAsync(IntPtr windowHandle)` 方法，接收 HWND 用于 wid 模式
- **THEN** 新增 `Task SetDecodeOptionsAsync(DecodeMode mode)` 方法，配置硬件/软件解码

#### Scenario: 保留所有播放控制和配置方法
- **WHEN** 重写 IMpvPlayerService 接口
- **THEN** 保留 `Play()` / `Pause()` / `TogglePlayPause()` / `Stop()`
- **THEN** 保留 `Seek(TimeSpan)` / `SeekRelative(double)`
- **THEN** 保留 `LoadFileAsync(string)` / 所有 Volume / Speed 属性
- **THEN** 保留 `SetSubtitleTrack(int)` / `LoadExternalSubtitle(string)` / `SetAudioTrack(int)` / `SetAudioBoost(double)` / `SetAspectRatio(string)`
- **THEN** 保留 `PositionChanged` / `StateChanged` / `FileEnded` 事件

### Requirement: MpvPlayerService 完全重写
系统 SHALL 使用 mpv-kernel 的 MpvClient 和 MpvNative 完全重写 MpvPlayerService，从约 990 行简化到约 250 行。

#### Scenario: MpvClient 初始化
- **WHEN** `InitializeAsync(IntPtr windowHandle)` 被调用
- **THEN** 系统通过 `MpvClient.CreateAsync()` 创建客户端实例
- **THEN** 系统调用 `UseIdleAsync(true)` 启用空闲模式
- **THEN** 系统调用 `UseKeepOpenAsync(true)` 保持播放完成后最后一帧显示
- **THEN** 系统调用 `SetVideoOutputAsync(VideoOutputType.Gpu)` 配置 GPU 输出
- **THEN** 系统调用 `SetGpuContextAsync(GpuContextType.D3D11)` 配置 D3D11 后端
- **THEN** 系统调用 `SetHardwareDecodeAsync(HardwareDecodeType.Auto)` 启用硬件解码
- **THEN** 系统订阅 `DataNotify` 事件接收状态变化通知
- **THEN** 系统保存 windowHandle 供后续 PlayAsync 使用

#### Scenario: 播放文件
- **WHEN** `LoadFileAsync(string filePath)` 被调用
- **THEN** 系统构建 `MpvPlayOptions`，设置 WindowHandle 为保存的 HWND
- **THEN** 系统调用 `_client.PlayAsync(filePath, options)` 加载文件

#### Scenario: 通过 MpvNative 设置播放配置
- **WHEN** 调用字幕/音轨/画面比例等配置方法
- **THEN** `SetSubtitleTrack(int)` 通过 `MpvNative.SetPropertyString(_client.Handle, "sid", value)` 实现
- **THEN** `SetAudioTrack(int)` 通过 `MpvNative.SetPropertyString(_client.Handle, "aid", value)` 实现
- **THEN** `SetAspectRatio(string)` 通过 `MpvNative.SetPropertyString(_client.Handle, "video-aspect-override", value)` 实现
- **THEN** `SetAudioBoost(double)` 通过 `MpvNative.SetPropertyString(_client.Handle, "volume-max", value)` 实现
- **THEN** `LoadExternalSubtitle(string)` 通过 `MpvNative.SetCommandString(_client.Handle, $"sub-add \"{path}\"")` 实现

#### Scenario: 事件通知转发
- **WHEN** MpvClient.DataNotify 事件触发
- **THEN** PositionChanged 事件从 `MpvClientEventId.PositionChanged` 数据转发
- **THEN** StateChanged 事件从 `MpvClientEventId.StateChanged` 数据转发
- **THEN** VolumeChanged 事件从 `MpvClientEventId.VolumeChanged` 数据转发
- **THEN** FileEnded 事件从 MpvClient.ReachFileEnd 事件转发

#### Scenario: 资源清理
- **WHEN** MpvPlayerService 被释放（IAsyncDisposable）
- **THEN** 系统取消事件订阅
- **THEN** 系统调用 `_client.DisposeAsync()` 释放 MpvClient

### Requirement: PlaybackControlService 适配
系统 SHALL 适配 PlaybackControlService 以匹配新的 IMpvPlayerService 接口。

#### Scenario: 移除渲染器等待逻辑
- **WHEN** 适配 PlaybackControlService
- **THEN** 移除 `await _mpv.WaitForRendererReadyAsync()` 调用
- **THEN** 移除 `_mpv.DisposeRenderer()` 调用
- **THEN** 移除 `_mpv.Resize(...)` 调用

#### Scenario: 保留播放控制逻辑
- **WHEN** 适配 PlaybackControlService
- **THEN** 保留进度持久化逻辑
- **THEN** 保留 HDR 管理逻辑
- **THEN** 保留事件转发（PositionChanged / StateChanged / FileEnded）

### Requirement: DI 注册和 mpv 初始化
系统 SHALL 更新依赖注入注册和应用启动初始化。

#### Scenario: DI 容器注册
- **WHEN** 配置 DI 容器
- **THEN** 保留 `services.AddSingleton<IMpvPlayerService, MpvPlayerService>()` 注册
- **THEN** 新增 `services.AddTransient<PlayerWindow>()` 注册独立播放窗口

#### Scenario: mpv DLL 路径初始化
- **WHEN** 应用启动
- **THEN** 调用 `MpvNative.Initialize("libmpv-2.dll")` 初始化 DLL 加载路径

### Requirement: 删除不再需要的文件
系统 SHALL 删除与旧渲染管线相关的所有文件。

#### Scenario: 删除 SwapChain 相关文件
- **WHEN** 清理旧代码
- **THEN** 删除 `src/SweetPlayer.Services/Playback/ISwapChainPanelNative.cs`
- **THEN** 删除 `src/SweetPlayer.Services/Playback/MpvInterop.cs`
- **THEN** 删除 `src/SweetPlayer/Controls/MpvPlayerControl.xaml`
- **THEN** 删除 `src/SweetPlayer/Controls/MpvPlayerControl.xaml.cs`
