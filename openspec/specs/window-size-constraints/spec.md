## ADDED Requirements

### Requirement: 主窗口固定宽度禁止调整大小
主窗口（媒体库所在窗口）SHALL 固定宽度为 1750 像素，并禁止用户调整窗口大小。

#### Scenario: 主窗口启动时固定宽度
- **WHEN** 主窗口创建并显示
- **THEN** 窗口宽度为 1750 像素，用户无法通过拖拽边缘调整窗口大小

#### Scenario: 主窗口最大化按钮禁用
- **WHEN** 主窗口不可调整大小
- **THEN** 标题栏最大化按钮置灰或不可点击（`OverlappedPresenter.IsMaximizable = false`，`IsResizable = false`）

### Requirement: 播放窗口视频内容区域默认尺寸
播放窗口（PlayerWindow）的视频内容区域（客户区）默认初始尺寸 SHALL 为 1920×1130 像素，不含标题栏高度。

#### Scenario: 播放窗口启动时客户区尺寸
- **WHEN** 播放窗口以窗口模式创建并显示
- **THEN** 客户区（视频渲染区域）精确为 1920×1130 像素，窗口总尺寸为 1920 × (1130 + 标题栏高度)

#### Scenario: 播放窗口标题栏高度补偿
- **WHEN** 播放窗口启用 `ExtendsContentIntoTitleBar` 标题栏
- **THEN** 窗口 Resize 调用中动态读取标题栏高度，将高度设置为 `1080 + titleBarHeight`，确保客户区不受标题栏影响
