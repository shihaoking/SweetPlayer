## Why

当前播放器需要实现基于 SwapChainPanel 的硬件加速渲染，以提升视频播放性能和画质。通过 D3D11 API 与 mpv 渲染上下文集成，可以实现更高效的 GPU 渲染，并在 PlayerPage.xaml 中提供流畅的播放体验。

## What Changes

- 在 MpvPlayerService.cs 的 InitializeRenderer 方法中创建 D3D11 设备
- 配置 mpv_render_context 使用 D3D11 渲染 API
- 实现渲染循环，监听 mpv_render_context_update 回调
- 调用 mpv_render_context_render 更新帧并将纹理呈现到 SwapChainPanel
- 确保点击播放视频时正确加载和使用 PlayerPage.xaml

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
