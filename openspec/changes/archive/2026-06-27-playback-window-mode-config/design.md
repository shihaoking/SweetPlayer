## Context

SweetPlayer 当前播放视频时，`PlayerPage.OnNavigatedTo` 创建 `PlayerWindow` 并调用 `ShowAsync()`，始终以窗口模式启动播放窗口。用户需手动双击或按 F 键切换到全屏。

现有设置体系：
- `IUserSettingsService` / `UserSettingsService`：本地 JSON 文件持久化，目前仅有 `AutoResumePlayback` 一个设置项
- `SettingsViewModel`：MVVM 绑定，通过 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 实现
- `SettingsPage.xaml`：设置 UI，当前仅展示"自动恢复播放进度"开关

`PlayerWindow` 已具备 `ToggleFullScreen()` 方法，通过 `AppWindowPresenterKind.FullScreen` 实现全屏切换。

现有底部控制栏布局（`PlayerWindowOverlay.xaml`）：
- 左列（Column 0）：后退 10s + 播放/暂停 + 快进 10s
- 中列（Column 1）：倍速按钮（居中）
- 右列（Column 2）：字幕 + 音轨 + 音量 + 全屏 + 更多

播放窗口标题栏状态：
- 当前 `PlayerWindow.ShowAsync()` 中调用 `presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` 隐藏了标题栏
- 原始动机：消除标题栏占用客户区高度导致的视频左右黑边
- 副作用：用户无法通过标准方式最小化/最大化/关闭窗口

MainWindow 标题栏状态：
- 当前使用系统默认标题栏，未设置 `ExtendsContentIntoTitleBar`
- NavigationView 控件自带顶部空白区域，与系统标题栏形成双重顶部空间

## Goals / Non-Goals

**Goals:**
- 新增 `DefaultPlaybackWindowMode` 设置项，支持 `Windowed` / `FullScreen` 两种模式，默认 `Windowed`
- 设置页面提供用户可操作的配置 UI
- 播放窗口启动时自动应用用户配置
- 底部控制栏后退/播放/快进三按钮水平居中，倍速按钮移至右侧字幕按钮旁
- 所有窗口通过 `ExtendsContentIntoTitleBar` 实现无边框自定义标题栏，左侧应用图标+标题，右侧窗口控制按钮
- 主窗口固定宽度 1750，禁止调整大小
- 播放窗口视频内容区域默认初始尺寸 1920×1130（不含标题栏高度）

**Non-Goals:**
- 不修改 `MpvKernel.Core` 或 `MpvKernel.Interop`（受保护规则约束）
- 不新增"记住上次窗口位置/大小"功能
- 不处理多显示器场景下的全屏目标显示器选择（使用系统默认）

## Decisions

### 1. 设置值类型选择：枚举字符串 vs 布尔值

**方案 A（选定）**：使用枚举类型 `PlaybackWindowMode`（`Windowed = 0`, `FullScreen = 1`），在 JSON 中以整数存储。

**方案 B**：使用 `bool DefaultFullScreen` 布尔值。

**选择理由**：枚举方案更具扩展性，未来如需增加"无边框窗口"等模式只需新增枚举值，无需重命名属性。序列化以整数存储，兼容性好。

### 2. 全屏应用时机：PlayerPage 层触发

在 `PlayerPage.OnNavigatedTo` 中，`_playerWindow.ShowAsync()` 完成后、设置 overlay 之前，读取设置并调用 `_playerWindow.ToggleFullScreen()`。

**替代方案**：在 `PlayerWindow.ShowAsync()` 内部读取设置自动全屏。

**选择理由**：`PlayerWindow` 是 Transient 注册的窗口类，不应依赖用户设置服务。将配置读取放在 `PlayerPage` 调用侧，保持 `PlayerWindow` 的纯净性和可测试性。

### 3. 设置 UI 控件：ComboBox

使用 WinUI 3 的 `ComboBox` 控件，提供"窗口模式"和"全屏模式"两个选项。

**替代方案**：`ToggleSwitch`（仅两值）或 `RadioButtons`。

**选择理由**：`ComboBox` 与枚举扩展性一致，未来增加新模式只需添加条目。`ToggleSwitch` 虽然二值场景更直观，但不利于后续扩展。

### 4. 底部控制栏布局重构

当前控制按钮行的 Grid 采用三列布局（左: 播放控制 / 中: 倍速 / 右: 功能按钮）。重构为：
- 左列（`*`）：空白占位
- 中列（`Auto`）：后退 + 播放/暂停 + 快进（水平居中）
- 右列（`Auto`）：倍速 + 字幕 + 音轨 + 音量 + 全屏 + 更多

