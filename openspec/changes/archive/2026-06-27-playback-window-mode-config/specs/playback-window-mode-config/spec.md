## ADDED Requirements

### Requirement: 播放窗口模式枚举定义
系统 SHALL 定义 `PlaybackWindowMode` 枚举，包含 `Windowed`（值为 0）和 `FullScreen`（值为 1）两个成员。

#### Scenario: 枚举值正确定义
- **WHEN** 系统初始化 `PlaybackWindowMode` 枚举
- **THEN** `Windowed` 值为 0，`FullScreen` 值为 1

### Requirement: 用户设置持久化默认播放窗口模式
系统 SHALL 在 `IUserSettingsService` 中提供 `DefaultPlaybackWindowMode` 属性（类型为 `PlaybackWindowMode`），默认值为 `Windowed`，并通过 `UserSettingsService` 持久化到本地 JSON 文件。

#### Scenario: 首次启动使用默认值
- **WHEN** 用户首次启动应用，无已有设置文件
- **THEN** `DefaultPlaybackWindowMode` 值为 `Windowed`（0）

#### Scenario: 用户修改设置后持久化
- **WHEN** 用户将默认播放窗口模式改为 `FullScreen`
- **THEN** 系统将值写入 `user_settings.json`，下次启动时读取为 `FullScreen`

### Requirement: 设置页面配置入口
系统 SHALL 在设置页面（`SettingsPage`）的"播放设置"卡片中提供"默认播放窗口模式"下拉选择框，选项为"窗口模式"和"全屏模式"。

#### Scenario: 设置页面显示配置项
- **WHEN** 用户导航到设置页面
- **THEN** "播放设置"卡片中显示"默认播放窗口模式" ComboBox，当前选中项与用户设置一致

#### Scenario: 用户切换设置值
- **WHEN** 用户在 ComboBox 中从"窗口模式"切换到"全屏模式"
- **THEN** `SettingsViewModel` 将新值写入 `IUserSettingsService.DefaultPlaybackWindowMode`，值立即持久化
