## Context

当前 MpvPlayerService.cs 已具备基本的 mpv 播放控制功能（加载、播放、暂停、跳转等），但 InitializeRenderer 方法仅为占位实现，未真正创建 D3D11 渲染管道。PlayerPage.xaml 包含 MpvPlayerControl 控件，但该控件尚未与 SwapChainPanel 集成进行硬件加速渲染。

项目使用 WinUI 3 技术栈，需要通过 P/Invoke 调用 libmpv 的渲染 API (`mpv_render_context_*`)，并结合 SharpDX.Direct3D11 创建 D3D11 设备和交换链，将 mpv 解码的视频帧渲染到 SwapChainPanel 上。

**约束条件**：
- 必须使用 SharpDX.Direct3D11 创建 D3D11 设备
- 必须通过 mpv_render_context API 进行渲染
- 渲染循环需要在独立线程中运行，避免阻塞 UI 线程
- 需要处理 SwapChainPanel 尺寸变化和设备丢失等异常情况

## Goals / Non-Goals

**Goals:**
- 在 MpvPlayerService 中实现完整的 D3D11 渲染管道
- 配置 mpv_render_context 使用 D3D11 API 进行硬件加速渲染
- 实现渲染循环，监听 mpv_render_context_update 回调并及时更新帧
- 将渲染纹理呈现到 SwapChainPanel，实现流畅的视频播放
- 处理渲染区域 Resize 事件，确保画面自适应
- 确保点击播放视频时正确导航到 PlayerPage.xaml

**Non-Goals:**
- 不实现软件渲染回退机制（仅支持 D3D11 硬件渲染）
- 不在此阶段实现多显示器、HDR 或其他高级渲染特性
- 不修改现有的播放控制逻辑（播放、暂停、跳转等）

## Decisions

### 1. 使用 SharpDX.Direct3D11 创建 D3D11 设备

**理由**：SharpDX 是成熟的 DirectX .NET 封装库，提供完整的 D3D11 API 支持，与 WinUI 3 的 SwapChainPanel 集成良好。

**替代方案**：
- 使用 Vortice.Windows：更现代的 DirectX 封装，但社区生态不如 SharpDX 成熟
- 直接使用 P/Invoke：实现复杂度高，容易出错

**选择 SharpDX 的原因**：文档完善，社区案例丰富，与 WinUI 3 集成经过验证。

### 2. 渲染循环在独立线程中运行

**理由**：mpv_render_context_render 是阻塞调用，在 UI 线程中运行会导致界面卡顿。使用独立线程配合 mpv_render_context_update 回调可以高效地仅在有新帧时渲染。

**实现方式**：
- 使用 `Task.Run` 创建后台任务
- 通过 `ManualResetEventSlim` 或类似机制等待 mpv_render_context_update 回调信号
- 渲染完成后通过 `DispatcherQueue` 更新 SwapChainPanel

### 3. 在 MpvPlayerService 中集中管理渲染上下文

**理由**：MpvPlayerService 已经管理 mpv 核心实例 (_mpvHandle)，将 _renderContext 也放在此类中可以保持生命周期一致，便于统一管理和资源释放。

**替代方案**：
- 在 MpvPlayerControl 中管理渲染上下文：会导致播放逻辑和渲染逻辑分离，增加同步复杂度

### 4. SwapChainPanel 集成方式

**实现方式**：
- 通过 `ISwapChainPanelNative` COM 接口获取 SwapChainPanel 的原生指针
- 使用 SharpDX 创建 SwapChain 并关联到 SwapChainPanel
- 每次渲染时通过 `mpv_render_context_render` 将帧渲染到 D3D11 纹理
- 调用 `swapChain.Present()` 呈现到屏幕

### 5. 添加必要的 P/Invoke 声明

需要在 MpvInterop.cs 中添加以下 mpv_render_context API：
- `mpv_render_context_create`
- `mpv_render_context_render`
- `mpv_render_context_update`
- `mpv_render_context_set_update_callback`
- `mpv_render_context_free`

## Risks / Trade-offs

### 风险 1：SharpDX 已停止维护
**影响**：SharpDX 已于 2019 年停止维护，未来可能与新版 Windows SDK 不兼容。
**缓解措施**：当前使用的 D3D11 API 较为稳定，短期内不会有兼容性问题。如需长期维护，可考虑迁移到 Vortice.Windows。