**替代方案**：使用绝对定位将三组按钮分别对齐。

**选择理由**：Grid 三列布局（`*` / `Auto` / `Auto`）保持原有结构最小变更，左列自动填充确保中右列内容靠右集中，视觉平衡。

### 5. 所有窗口自定义无边框标题栏

统一采用 `ExtendsContentIntoTitleBar` 方案，将 XAML 内容拓展到标题栏区域，实现无边框视觉效果。

#### MainWindow（WinUI Window 类）
- 设置 `this.ExtendsContentIntoTitleBar = true`
- 在 `MainWindow.xaml` 顶部新增自定义标题栏 Grid（高度 48px）：
  - 左侧：应用图标 + "SweetPlayer" 标题文本
  - 右侧：占位空白（系统窗口控制按钮自动覆盖右上角）
- 调用 `this.SetTitleBar(titleBarElement)` 设置拖拽区域
- NavigationView 顶部对齐到自定义标题栏下方

#### PlayerWindow（AppWindow 类）
- 保持 `presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: true)` 启用标题栏
- 设置 `_appWindow.TitleBar.ExtendsContentIntoTitleBar = true`
- 在 `PlayerWindowOverlay.xaml` 顶部新增自定义标题栏行：
  - 左侧：仅应用图标（**无标题文字**，与 MainWindow 区分）
  - 右侧：占位空白（系统窗口控制按钮自动覆盖）
- 通过 `_appWindow.TitleBar.SetDragRectangles()` 设置可拖拽区域（排除按钮区域）
- 全屏模式下自定义标题栏行自动隐藏

**两窗口标题栏内容对比：**

| 窗口 | 左侧内容 | 右侧 |
|------|-----------|---------|
| MainWindow | 应用图标 + “SweetPlayer”标题文字 | 系统最小化/最大化/关闭 |
| PlayerWindow | 应用图标（无标题文字） | 系统最小化/最大化/关闭 |

**替代方案**：使用系统原生标题栏（`hasTitleBar: true` + `IconShowOption`）。

**选择理由**：`ExtendsContentIntoTitleBar` 方案让所有窗口视觉风格统一，自定义标题栏可精确控制布局和样式，与现代 WinUI 3 应用风格一致。系统原生标题栏样式受系统主题影响，无法与播放窗口覆盖层风格统一。

### 6. 窗口尺寸约束

#### 主窗口（MainWindow）
- 在 `App.xaml.cs` 的 `OnLaunched` 中，创建 MainWindow 后通过 `AppWindow` 设置固定宽度 1750
- 通过 `OverlappedPresenter.IsResizable = false` 禁止用户调整窗口大小
- 高度由系统根据宽度自动计算，或设置为固定值（根据屏幕分辨率适配）

#### 播放窗口（PlayerWindow）
- 当前 `_appWindow.Resize(new SizeInt32(1920, 1130))` 设置的是窗口总尺寸（含标题栏）
- 启用 `ExtendsContentIntoTitleBar` 后标题栏可见（高度约 32px），需将 Resize 调整为 `SizeInt32(1920, 1130 + titleBarHeight)` 以确保客户区精确为 1920×1130
- 通过读取 `_appWindow.TitleBar.PreferredHeightOption` 获取标题栏实际高度后计算

**替代方案**：硬编码标题栏高度为 48px。

**选择理由**：动态读取标题栏高度可适配不同系统 DPI 和主题设置，避免硬编码带来的适配问题。

## Risks / Trade-offs

- **[全屏启动闪烁]**：窗口先以窗口模式出现再切换全屏，可能有短暂闪烁 → 缓解：`ShowAsync()` 后立即调用 `ToggleFullScreen()`，切换在首帧渲染前完成
- **[设置同步]**：播放期间用户在设置页更改默认模式，不影响当前播放窗口 → 设计如此，仅在新播放时生效，符合用户预期
- **[布局重构范围]**：仅修改 `PlayerWindowOverlay.xaml` 的底部控制按钮行 Grid 布局，不涉及功能逻辑变更，所有按钮的 Click 事件处理不变
- **[标题栏恢复后视频黑边]**：启用 `ExtendsContentIntoTitleBar` 后 XAML 内容拓展到标题栏区域，客户区大小不变，窗口模式下视频黑边问题与之前隐藏标题栏时一致，无额外影响
- **[自定义标题栏复杂度]**：需实现拖拽区域设置、窗口控制按钮位置预留 → 使用 WinAppSDK 内置 API（`SetTitleBar` / `SetDragRectangles`）最小化工作量
