## Why

SweetPlayer 当前使用 LibMpv.Client + SharpDX.Direct3D11 手动管理 D3D11 渲染管线，通过 `mpv_render_context` + SwapChainPanel 实现视频输出。该方案存在 GPU→CPU→GPU 三次拷贝的性能瓶颈，且代码复杂度高（~990 行 MpvPlayerService，含大量 unsafe 代码）。mpv-kernel（`Richasy.MpvKernel.*`）提供了成熟的分层封装，其 `wid` 独立窗口模式让 mpv 直接 GPU 零拷贝渲染，可显著降低 CPU 占用并消除所有手动渲染逻辑。

## What Changes

- **BREAKING** 移除 LibMpv.Client、SharpDX.Direct3D11、SharpDX.DXGI 依赖，替换为 Richasy.MpvKernel.Interop / Core / WinUI NuGet 包
- **BREAKING** 完全重写 MpvPlayerService（~990 行→~250 行），移除所有 D3D11/SwapChain/RenderLoop/unsafe 代码
- **BREAKING** 移除 IMpvPlayerService 中的渲染器相关方法（InitializeRenderer / IsRendererReady / WaitForRendererReadyAsync / Resize / DisposeRenderer），新增 `InitializeAsync(IntPtr windowHandle)` 和 `SetDecodeOptionsAsync`
- **BREAKING** 播放 UI 从 PlayerPage 内嵌迁移到独立 AppWindow（通过 MpvPlayerWindow 或自定义窗口），PlayerPage 简化为窗口启动桥接页
- 新增 PlayerWindow 独立播放窗口管理类（AppWindow + DesktopWindowXamlSource）
- 新增 PlayerWindowOverlay 覆盖层（从 PlayerPage.xaml 完整迁移顶部栏、底部控制栏、设置面板、OSD 通知、Up Next 等全部 UI）
- 保留所有现有播放配置功能：字幕轨道/字号/颜色/延迟、音轨切换、音量(含增强)、播放速度、画面比例、章节导航
- 保留所有键盘快捷键映射不变
- 删除 4 个不再需要的文件：ISwapChainPanelNative.cs、MpvInterop.cs、MpvPlayerControl.xaml/.cs
- `d3d11-rendering` 和 `swapchain-panel-integration` 两个 spec 不再适用，标记为废弃

## Capabilities

### New Capabilities
- `mpv-kernel-engine`: 基于 mpv-kernel 的播放引擎替换，涵盖 NuGet 依赖切换、IMpvPlayerService 接口重写、MpvPlayerService 完全重写、PlaybackControlService 适配、DI 注册和 mpv DLL 初始化
- `player-window-integration`: 独立播放窗口架构，涵盖 PlayerWindow 窗口管理（AppWindow 生命周期、全屏/小窗/置顶）、PlayerPage 桥接页简化、键盘快捷键目标迁移

### Modified Capabilities
- `video-playback`: 播放 UI 从 PlayerPage 内嵌迁移到独立窗口覆盖层（PlayerWindowOverlay），布局和交互逻辑完整保留但宿主变更；控件自动隐藏/显示行为保留

## Impact

- **依赖**: 移除 LibMpv.Client + SharpDX (3 个包)，新增 Richasy.MpvKernel.Interop + Core + WinUI (3 个包)
- **代码**: MpvPlayerService.cs 完全重写；PlayerPage.xaml/.cs 大幅简化；新增 ~3 个文件（PlayerWindow.cs、PlayerWindowOverlay.xaml/.cs）
- **删除文件**: ISwapChainPanelNative.cs、MpvInterop.cs、MpvPlayerControl.xaml、MpvPlayerControl.xaml.cs
- **接口变更**: IMpvPlayerService 移除渲染方法，新增 InitializeAsync(IntPtr)
- **DI 注册**: IMpvPlayerService 和 IPlaybackControlService 从 Singleton 改为 Transient（每次播放创建新实例，窗口关闭时销毁），新增 PlayerWindow Transient 注册；App 启动时调用 MpvNative.Initialize
- **性能预期**: 4K 视频播放 CPU 占用降低 30-50%（消除三次拷贝）
- **TargetFramework**: SweetPlayer.Services 需升级为 net8.0-windows10.0.22621.0
