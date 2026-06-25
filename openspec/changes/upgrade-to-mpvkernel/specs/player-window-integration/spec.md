## ADDED Requirements

### Requirement: PlayerWindow 独立播放窗口
系统 SHALL 创建独立的 AppWindow 作为视频播放窗口，通过 DesktopWindowXamlSource 托管 XAML UI 覆盖层。

#### Scenario: 创建独立播放窗口
- **WHEN** PlayerWindow 被实例化
- **THEN** 系统创建 `AppWindow` 实例
- **THEN** 系统创建 `DesktopWindowXamlSource` 并关联到 AppWindow
- **THEN** 系统获取窗口 HWND 供 mpv wid 模式使用

#### Scenario: 窗口生命周期管理
- **WHEN** `Show()` 被调用
- **THEN** 系统调用 `_topWindow.Show()` 显示窗口
- **THEN** 系统调用 `InitializeAsync(hwnd)` 初始化 mpv 客户端

#### Scenario: 窗口关闭触发清理
- **WHEN** 用户关闭窗口
- **THEN** 系统触发 `Closed` 事件通知外部
- **THEN** 系统停止播放并释放窗口资源
- **THEN** 系统不销毁 MpvClient（由 DI 管理 Singleton）

#### Scenario: 窗口尺寸变化同步
- **WHEN** AppWindow 尺寸发生变化
- **THEN** 系统更新 DesktopWindowXamlSource 的位置和大小
- **THEN** mpv 自动适配新尺寸（wid 模式下 mpv 内部处理）

### Requirement: PlayerWindow 全屏和小窗模式
系统 SHALL 支持全屏和小窗置顶两种窗口模式。

#### Scenario: 切换全屏模式
- **WHEN** 用户请求全屏切换
- **THEN** 系统通过 AppWindow.Presenter 在 FullScreen 和 CompactOverlay/Overlapped 之间切换
- **THEN** 系统调用 `_client.SetFullScreenStateAsync(isFullScreen)` 同步 mpv 状态

#### Scenario: 切换小窗置顶模式
- **WHEN** 用户请求小窗模式
- **THEN** 系统调用 `_client.SetCompactOverlayStateAsync(true)` 设置置顶
- **THEN** 系统调整窗口尺寸为小窗大小

### Requirement: PlayerPage 桥接页
系统 SHALL 将 PlayerPage 简化为窗口启动桥接页，接收导航参数后打开 PlayerWindow。

#### Scenario: 导航到 PlayerPage 时打开播放窗口
- **WHEN** 应用导航到 PlayerPage 并接收到视频参数
- **THEN** 系统从 DI 容器获取 PlayerWindow 实例
- **THEN** 系统调用 `PlayerWindow.Show()` 显示独立窗口
- **THEN** 系统调用 `PlaybackControlService.PlayVideoAsync()` 开始播放
- **THEN** PlayerPage 显示为全黑背景

#### Scenario: 播放窗口关闭时导航返回
- **WHEN** PlayerWindow 触发 Closed 事件
- **THEN** 系统调用 `_navigation.GoBack()` 返回上一页

### Requirement: PlayerWindowOverlay UI 覆盖层
系统 SHALL 在 PlayerWindow 中创建完整的 UI 覆盖层，迁移自 PlayerPage.xaml 的所有控件。

#### Scenario: 覆盖层结构
- **WHEN** PlayerWindowOverlay 被加载
- **THEN** 系统渲染顶部信息栏（渐变背景 + 返回按钮 + 标题 + 设置按钮）
- **THEN** 系统渲染中央播放按钮
- **THEN** 系统渲染底部控制栏（进度条行 + 按钮行）
- **THEN** 系统渲染设置侧边面板（字幕/音轨/画面比例/倍速 4 个标签页）
- **THEN** 系统渲染 OSD 通知控件
- **THEN** 系统渲染 Up Next 覆盖层

#### Scenario: 控件样式保持一致
- **WHEN** PlayerWindowOverlay 渲染控件
- **THEN** 系统复用现有样式（OverlayCapsuleStyle、OverlayIconButtonStyle、GhostButtonStyle 等）
- **THEN** 按钮图标使用 Segoe MDL2 Glyph
- **THEN** 颜色方案保持一致（#B30A0A10 背景、#FFF6F4EE 前景、#FFE9C46A 强调色）

#### Scenario: 自动隐藏和显示行为
- **WHEN** 播放期间 3 秒无鼠标/键盘操作
- **THEN** 所有覆盖栏和光标平滑淡出
- **WHEN** 用户移动鼠标或按键
- **THEN** 所有覆盖栏和光标平滑淡入

#### Scenario: 进度条交互
- **WHEN** 用户拖动进度条 Slider
- **THEN** 系统暂停播放并显示时间预览
- **THEN** 用户释放后跳转到目标位置并恢复播放

### Requirement: 键盘快捷键迁移
系统 SHALL 将键盘快捷键注册目标从 PlayerPage 的根 Grid 迁移到 PlayerWindowOverlay 的根 Grid。

#### Scenario: 快捷键在独立窗口中生效
- **WHEN** PlayerWindow 获得焦点
- **THEN** Space 键切换播放/暂停
- **THEN** Left/Right 键快退/快进 10 秒
- **THEN** Up/Down 键增减音量
- **THEN** M 键切换静音
- **THEN** F/F11 键切换全屏
- **THEN** Esc 键退出全屏或关闭窗口
- **THEN** Ctrl+Left/Right 跳转上/下一章节

#### Scenario: 快捷键注册目标变更
- **WHEN** KeyboardShortcutService 初始化
- **THEN** 快捷键监听目标为 PlayerWindowOverlay 的根 Grid
- **THEN** 不再监听 PlayerPage 的 PlayerRoot Grid

### Requirement: PlayerViewModel 生命周期适配
系统 SHALL 适配 PlayerViewModel 以支持独立窗口生命周期。

#### Scenario: ViewModel 初始化增加窗口引用
- **WHEN** PlayerViewModel.Initialize() 被调用
- **THEN** 系统传入 PlayerWindow 引用（可选）
- **THEN** ViewModel 的命令和绑定逻辑保持不变

#### Scenario: 窗口关闭时清理事件
- **WHEN** PlayerWindow 关闭
- **THEN** 系统调用 `DetachEvents()` 取消事件订阅
- **THEN** 系统保存播放进度