### 风险 2：渲染线程与 UI 线程同步问题
**影响**：渲染循环在后台线程运行，与 UI 线程的交互（如 Resize 事件）需要小心处理，否则可能导致死锁或崩溃。
**缓解措施**：
- 使用线程安全的队列或事件机制传递 Resize 通知
- 在渲染循环中定期检查终止标志，确保可以及时停止

### 风险 3：设备丢失（Device Lost）
**影响**：当显卡驱动重置或系统进入休眠时，D3D11 设备可能丢失，导致渲染失败。
**缓解措施**：
- 捕获 `SharpDXException` 中的 `DXGI_ERROR_DEVICE_REMOVED` 或 `DXGI_ERROR_DEVICE_RESET`
- 重新创建 D3D11 设备和渲染上下文
- 当前阶段暂不实现完整的设备丢失恢复，仅记录日志并停止渲染

### 风险 4：libmpv 动态库缺失
**影响**：如果运行环境未安装 libmpv-2.dll，应用将无法渲染视频。
**缓解措施**：当前已有降级机制（TryInitializeMpv），在 libmpv 不可用时使用模拟播放。渲染部分同样需要在 libmpv 可用性检查通过后才初始化。

## Migration Plan

### 部署步骤

1. **添加 SharpDX 依赖**：
   - 在 `SweetPlayer.csproj` 中添加 `SharpDX.Direct3D11` 和 `SharpDX.DXGI` NuGet 包

2. **扩展 MpvInterop.cs**：
   - 添加 `mpv_render_context_*` 相关的 P/Invoke 声明
   - 定义 `mpv_render_param` 结构体

3. **实现 MpvPlayerService.InitializeRenderer**：
   - 创建 D3D11 设备
   - 配置 mpv_render_context
   - 启动渲染循环线程

4. **更新 MpvPlayerControl**：
   - 确保包含 SwapChainPanel 控件
   - 在控件加载时调用 MpvPlayerService.InitializeRenderer

5. **测试验证**：
   - 运行应用，点击播放视频
   - 验证 PlayerPage 正确导航
   - 验证视频在 SwapChainPanel 中正确渲染
   - 测试 Resize、全屏等场景

### 回滚策略

如果渲染实现出现问题：
- 可以暂时回退 InitializeRenderer 为占位实现
- 播放控制逻辑不受影响，应用仍可正常运行（仅无视频画面）

## Open Questions

1. **是否需要支持多实例播放**？
   - 当前设计假定单个 MpvPlayerService 实例，如需多窗口播放需要调整架构

2. **渲染帧率控制**？
   - mpv_render_context_update 回调会通知何时有新帧，但如果帧率过高（如 120fps），是否需要限制渲染频率？
   - 建议：初期不限制，依赖 mpv 的内部节奏控制

3. **是否需要暴露渲染统计信息**？
   - 如 FPS、丢帧数等，用于调试和性能监控
   - 建议：可作为后续增强功能

## Implementation Notes (实际实现与原初设计的重大偏差)

在实际落地过程中发现多个原初设计未考虑的底层约束，需要记录以供后续维护参考。

### 偏差 1：采用 sw 渲染 API 而非 d3d11 渲染 API
**原设计**：使用 `MPV_RENDER_API_TYPE_D3D11`，传递 `MPV_RENDER_PARAM_D3D11_DEVICE`。
**实际实现**：使用 `MPV_RENDER_API_TYPE_SW`（软件渲染模式），让 mpv 输出 BGR0 像素到应用提供的 CPU 内存，再用 `UpdateSubresource` 上传到 SwapChain 后缓冲区。
**原因**：LibMpv.Client 1.0.0 与当前 libmpv-2.dll 的 D3D11 渲染 API 集成存在在本项目环境中难以调通的冗余问题，而 sw 渲染路径鲁棒且在今代硬件上性能足够。

