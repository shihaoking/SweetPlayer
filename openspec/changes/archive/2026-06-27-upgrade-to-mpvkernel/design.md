## Context

SweetPlayer 当前播放引擎基于 LibMpv.Client + SharpDX.Direct3D11，通过 `mpv_render_context` + SwapChainPanel 实现视频渲染。该方案需要手动管理 D3D11 设备、SwapChain、渲染循环和 CPU 帧缓冲区，代码量约 990 行（MpvPlayerService.cs），包含大量 unsafe 代码和复杂的线程同步逻辑。

mpv-kernel（`Richasy.MpvKernel.*`）是一个分层 .NET 封装库：
- **Interop 层**: P/Invoke 绑定 libmpv-2.dll，使用 LibraryImport 源生成器
- **Core 层**: MpvClient 高级异步 API，事件轮询、属性观察、播放控制
- **WinUI 层**: MpvPlayerWindow 独立播放窗口、交互控件

采用 `wid`（Window ID）模式：将 HWND 传给 mpv，由 mpv 内部完成 GPU 渲染，应用无需管理渲染管线。

**约束**：
- 所有现有播放配置功能（字幕、音轨、音量/增强、速度、画面比例、章节）必须完整迁移
- PlayerViewModel 的命令/绑定/配置逻辑尽量保持不变
- 键盘快捷键映射不变

## Goals / Non-Goals

**Goals:**
- 用 mpv-kernel 的 MpvClient 替换 LibMpv.Client + SharpDX 播放引擎
- 采用 `wid` 独立窗口模式实现 GPU 零拷贝渲染
- 完整保留所有播放配置功能和 UI 布局
- 大幅简化 MpvPlayerService（~990→~250 行）
- 消除所有手动渲染逻辑和 unsafe 代码

**Non-Goals:**
- 不改变 PlayerViewModel 的业务逻辑（命令、绑定、配置选项）
- 不改变播放进度持久化、HDR 检测、元数据刮取等非播放引擎功能
- 不重构媒体库 UI、文件源浏览器等非播放器页面
- 不引入 mpv-kernel 的手势交互面板（PlayerInteractivePanel）替换现有 UI 控件
- 不改变网络播放（WebDAV）的功能逻辑

## Decisions

### 决策 1：使用 `wid` 独立窗口模式而非 `libmpv render API`

**选择**: 将 HWND 传给 mpv，由 mpv 内部管理渲染

**理由**:
- 零拷贝：mpv 直接在目标窗口 GPU 渲染，消除 GPU→CPU→GPU 路径
- 极简：无需管理 D3D11 Device、SwapChain、渲染循环、帧缓冲
- mpv-kernel 的 `MpvPlayOptions.WindowHandle` 原生支持此模式

**替代方案**: 继续使用 `mpv_render_context` 通过 mpv-kernel 的 Interop 层手动渲染
- 缺点：仍需管理渲染管线，无实质简化

### 决策 2：自定义 PlayerWindow 而非直接使用 MpvPlayerWindow

**选择**: 创建自定义 `PlayerWindow.cs`，手动管理 AppWindow + DesktopWindowXamlSource

**理由**:
- 现有 PlayerPage 有复杂的自定义 UI（顶部栏、底部控制栏、设置侧边栏、OSD 通知、Up Next），需要完全控制 UI 层
- MpvPlayerWindow 的 UI 注入机制（SetUIElement/SetBackgroundElement）虽可用，但限制了控件层级和交互细节
- 自定义窗口可以更好地集成现有样式（OverlayCapsuleStyle、GhostButtonStyle 等）

**替代方案**: 直接使用 MpvPlayerWindow + SetUIElement 注入覆盖层
- 缺点：交互面板（PlayerInteractivePanel）的手势逻辑可能与现有控件冲突
- 可作为后续优化考虑

### 决策 3：PlayerPage 作为桥接页而非完全移除

**选择**: 保留 PlayerPage 但简化为窗口启动桥接页

**理由**:
- 现有导航架构（NavigationService）以 Page 为中心，移除 PlayerPage 需要重构整个导航系统
- 桥接页接收导航参数后打开 PlayerWindow，窗口关闭后自动 GoBack()，对现有架构影响最小
- 页面显示为全黑，用户几乎感知不到

### 决策 4：MpvPlayerService 使用 MpvClient + MpvNative 混合调用

**选择**: 高层控制用 MpvClient API（PlayAsync/PauseAsync 等），低层配置（字幕/音轨/画面比例等）直接调用 MpvNative.SetOptionString / SetProperty

**理由**:
- MpvClient 已封装好播放控制、事件通知、属性观察
- 字幕/音轨/画面比例等配置在 MpvClient 中未暴露，需直接调用 Interop 层
- mpv-kernel 的 MpvClient.Handle 属性暴露了 MpvInteropHandle，可直接传给 MpvNative 方法

### 决策 5：SweetPlayer.Services TargetFramework 升级

**选择**: 从 `net8.0` 升级为 `net8.0-windows10.0.22621.0`

