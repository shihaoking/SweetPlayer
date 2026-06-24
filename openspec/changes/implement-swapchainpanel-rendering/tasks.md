## 1. 添加依赖和 P/Invoke 声明

- [x] 1.1 在 SweetPlayer.csproj 中添加 SharpDX.Direct3D11 和 SharpDX.DXGI NuGet 包
- [x] 1.2 在 MpvInterop.cs 中添加 mpv_render_param 结构体定义
- [x] 1.3 在 MpvInterop.cs 中添加 mpv_render_context_create P/Invoke 声明
- [x] 1.4 在 MpvInterop.cs 中添加 mpv_render_context_render P/Invoke 声明
- [x] 1.5 在 MpvInterop.cs 中添加 mpv_render_context_set_update_callback P/Invoke 声明
- [x] 1.6 在 MpvInterop.cs 中添加 mpv_render_context_free P/Invoke 声明
- [x] 1.7 定义 ISwapChainPanelNative COM 接口声明

## 2. 实现 D3D11 设备创建

- [x] 2.1 在 MpvPlayerService.cs 中添加 D3D11 Device 和 SwapChain 字段
- [x] 2.2 在 InitializeRenderer 方法中创建 SharpDX.Direct3D11.Device（使用 DriverType.Hardware）
- [x] 2.3 添加设备创建失败时的错误处理和日志记录
- [x] 2.4 创建 DXGI SwapChain 并配置为 BGRA 格式
- [x] 2.5 通过 ISwapChainPanelNative 将 SwapChain 关联到 SwapChainPanel

## 3. 配置 mpv_render_context

- [x] 3.1 构建 mpv_render_param 数组，设置 API_TYPE 为 "d3d11"
- [x] 3.2 将 D3D11 设备的 NativePointer 传递给 mpv_render_param
- [x] 3.3 调用 mpv_render_context_create 创建渲染上下文
- [x] 3.4 添加渲染上下文创建失败时的错误处理
- [x] 3.5 将 _renderContext 字段保存为创建的上下文句柄

## 4. 实现渲染循环

- [x] 4.1 定义渲染更新回调委托和同步机制（ManualResetEventSlim）
- [x] 4.2 调用 mpv_render_context_set_update_callback 注册更新回调
- [x] 4.3 在 Task.Run 中启动后台渲染循环线程
- [x] 4.4 在渲染循环中等待更新回调信号
- [x] 4.5 收到信号后调用 mpv_render_context_render 渲染到 D3D11 纹理
- [x] 4.6 调用 swapChain.Present(1, 0) 呈现帧到 SwapChainPanel
- [x] 4.7 添加渲染失败时的错误日志记录
- [x] 4.8 实现渲染循环终止标志检查机制

## 5. 实现 Resize 处理

- [x] 5.1 在 Resize 方法中验证 width 和 height 参数有效性
- [x] 5.2 使用线程安全机制（锁或队列）通知渲染线程尺寸变化
- [x] 5.3 在渲染线程中调用 swapChain.ResizeBuffers 调整缓冲区大小
- [x] 5.4 添加 Resize 失败时的错误处理和日志记录

## 6. 实现资源清理

- [x] 6.1 在 Dispose 方法中设置渲染线程终止标志
- [x] 6.2 等待渲染线程完全退出
- [x] 6.3 调用 mpv_render_context_free 释放渲染上下文
- [x] 6.4 释放 SwapChain 资源
- [x] 6.5 释放 D3D11 Device 资源
- [x] 6.6 确保 _renderContext 和设备字段设置为 null/IntPtr.Zero

## 7. 更新 MpvPlayerControl 集成

- [x] 7.1 确认 MpvPlayerControl.xaml 包含 SwapChainPanel 控件
- [x] 7.2 在 MpvPlayerControl 的 Loaded 事件中获取 SwapChainPanel 句柄
- [x] 7.3 调用 MpvPlayerService.InitializeRenderer 并传递 SwapChainPanel 参数
- [x] 7.4 订阅 SizeChanged 事件并调用 MpvPlayerService.Resize

## 8. 错误处理和日志

- [x] 8.1 添加设备丢失（DXGI_ERROR_DEVICE_REMOVED）检测和日志
- [x] 8.2 添加驱动重置（DXGI_ERROR_DEVICE_RESET）检测和日志
- [x] 8.3 在所有关键渲染操作中添加 try-catch 和详细日志

## 9. 测试和验证

