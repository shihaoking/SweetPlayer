## Why

1. 当前播放视频时，播放窗口始终以窗口模式启动，用户每次都需要手动双击或按 F 键切换到全屏。对于偏好全屏观影的用户，这一重复操作降低了体验。需要增加一个用户可配置的默认播放窗口模式，让用户选择"窗口模式"或"全屏模式"作为播放时的默认行为。
2. 当前底部控制栏的布局不够合理：后退/播放/快进三个核心按钮偏左，倍速按钮占据中间位置。需要将三个核心播放控制按钮居中显示，倍速按钮移至右侧与字幕等功能按钮并排。
3. 当前所有窗口使用系统默认标题栏，视觉风格不统一。需要通过 `ExtendsContentIntoTitleBar` 将自定义内容拓展到标题栏区域，实现无边框效果：MainWindow 左侧展示应用图标+标题，右侧展示窗口控制按钮；PlayerWindow 同理但是不展示标题文字。
4. 当前主窗口可随意调整大小，且无默认尺寸约束。需要固定主窗口宽度为 1750 并禁止调整大小；视频播放窗口的视频内容区域默认初始尺寸应为 1920×1130（不含标题栏高度）。

## What Changes

- 新增用户设置项 `DefaultPlaybackWindowMode`，支持 `Windowed`（窗口）和 `FullScreen`（全屏）两个选项，默认为 `Windowed`
- 设置页面新增"默认播放窗口模式"配置项（ComboBox）
- 播放窗口启动时读取该设置，若为全屏模式则自动进入全屏状态
- 底部控制栏布局重构：后退/播放/快进三按钮水平居中，倍速按钮移至右侧字幕按钮旁
- 所有窗口通过 `ExtendsContentIntoTitleBar` 实现无边框自定义标题栏：左侧没有内容，右侧最小化/最大化/关闭按钮
- 主窗口固定宽度 1750，禁止调整大小
- 播放窗口视频内容区域默认初始尺寸 1920×1130（不含标题栏高度）

## Capabilities

### New Capabilities
- `playback-window-mode-config`: 播放窗口模式的用户配置功能，包括设置持久化、设置 UI、播放窗口启动时自动应用
- `bottom-bar-layout`: 底部控制栏布局重构，核心播放按钮居中、倍速按钮右移
- `custom-titlebar`: 所有窗口通过 `ExtendsContentIntoTitleBar` 实现无边框自定义标题栏，包含应用图标、标题文本和窗口控制按钮
- `window-size-constraints`: 主窗口固定宽度 1750 禁止调整大小，播放窗口视频内容区域默认 1920×1130

### Modified Capabilities
- `video-playback`: 播放窗口启动行为新增全屏模式自动进入逻辑（原需求仅窗口模式）；底部控制栏布局调整；自定义标题栏；播放窗口尺寸约束

## Impact

- **设置层**：`IUserSettingsService` / `UserSettingsService` 新增属性，`UserSettingsData` 新增字段
- **ViewModel 层**：`SettingsViewModel` 新增绑定属性
- **UI 层**：`SettingsPage.xaml` 新增配置控件
- **播放层**：`PlayerPage.xaml.cs` 在 `ShowAsync()` 后根据设置自动切换全屏
- **App.xaml.cs**：主窗口初始化时设置固定宽度 1750，禁止调整大小
- **MainWindow.xaml / .cs**：`ExtendsContentIntoTitleBar = true`，新增自定义标题栏 XAML，`SetTitleBar()` 设置拖拽区域
- **PlayerWindow.cs**：`AppWindowTitleBar.ExtendsContentIntoTitleBar = true`，恢复标题栏；调整 `Resize()` 为 `1920 × (1130 + 标题栏高度)` 确保客户区为 1920×1130
- **PlayerWindowOverlay.xaml**：新增自定义标题栏行，底部控制栏 Grid 列布局重构
