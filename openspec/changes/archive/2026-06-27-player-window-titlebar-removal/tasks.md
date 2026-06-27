## 1. 隐藏 Windows 标题栏

- [x] 1.1 在 `PlayerWindow.ShowAsync()` 中，`AppWindow.Create()` 后获取 `OverlappedPresenter`
- [x] 1.2 调用 `presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)`
- [x] 1.3 验证编译通过

## 2. 验证覆盖层 TopBar 悬浮行为

- [x] 2.1 确认 TopBar 已设置 `VerticalAlignment="Top"` + 渐变背景（悬浮在视频之上）
- [x] 2.2 确认 `ShowControlsImmediate()` 同时动画 TopBar、BottomBar、CenterPlayButton
- [x] 2.3 确认 `HideControls()` 同时淡出 TopBar、BottomBar、CenterPlayButton
- [x] 2.4 确认鼠标移动触发 `ShowControlsImmediate()` + 重置 3 秒隐藏计时器

## 3. 撤销之前的无效方案代码

- [x] 3.1 移除 `MpvPlayerService.GetVideoAspectRatio()` 方法
- [x] 3.2 移除 `IMpvPlayerService.GetVideoAspectRatio()` 接口声明
- [x] 3.3 移除 `PlayerWindow.ConstrainWindowToVideoAspect()` 方法
- [x] 3.4 移除 `PlayerWindow.AdjustToVideoAspectRatio()` 方法
- [x] 3.5 移除 `PlayerPage.xaml.cs` 中对 `AdjustToVideoAspectRatio()` 的调用
- [x] 3.6 移除 `_isResizing` 和 `_lastClientWidth` 字段
