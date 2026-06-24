## Why

当前播放器需要实现基于 SwapChainPanel 的硬件加速渲染，以提升视频播放性能和画质。通过 D3D11 API 与 mpv 渲染上下文集成，可以实现更高效的 GPU 渲染，并在 PlayerPage.xaml 中提供流畅的播放体验。

## What Changes

- 在 MpvPlayerService.cs 的 InitializeRenderer 方法中创建 D3D11 设备
- 配置 mpv_render_context 使用 D3D11 渲染 API
- 实现渲染循环，监听 mpv_render_context_update 回调
- 调用 mpv_render_context_render 更新帧并将纹理呈现到 SwapChainPanel
- 确保点击播放视频时正确加载和使用 PlayerPage.xaml

### 实际落地中额外增加的关键变更（见 design.md Implementation Notes）

- 采用 `MPV_RENDER_API_TYPE_SW` 代替 D3D11 API（避开 LibMpv.Client + libmpv-2.dll 集成险坑）
- D3D11 设备创建与所有 ImmediateContext 调用集中在专用 `MpvRenderThread` 上进行（避开跨线程 `MapSubresource` 返回 E_INVALIDARG）
- SwapChainPanel 关联与 Present 必须通过 `SynchronizationContext.Post` 分派回 UI 线程
- SwapChain 使用逻辑像素尺寸，不乘以 DPI
- **增加渲染器就绪同步原语**：`IMpvPlayerService.IsRendererReady` / `WaitForRendererReadyAsync(timeout)`，以 `TaskCompletionSource<bool>` 实现，避免 `loadfile` 在 `mpv_render_context_create` 之前执行导致的 `vo/libmpv: No render context set` 间歇性黑屏
- **业务层调整**：`MovieDetailViewModel` / `SeriesDetailViewModel` 先 `NavigateTo(PlayerPage)` 再 `PlayVideoAsync`；`PlaybackControlService.PlayVideoAsync` 在 `LoadFileAsync` 前 `await WaitForRendererReadyAsync(10s)`
- **mpv 诊断能力**：启用 `mpv_request_log_messages("info")`，事件循环转发 `MPV_EVENT_LOG_MESSAGE` 到 ILogger
- **进度条交互修复**：解决 WinUI 3 Slider 控件 Pointer/Manipulation 事件不触发的问题，采用基于 ValueChanged 值变化特征的智能拖动检测（阈值 > 5.0 秒），实现可靠的拖拽和点击跳转功能
- **播放进度恢复与渲染同步修复**：
  - 修复 seek 命令时序问题，添加 800ms 延迟确保 mpv 完成文件加载后再执行 seek
  - 修复 IsUserSeeking 标志未重置导致进度条冻结问题，在 OnPositionChanged 中自动重置
  - 修复 DisposeRenderer 未重置渲染事件和计数器导致多次播放无画面问题
  - 调整进度恢复阈值从 5秒 改为 3秒，提升用户体验
- **用户可配置进度恢复**：添加 IUserSettingsService 和设置页面 UI，用户可通过开关控制是否自动恢复播放进度（默认启用，阈值 3秒）

## Capabilities

### New Capabilities
- `d3d11-rendering`: D3D11 硬件加速渲染能力，包括设备创建、渲染上下文配置和帧渲染
- `swapchain-panel-integration`: SwapChainPanel 与 mpv 渲染器的集成，实现纹理呈现和播放控制

### Modified Capabilities
<!-- 无现有能力需要修改 -->

## Impact

- **MpvPlayerService.cs**: 需要实现 InitializeRenderer 方法，添加 D3D11 设备创建和 mpv 渲染上下文配置
- **PlayerPage.xaml / PlayerPage.xaml.cs**: 需要确保 SwapChainPanel 控件正确配置并与渲染服务集成
- **依赖项**: 需要添加 SharpDX.Direct3D11 和相关 SharpDX 包
- **Native Interop**: 可能需要添加或更新 mpv_render_context 相关的 P/Invoke 声明
