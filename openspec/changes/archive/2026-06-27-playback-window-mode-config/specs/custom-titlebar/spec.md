## ADDED Requirements

### Requirement: MainWindow 自定义无边框标题栏
MainWindow SHALL 通过 `ExtendsContentIntoTitleBar = true` 将 XAML 内容拓展到标题栏区域，实现无边框效果。标题栏左侧展示应用图标和标题文本，右侧由系统窗口控制按钮自动覆盖。

#### Scenario: MainWindow 启动时显示自定义标题栏
- **WHEN** MainWindow 创建并显示
- **THEN** 窗口顶部显示自定义标题栏区域（高度 48px），左侧展示应用图标和"SweetPlayer"标题文本，系统最小化/最大化/关闭按钮自动覆盖在右上角

#### Scenario: 用户拖拽自定义标题栏移动窗口
- **WHEN** 用户按住自定义标题栏的拖拽区域并拖动
- **THEN** 窗口跟随鼠标移动（通过 `SetTitleBar()` 实现）

### Requirement: PlayerWindow 自定义无边框标题栏
PlayerWindow SHALL 通过 `AppWindowTitleBar.ExtendsContentIntoTitleBar = true` 将 XAML 覆盖层内容拓展到标题栏区域，实现无边框效果。标题栏左侧仅展示应用图标（无标题文字），右侧由系统窗口控制按钮自动覆盖。

#### Scenario: PlayerWindow 窗口模式下显示自定义标题栏
- **WHEN** 播放窗口以窗口模式显示
- **THEN** 覆盖层顶部显示自定义标题栏行，左侧仅展示应用图标（无标题文字），系统窗口控制按钮自动覆盖在右上角

#### Scenario: 用户拖拽播放窗口标题栏
- **WHEN** 用户按住播放窗口自定义标题栏的拖拽区域并拖动
- **THEN** 播放窗口跟随鼠标移动（通过 `SetDragRectangles()` 实现）

### Requirement: 全屏模式下隐藏自定义标题栏
PlayerWindow 在全屏模式下 SHALL 隐藏自定义标题栏行，视频铺满整个屏幕。退出全屏后恢复显示。

#### Scenario: 进入全屏时标题栏隐藏
- **WHEN** PlayerWindow 切换到全屏模式
- **THEN** 自定义标题栏行自动隐藏，视频铺满整个屏幕

#### Scenario: 退出全屏时标题栏恢复
- **WHEN** PlayerWindow 从全屏模式退出
- **THEN** 自定义标题栏行恢复显示

### Requirement: 自定义标题栏内容差异化
MainWindow 和 PlayerWindow 的自定义标题栏内容 SHALL 有意差异：MainWindow 左侧展示应用图标 + “SweetPlayer”标题文字，而 PlayerWindow 左侧仅展示应用图标（无标题文字）。两者右侧均由系统窗口控制按钮自动覆盖。

#### Scenario: 标题栏内容展示差异
- **WHEN** MainWindow 和 PlayerWindow 同时显示自定义标题栏
- **THEN** MainWindow 标题栏左侧显示应用图标和“SweetPlayer”文字；PlayerWindow 标题栏左侧仅显示应用图标，不展示标题文字
