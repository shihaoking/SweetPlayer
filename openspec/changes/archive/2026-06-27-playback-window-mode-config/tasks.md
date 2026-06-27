## 1. 枚举与设置层

- [x] 1.1 在 `SweetPlayer.Services/Settings/` 下新建 `PlaybackWindowMode.cs`，定义 `PlaybackWindowMode` 枚举（`Windowed = 0`, `FullScreen = 1`）
- [x] 1.2 在 `IUserSettingsService` 接口中新增 `DefaultPlaybackWindowMode` 属性（类型 `PlaybackWindowMode`）
- [x] 1.3 在 `UserSettingsService` 中实现该属性，在 `UserSettingsData` 内部类中新增对应字段（默认值 `PlaybackWindowMode.Windowed`），setter 中触发 `SaveAsync()`

## 2. ViewModel 层

- [x] 2.1 在 `SettingsViewModel` 中新增 `SelectedPlaybackWindowModeIndex` 属性（`int` 类型），绑定到 ComboBox 的 `SelectedIndex`
- [x] 2.2 实现 `OnSelectedPlaybackWindowModeIndexChanged` partial 方法，将索引转换为 `PlaybackWindowMode` 写入 `IUserSettingsService`
- [x] 2.3 构造函数中从 `_userSettings.DefaultPlaybackWindowMode` 初始化 `SelectedPlaybackWindowModeIndex`

## 3. 设置页面 UI

- [x] 3.1 在 `SettingsPage.xaml` 的"播放设置"卡片中，在 `AutoResumeToggle` 下方新增 `ComboBox`，Header 为"默认播放窗口模式"，包含"窗口模式"和"全屏模式"两个 `ComboBoxItem`
- [x] 3.2 将 ComboBox 的 `SelectedIndex` 双向绑定到 `ViewModel.SelectedPlaybackWindowModeIndex`
- [x] 3.3 在 ComboBox 下方添加说明文本："设置播放视频时的默认窗口模式"

## 4. 播放启动逻辑

- [x] 4.1 在 `PlayerPage.OnNavigatedTo` 中，`ShowAsync()` 之后读取 `IUserSettingsService.DefaultPlaybackWindowMode`
- [x] 4.2 若值为 `FullScreen`，调用 `_playerWindow.ToggleFullScreen()` 进入全屏模式

## 5. 底部控制栏布局重构

- [x] 5.1 修改 `PlayerWindowOverlay.xaml` 底部控制按钮行的 Grid 列定义：左列改为 `*`（空白占位）、中列改为 `Auto`（播放控制）、右列保持 `Auto`（功能按钮）
- [x] 5.2 将原左列（Column 0）的后退/播放/快进 StackPanel 移至中列（Column 1），设置 `HorizontalAlignment="Center"`
- [x] 5.3 移除原中列（Column 1）的倍速按钮，将其移入右列（Column 2）字幕按钮之前
- [x] 5.4 右列 StackPanel 中按钮顺序：倍速 + 字幕 + 音轨 + 音量 + 全屏 + 更多

## 6. 自定义无边框标题栏

- [x] 6.1 修改 `MainWindow.xaml`：在 NavigationView 上方新增自定义标题栏 Grid（高度 48px），左侧应用图标 + "SweetPlayer" 标题，右侧留出空白给系统控制按钮
- [x] 6.2 修改 `MainWindow.xaml.cs`：设置 `this.ExtendsContentIntoTitleBar = true`，调用 `this.SetTitleBar(titleBarElement)` 设置拖拽区域
- [x] 6.3 修改 `PlayerWindow.ShowAsync()`：改为 `SetBorderAndTitleBar(hasBorder: true, hasTitleBar: true)`，设置 `_appWindow.TitleBar.ExtendsContentIntoTitleBar = true`
- [x] 6.4 修改 `PlayerWindowOverlay.xaml`：在 OverlayRoot 顶部新增自定义标题栏行（高度 48px），左侧应用图标（无标题文字），右侧留空
- [x] 6.5 修改 `PlayerWindow.cs`：通过 `_appWindow.TitleBar.SetDragRectangles()` 设置可拖拽区域（排除按钮区域）
- [x] 6.6 确认全屏模式下自定义标题栏行自动隐藏（随控件显隐逻辑同步）

## 7. 窗口尺寸约束

- [x] 7.1 修改 `App.xaml.cs` `OnLaunched`：创建 MainWindow 后通过 `AppWindow` 设置宽度 1750，通过 `OverlappedPresenter.IsResizable = false` 禁止调整大小，`IsMaximizable = false` 禁用最大化
- [x] 7.2 修改 `PlayerWindow.ShowAsync()`：将 `_appWindow.Resize(new SizeInt32(1920, 1130))` 改为动态计算 `_appWindow.Resize(new SizeInt32(1920, 1130 + titleBarHeight))`，通过 `_appWindow.TitleBar.Height` 获取标题栏实际高度

## 8. 验证

- [x] 8.1 构建项目确认编译通过
- [ ] 8.2 手动验证：设置页切换选项后重启应用，确认设置持久化
- [ ] 8.3 手动验证：配置全屏模式后播放视频，确认自动全屏启动
- [ ] 8.4 手动验证：播放视频时确认底部控制栏布局正确（三按钮居中、倍速在右侧）
- [ ] 8.5 手动验证：MainWindow 自定义标题栏仅显示右侧最小化/最大化/关闭按钮，可拖拽移动窗口，系统控制按钮正常工作
- [ ] 8.6 手动验证：PlayerWindow 自定义标题栏仅显示右侧最小化/最大化/关闭按钮，全屏时标题栏隐藏
- [ ] 8.7 手动验证：主窗口宽度固定 1750，无法拖拽调整大小，最大化按钮禁用
- [ ] 8.8 手动验证：播放窗口窗口模式下客户区为 1920×1130，视频无黑边
