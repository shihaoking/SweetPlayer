## ADDED Requirements

### Requirement: SwapChainPanel 集成
系统 SHALL 通过 ISwapChainPanelNative 接口将 D3D11 SwapChain 关联到 SwapChainPanel 控件。

#### Scenario: 获取 SwapChainPanel 原生接口
- **WHEN** InitializeRenderer 接收到 SwapChainPanel 句柄
- **THEN** 系统通过 COM 接口获取 ISwapChainPanelNative
- **THEN** 系统成功获取原生指针用于 SwapChain 关联

#### Scenario: 创建并关联 SwapChain
- **WHEN** D3D11 设备创建成功
- **THEN** 系统创建 DXGI SwapChain
- **THEN** 系统通过 ISwapChainPanelNative.SetSwapChain 将 SwapChain 关联到 SwapChainPanel

#### Scenario: SwapChain 配置匹配面板尺寸
- **WHEN** 创建 SwapChain
- **THEN** 系统使用传入的 width 和 height 参数配置 SwapChain 缓冲区大小
- **THEN** SwapChain 格式设置为 BGRA 格式以匹配 WinUI 要求

### Requirement: 纹理呈现到 SwapChain
系统 SHALL 在每次渲染完成后将 D3D11 纹理内容呈现到 SwapChainPanel。

#### Scenario: 渲染帧呈现到屏幕
- **WHEN** mpv_render_context_render 完成
- **THEN** 系统调用 swapChain.Present() 将帧呈现到 SwapChainPanel
- **THEN** 用户在 PlayerPage 中看到更新的视频画面

#### Scenario: 使用垂直同步控制帧率
- **WHEN** 调用 swapChain.Present()
- **THEN** 系统使用同步间隔 1（启用 VSync）以避免画面撕裂

### Requirement: 渲染区域调整
系统 SHALL 在 Resize 方法被调用时调整 SwapChain 缓冲区大小。

#### Scenario: 处理窗口尺寸变化
- **WHEN** Resize 方法接收到新的 width 和 height
- **THEN** 系统调用 swapChain.ResizeBuffers 调整缓冲区大小
- **THEN** 视频画面适应新的显示区域

#### Scenario: 全屏切换时调整尺寸
- **WHEN** 用户切换全屏模式
- **THEN** 系统接收 Resize 事件
- **THEN** SwapChain 缓冲区调整为全屏分辨率

#### Scenario: Resize 失败时记录错误
- **WHEN** swapChain.ResizeBuffers 抛出异常
- **THEN** 系统记录错误日志
- **THEN** 系统保持当前缓冲区大小继续渲染

### Requirement: PlayerPage 播放集成
系统 SHALL 确保点击播放视频时正确导航到 PlayerPage.xaml 并初始化渲染器。

#### Scenario: 导航到 PlayerPage 时初始化渲染
- **WHEN** 应用导航到 PlayerPage
- **THEN** PlayerPage 加载 MpvPlayerControl 控件
- **THEN** MpvPlayerControl 调用 MpvPlayerService.InitializeRenderer 并传递 SwapChainPanel 句柄

#### Scenario: 播放视频时显示画面
- **WHEN** 用户点击播放按钮
- **THEN** 系统加载视频文件
- **THEN** 视频帧渲染到 PlayerPage 的 SwapChainPanel 中
- **THEN** 用户看到流畅的视频播放画面

### Requirement: 线程安全
系统 SHALL 确保渲染线程和 UI 线程之间的交互是线程安全的。

#### Scenario: Resize 事件线程安全处理
- **WHEN** UI 线程调用 Resize 方法
- **THEN** 系统使用线程安全机制（如锁或队列）通知渲染线程
- **THEN** 渲染线程在安全时机执行 SwapChain 调整

#### Scenario: Dispose 时终止渲染线程
- **WHEN** UI 线程调用 Dispose
- **THEN** 系统设置终止标志
- **THEN** 渲染线程检测标志后安全退出
- **THEN** UI 线程等待渲染线程完成后释放资源

### Requirement: 错误处理
系统 SHALL 处理设备丢失等异常情况并记录详细日志。

#### Scenario: 检测设备丢失错误
- **WHEN** swapChain.Present() 抛出 DXGI_ERROR_DEVICE_REMOVED
- **THEN** 系统记录设备丢失日志
- **THEN** 系统停止渲染循环