**理由**:
- mpv-kernel Core 层目标 net9.0，但通过 NuGet 引用时 net8.0 项目可兼容
- Services 层需要 Windows 特定 API（如窗口句柄传递），需显式声明 Windows 目标
- 保持与主项目（net8.0-windows10.0.22621.0）一致的 TFM

### 决策 6：PlayerWindow 通过 AssociateWithDispatcherQueue 与主窗口同线程

**选择**: 在 `ShowAsync()` 中调用 `_appWindow.AssociateWithDispatcherQueue(_dispatcherQueue)` 将播放窗口绑定到主窗口的 DispatcherQueue

**理由**:
- `AssociateWithDispatcherQueue` 确保 AppWindow 和主窗口共享同一 UI 线程的消息循环
- 窗口销毁时 OS 自动清理 XAML Island 的 DesktopChildSiteBridge 子窗口
- 焦点自动归还主窗口，标题栏和非客户区输入正常响应
- `Close()` 只需调用 `_appWindow?.Destroy()`，无需手动 Win32 P/Invoke
- 与 MpvKernel 的 `MpvPlayerWindow` 实现保持一致

**替代方案**: 不调用 AssociateWithDispatcherQueue，手动用 Win32 API（`EnableWindow`/`SetActiveWindow`/`SetFocus`/`DestroyWindow`）强制清理
- 缺点：需约 155 行 Win32 P/Invoke 代码，依赖 Windows 内部行为，边界情况多

### 决策 7：mpv 实例采用 Transient 生命周期（每次播放新建，窗口关闭销毁）

**选择**: `IMpvPlayerService` 和 `IPlaybackControlService` 注册为 Transient，每次播放创建全新 MpvClient，窗口关闭时销毁

**理由**:
- 每次播放都是干净的新 MpvClient，无配置状态残留（音量、速度、字幕轨道等）
- 与 MpvKernel WinUI 示例完全一致
- 实现细节：`PlayerPage` 使用 `ActivatorUtilities.CreateInstance` 创建单个 `IMpvPlayerService` 实例，同时传给 `PlayerWindow` 和 `PlaybackControlService`（避免 Transient 模式下各自解析到不同实例）
- `PlayerWindow.DisposeAsync()` 调用 `_mpv.DisposeAsync()` 销毁 mpv 实例

**替代方案**: 保持 Singleton + Stop()（每次播放只停止不销毁 mpv）
- 缺点：旧 mpv 实例可能残留 wid、解码配置、字幕轨道等状态，与 MpvKernel 行为不一致

### 决策 8：mpv_command 参数数组需显式 NULL 终止（.NET LibraryImport 约束）

**选择**: 在 `MpvClient.PlayAsync` 中将 `commandArgs` 末尾追加 `null!` 作为终止符

**理由**:
- `mpv_command` 的 C 签名要求 `const char **args` 是 NULL-terminated 数组
- .NET `LibraryImport` 源生成器对 `string[]` 的 marshaling 不会自动添加 NULL 终止符
- 这是 P/Invoke `LibraryImport` + `string[]` 的已知约束，所有使用数组形式调用 `mpv_command` 的代码都需注意
- 不加 NULL 终止符时，mpv 会读取数组边界后的内存，将垃圾字节当作命令参数解析

### 决策 9：wid（窗口句柄）仅在 MpvClient 初始化时设置一次

**选择**: 在 `InitializeAsync` 中通过 `SetOption("wid", ...)` 设置窗口句柄，`LoadFileAsync` 不再传入 `MpvPlayOptions`

**理由**:
- `wid` 模式下 mpv 关联窗口后在实例生命周期内不需要重新设置
- `LoadFileAsync` 简化为只调用 `PlayAsync(filePath)`，语义更清晰
- 避免每次播放时 `SetOption("wid", ...)` 带来的不必要调用

## Risks / Trade-offs

**[风险] mpv-kernel NuGet 包兼容性问题**
→ 在实施前先创建测试分支验证包引用和编译；如有问题可退回继续使用 LibMpv.Client

**[风险] 独立窗口模式的焦点和输入处理**
→ AppWindow 与主窗口的焦点切换可能异常；需充分测试键盘快捷键在独立窗口中的行为

**[风险] PlayerWindowOverlay 迁移遗漏**
→ 逐步迁移：先迁移核心控件（进度条、播放按钮），再迁移设置面板和 OSD；每步编译验证

**[权衡] 自定义窗口 vs MpvPlayerWindow**
→ 自定义窗口代码量更大，但控制力更强；长期可评估是否迁移到 MpvPlayerWindow

**[风险] 字幕/音轨等配置通过 MpvNative 直接调用**
→ 需确保 MpvClient.Handle 在播放期间有效；通过 IsDisposed 检查保护

**[风险] 窗口关闭时的资源清理顺序**
→ PlayerWindow.DisposeAsync → 销毁 mpv 实例（持有全局信号量）→ 销毁 AppWindow → XamlSource Dispose；需确保多次播放/关闭不泄漏

**[约束] libmpv DLL 进程内单例**
→ libmpv-2.dll 在进程内只有一份，多个 MpvClient 实例不能并发存在
→ 通过全局 `SemaphoreSlim` 互斥锁 + 100ms 冷却期确保前一个实例完全销毁后才创建新实例