### 偏差 2：D3D11 设备创建于专用渲染线程
**原设计**：未明确线程所属，隐含在 InitializeRenderer（UI 线程）创建。
**实际约束**：D3D11 ImmediateContext 在未启用 `D3D11_CREATE_DEVICE_MULTITHREADED` 时具有线程亲和性：`MapSubresource` 从非创建设备的线程调用返回 `E_INVALIDARG (0x80070057)`。
**修正方案**：所有 D3D11 资源创建与渲染调用集中在 `RenderThreadEntry` 专用 `Thread` 上执行。

### 偏差 3：SwapChainPanel 关联与 Present 必须在 UI 线程
**原设计**：未区分调用线程。
**实际约束**：WinUI 3 的 `ISwapChainPanelNative.SetSwapChain` 和 `IDXGISwapChain.Present` 必须在 UI 线程调用，否则焦点丢失或帧不被合成。
**修正方案**：`InitializeRenderer` 接收时捕获 `SynchronizationContext.Current`；SwapChain 关联和 Present 都通过 `SynchronizationContext.Post` 分派回 UI 线程。

### 偏差 4：加载文件须等待渲染上下文就绪（关键）
**原设计**：未考虑 mpv vo 初始化与 `mpv_render_context_create` 之间的严格顺序。
**实际事故**：若 `loadfile` 在 `mpv_render_context_create` 之前执行，mpv vo 初始化时输出 `No render context set` 错误并永久禁用视频输出（`Video: no video`）。表现为间歇性有声无画面，取决于两者完成顺序的竞态。
**修正方案**：
1. 业务层：`MovieDetailViewModel` / `SeriesDetailViewModel` 先 `NavigateTo(PlayerPage)` 再 `PlayVideoAsync`（先创建控件才可能创建渲染器）。
2. 服务层：`IMpvPlayerService` 增加 `IsRendererReady` 与 `WaitForRendererReadyAsync(timeout)`；MpvPlayerService 内部以 `TaskCompletionSource<bool>` 维护就绪信号，`mpv_render_context_create` 成功后 `TrySetResult(true)`。
3. `PlaybackControlService.PlayVideoAsync` 在 `LoadFileAsync` 前 `await _mpv.WaitForRendererReadyAsync(TimeSpan.FromSeconds(10))`。

### 偏差 5：SwapChain 使用逻辑像素尺寸
**原设计**：传递 `int width / int height`，未明确单位。
**实际约束**：WinUI 3 SwapChainPanel 内部处理 DPI 缩放，若传递物理像素（乘以 `RasterizationScale`）会造成 SwapChain 与面板不匹配的黑边。
**修正方案**：始终使用 `VideoPanel.ActualWidth/ActualHeight` 逻辑像素传入。

### 偏差 6：启用 mpv 内部日志用于诊断
**原设计**：未提及。
**实际需要**：间歇性问题难以从应用侧定位，必须获取 mpv 内部错误（如 `vo/libmpv: No render context set`）才能找到根因。
**修正方案**：`mpv_initialize` 后立即调用 `mpv_request_log_messages("info")`；事件循环中处理 `MPV_EVENT_LOG_MESSAGE`（id=2）并按 mpv level 映射到 ILogger。

### 最终架构示意
```
UI 线程                       事件循环线程               渲染线程 (MpvRenderThread)
--------                       ------------------         ----------------------------
MpvPlayerService.ctor()
  TryInitializeMpv()
    mpv_create / initialize
    mpv_request_log_messages
    Task.Run(EventLoop)  ----> mpv_wait_event 循环
NavigateTo(PlayerPage)
MpvPlayerControl.OnLoaded
  AttachPlayer()
  TryInitializeRenderer()
    捕获 SynchronizationContext
    启动 RenderThread       --------------------------> Device(BgraSupport|VideoSupport)
                                                          SwapChain1 创建
                              <---- Post SetSwapChain ---  (等待<=1s)
  SetSwapChain
                              ---- assocDone.Set()  --->  CreateMpvRenderContext("sw")
                                                          SetUpdateCallback
                                                          _rendererReadyTcs.TrySetResult(true)
await WaitForRendererReady
LoadFileAsync(loadfile cmd)
                               <---- mpv 事件 ---       RenderLoop:
                                                            Wait(_renderUpdateEvent, 100ms)
                                                            mpv_render_context_render -> CPU 缓冲区
                                                            UpdateSubresource(backBuffer, frameBuf)
                              <---- Post Present ----      (等待<=200ms)
SwapChain.Present
```

