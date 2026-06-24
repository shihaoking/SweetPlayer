## ADDED Requirements

### Requirement: D3D11 设备创建
系统 SHALL 在 InitializeRenderer 方法中创建 D3D11 设备，使用硬件加速驱动类型。

#### Scenario: 成功创建 D3D11 设备
- **WHEN** InitializeRenderer 被调用
- **THEN** 系统创建 SharpDX.Direct3D11.Device 实例，使用 DriverType.Hardware

#### Scenario: 设备创建失败时记录错误
- **WHEN** D3D11 设备创建失败（如驱动不支持）
- **THEN** 系统记录错误日志并抛出异常

### Requirement: mpv_render_context 配置
系统 SHALL 配置 mpv_render_context 使用 D3D11 API 类型，并传递 D3D11 设备的原生指针。

#### Scenario: 正确配置渲染上下文参数
- **WHEN** 创建 mpv_render_context
- **THEN** 系统传递 MPV_RENDER_PARAM_API_TYPE 为 "d3d11"
- **THEN** 系统传递 MPV_RENDER_PARAM_D3D11_DEVICE 指向 D3D11 设备的原生指针

#### Scenario: 渲染上下文创建失败
- **WHEN** mpv_render_context_create 返回非零错误码
- **THEN** 系统记录错误并释放已创建的资源

### Requirement: 渲染循环实现
系统 SHALL 实现渲染循环，监听 mpv_render_context_update 回调，并在有新帧时调用 mpv_render_context_render。

#### Scenario: 接收更新通知时触发渲染
- **WHEN** mpv_render_context_update 回调被触发
- **THEN** 系统唤醒渲染线程
- **THEN** 系统调用 mpv_render_context_render 渲染新帧

#### Scenario: 渲染循环在独立线程运行
- **WHEN** InitializeRenderer 完成
- **THEN** 系统在后台线程中启动渲染循环
- **THEN** 渲染循环不阻塞 UI 线程

#### Scenario: 停止渲染时终止循环
- **WHEN** Dispose 方法被调用
- **THEN** 系统设置终止标志
- **THEN** 渲染线程检测到标志后退出循环

### Requirement: D3D11 纹理渲染
系统 SHALL 使用 mpv_render_context_render 将视频帧渲染到 D3D11 纹理。

#### Scenario: 渲染帧到 D3D11 纹理
- **WHEN** 调用 mpv_render_context_render
- **THEN** 系统传递 D3D11 纹理作为渲染目标
- **THEN** mpv 将解码后的视频帧写入纹理

#### Scenario: 渲染失败时记录日志
- **WHEN** mpv_render_context_render 返回错误
- **THEN** 系统记录错误日志并跳过当前帧

### Requirement: 资源清理
系统 SHALL 在 Dispose 时正确释放 D3D11 设备和 mpv_render_context。

#### Scenario: 释放渲染上下文
- **WHEN** Dispose 被调用
- **THEN** 系统调用 mpv_render_context_free 释放渲染上下文
- **THEN** 系统将 _renderContext 设置为 IntPtr.Zero

#### Scenario: 释放 D3D11 设备
- **WHEN** Dispose 被调用
- **THEN** 系统调用 D3D11 设备的 Dispose 方法
- **THEN** 系统释放所有关联的 D3D11 资源

### Requirement: 采用 sw 渲染路径代替 D3D11 API以避开集成险坑
系统 SHALL 使用 `MPV_RENDER_API_TYPE_SW` 创建 mpv 渲染上下文，让 mpv 输出 BGR0 像素到应用提供的 CPU 内存，再以 `UpdateSubresource` 上传到 SwapChain 后缓冲区。

#### Scenario: 使用 sw API 创建渲染上下文
- **WHEN** 调用 `mpv_render_context_create`
- **THEN** 传递 `MPV_RENDER_PARAM_API_TYPE` = `"sw"`
- **THEN** 不传递 `MPV_RENDER_PARAM_D3D11_DEVICE`

#### Scenario: CPU 帧缓冲区接收 mpv 输出
- **WHEN** mpv 准备输出帧
- **THEN** 应用传递 `MPV_RENDER_PARAM_SW_SIZE`、`MPV_RENDER_PARAM_SW_FORMAT`(`bgr0`)、`MPV_RENDER_PARAM_SW_STRIDE`、`MPV_RENDER_PARAM_SW_POINTER`
- **THEN** mpv 将解码后的帧写入应用 `byte[]` 缓冲区

#### Scenario: UpdateSubresource 上传到后缓冲区避开 MapSubresource
- **WHEN** mpv 完成一帧输出
- **THEN** 系统获取 SwapChain BackBuffer 并调用 `DeviceContext.UpdateSubresource(backBuffer, 0, null, framePtr, stride, 0)`
- **THEN** 系统 NEVER 调用 `MapSubresource`（在未启用 MULTITHREADED 设备的环境下会返回 E_INVALIDARG）

### Requirement: D3D11 ImmediateContext 线程亲和性
系统 SHALL 在专用渲染线程上创建 D3D11 设备并在同一线程调用所有 ImmediateContext 操作。

#### Scenario: 创建专用渲染线程
- **WHEN** `InitializeRenderer` 被调用
- **THEN** 系统启动名为 `MpvRenderThread` 的专用 `Thread`（非线程池 Task）
- **THEN** D3D11 Device、SwapChain、`mpv_render_context`、渲染循环都在该线程上创建/运行

#### Scenario: 避免跨线程 ImmediateContext 调用
- **WHEN** 任意 `MapSubresource` / `UpdateSubresource` / `CopyResource` 调用发生
- **THEN** 调用线程必须是创建 D3D11 设备的同一线程

### Requirement: D3D11 设备创建标志
系统 SHALL 在创建 D3D11 设备时同时启用 `BgraSupport` 与 `VideoSupport`。

#### Scenario: 设备创建标志
- **WHEN** `new Device(DriverType.Hardware, ...)` 被调用
- **THEN** flags = `DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport`
- **THEN** Debug 构建可加 `DeviceCreationFlags.Debug`，不可用时回退到普通标志

### Requirement: 渲染器就绪同步原语
系统 SHALL 提供异步机制让上层能够等待 mpv 渲染上下文创建完成。

#### Scenario: 提供就绪查询与等待 API
- **WHEN** 调用方需要同步渲染器状态
- **THEN** `IMpvPlayerService.IsRendererReady` 返回 `mpv_render_context` 是否创建成功
- **THEN** `IMpvPlayerService.WaitForRendererReadyAsync(TimeSpan timeout)` 返回可 await 的 Task，在超时或就绪时完成

#### Scenario: 渲染上下文创建成功后信号释放
- **WHEN** `mpv_render_context_create` 返回 0
- **THEN** 系统的 `TaskCompletionSource<bool>` 调用 `TrySetResult(true)`
- **THEN** 所有等待 `WaitForRendererReadyAsync` 的调用方被唤醒

#### Scenario: 避免 unsafe 上下文中使用 await
- **WHEN** `MpvPlayerService` 是 unsafe 类
- **THEN** `WaitForRendererReadyAsync` 仅返回从辅助类（`RendererReadyWaiter`）获取的 Task
- **THEN** 该辅助类为非 unsafe，允许内部使用 await