#### Scenario: 驱动重置时记录错误
- **WHEN** D3D11 设备返回 DXGI_ERROR_DEVICE_RESET
- **THEN** 系统记录驱动重置日志
- **THEN** 系统通知用户设备异常（可选）

### Requirement: SwapChainPanel 关联与 Present 必须在 UI 线程
系统 SHALL 通过 `SynchronizationContext.Post` 将 `ISwapChainPanelNative.SetSwapChain` 与 `IDXGISwapChain.Present` 分派回 UI 线程执行。

#### Scenario: 捕获 UI 线程同步上下文
- **WHEN** `InitializeRenderer` 被 UI 线程调用
- **THEN** 系统保存 `SynchronizationContext.Current` 到实例字段
- **THEN** 后续跨线程调用使用该上下文 `Post`

#### Scenario: SetSwapChain 分派回 UI 线程
- **WHEN** 渲染线程创建好 SwapChain 后需要关联
- **THEN** 系统 `Post` 到 UI 线程调用 `ISwapChainPanelNative.SetSwapChain`
- **THEN** 渲染线程以超时 1s 等待关联完成后才进入渲染循环

#### Scenario: Present 分派回 UI 线程
- **WHEN** 渲染线程完成一帧上传到 BackBuffer
- **THEN** 系统 `Post` 到 UI 线程调用 `IDXGISwapChain.Present(1, None)`
- **THEN** 渲染线程以超时 200ms 等待 Present 完成后进入下一帧

### Requirement: SwapChain 使用逻辑像素尺寸
系统 SHALL 使用 `SwapChainPanel.ActualWidth/ActualHeight` 逻辑像素作为 SwapChain 尺寸，不乘以 DPI 缩放因子。

#### Scenario: 初始化使用逻辑像素
- **WHEN** 调用 `InitializeRenderer`
- **THEN** 传入 `width = (int)VideoPanel.ActualWidth` 、`height = (int)VideoPanel.ActualHeight`
- **THEN** 系统 NEVER 乘以 `XamlRoot.RasterizationScale`

#### Scenario: 避免 DPI 黑边
- **WHEN** 面板在 高 DPI 环境运行（如 125%/150%）
- **THEN** SwapChain 尺寸仍然为逻辑像素，WinUI 3 SwapChainPanel 内部负责物理像素映射
- **THEN** 画面不出现黑边

### Requirement: 加载文件须等待渲染上下文就绪
系统 SHALL 确保 `loadfile` 命令在 `mpv_render_context_create` 成功之后才执行，否则 mpv vo 初始化会报 `No render context set` 并永久禁用视频输出。

#### Scenario: 业务层仅先导航后加载
- **WHEN** 用户点击播放按钮（`MovieDetailViewModel.PlayCommand` / `SeriesDetailViewModel.PlayEpisodeCommand`）
- **THEN** 系统先调用 `_navigation.NavigateTo(typeof(PlayerPage), target)`
- **THEN** 系统再 `await _playback.PlayVideoAsync(target)`

#### Scenario: 服务层等待渲染器就绪
- **WHEN** `PlaybackControlService.PlayVideoAsync` 调用
- **THEN** 在 `_mpv.LoadFileAsync(...)` 前调用 `await _mpv.WaitForRendererReadyAsync(TimeSpan.FromSeconds(10))`
- **THEN** 只有在渲染器就绪后才发送 `loadfile` 命令

#### Scenario: 超时后仍继续加载（降级）
- **WHEN** `WaitForRendererReadyAsync` 超时未就绪
- **THEN** 系统记录警告日志但仍然发送 loadfile（避免完全阅不到视频事件）

### Requirement: mpv 诊断日志转发
系统 SHALL 启用 mpv 内部日志输出并转发到应用 ILogger。

#### Scenario: 启用 mpv 日志
- **WHEN** `mpv_initialize` 成功
- **THEN** 系统调用 `mpv_request_log_messages("info")` 订阅 info 及以上级别日志

#### Scenario: 转发 LOG_MESSAGE 事件
- **WHEN** 事件循环收到 `MPV_EVENT_LOG_MESSAGE`（id=2）
- **THEN** 系统解析 `mpv_event_log_message` 结构体获取 prefix/level/text
- **THEN** 系统按 mpv level 转发到 ILogger 的对应级别（fatal/error->Error, warn->Warning, info->Information, 其他->Debug）