- [x] 9.1 运行应用并点击播放视频，验证正确导航到 PlayerPage
- [x] 9.2 验证视频画面在 SwapChainPanel 中正确渲染
- [x] 9.3 测试窗口尺寸调整，验证画面自适应
- [x] 9.4 测试全屏切换功能
- [x] 9.5 测试快进、快退、暂停等播放控制功能
- [x] 9.6 验证退出播放时资源正确释放
- [x] 9.7 测试在 libmpv 不可用时的降级行为

## 10. sw 渲染路径代替方案

- [x] 10.1 使用 `MPV_RENDER_API_TYPE_SW` 创建渲染上下文（代替 d3d11）
- [x] 10.2 分配 CPU 帧缓冲区（`byte[]`）接收 mpv BGR0 帧输出
- [x] 10.3 使用 `UpdateSubresource` 将 CPU 帧上传到 SwapChain 后缓冲区、避开 `MapSubresource` 的 `E_INVALIDARG`

## 11. D3D11 线程亲和性修复

- [x] 11.1 创建专用 `Thread`（`MpvRenderThread`）运行 `RenderThreadEntry`
- [x] 11.2 在该线程上创建 D3D11 Device、SwapChain、`mpv_render_context`、运行渲染循环
- [x] 11.3 D3D11 Device 的 `BgraSupport | VideoSupport` 标志设置
- [x] 11.4 在事件循环与渲染线程之间用事件信号（`ManualResetEventSlim`）同步帧生成

## 12. UI 线程同步与资源关联

- [x] 12.1 在 `InitializeRenderer` 中捕获 `SynchronizationContext.Current`
- [x] 12.2 通过 `SynchronizationContext.Post` 分派 `ISwapChainPanelNative.SetSwapChain` 到 UI 线程，超时 1s
- [x] 12.3 通过 `SynchronizationContext.Post` 分派 `IDXGISwapChain.Present` 到 UI 线程，超时 200ms
- [x] 12.4 SwapChain 尺寸使用 `SwapChainPanel.ActualWidth/ActualHeight` 逻辑像素，不乘以 DPI

## 13. 渲染器就绪同步原语（解决间歇性无画面）

- [x] 13.1 `IMpvPlayerService` 增加 `IsRendererReady` 属性
- [x] 13.2 `IMpvPlayerService` 增加 `Task WaitForRendererReadyAsync(TimeSpan timeout)`
- [x] 13.3 `MpvPlayerService` 内部以 `TaskCompletionSource<bool>` 维护就绪信号
- [x] 13.4 `mpv_render_context_create` 成功后 `_rendererReadyTcs.TrySetResult(true)`
- [x] 13.5 抽出 `RendererReadyWaiter.WaitAsync` 到非 unsafe 辅助类（unsafe 上下文不允许 await）
- [x] 13.6 `PlaybackControlService.PlayVideoAsync` 在 `LoadFileAsync` 前 `await WaitForRendererReadyAsync(10s)`
- [x] 13.7 `MovieDetailViewModel` / `SeriesDetailViewModel` 调整为先 `NavigateTo(PlayerPage)` 再 `PlayVideoAsync`

## 14. mpv 诊断与可观测性

- [x] 14.1 `mpv_initialize` 后调用 `mpv_request_log_messages("info")`
- [x] 14.2 事件循环处理 `MPV_EVENT_LOG_MESSAGE`（id=2）并按 mpv level 转发到 ILogger
- [x] 14.3 事件循环记录关键事件详情：START_FILE/FILE_LOADED/PLAYBACK_RESTART/VIDEO_RECONFIG

## 15. 进度条交互问题修复（额外任务）

- [x] 15.1 诊断 Slider 事件不触发问题（PointerPressed/Released、ManipulationStarted/Completed）
- [x] 15.2 实现基于 ValueChanged 值变化特征的智能拖动检测（阈值 > 5.0 秒）
- [x] 15.3 添加双重状态跟踪机制（本地 `_isDragging` + ViewModel `IsUserSeeking`）
- [x] 15.4 在 Slider 和父 Grid 上绑定 Pointer 事件，使用 `e.Handled = true` 阻止冒泡
- [x] 15.5 实现 `FindParent<T>` 辅助方法，沿 Visual Tree 查找父控件
- [x] 15.6 添加分层调试日志（UI 层、ViewModel 层、Service 层）
- [x] 15.7 验证拖动进度条快进/快退功能正常工作
- [x] 15.8 验证点击进度条跳转功能正常工作
- [x] 15.9 编写实施总结文档 `PROGRESS_BAR_FIX_SUMMARY.md`
