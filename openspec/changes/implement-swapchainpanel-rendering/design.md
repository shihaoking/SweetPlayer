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
