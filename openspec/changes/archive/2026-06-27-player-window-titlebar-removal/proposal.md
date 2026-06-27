## Why

视频播放窗口在窗口模式下，视频左右距离窗口边框存在黑边。根因是 Windows 标题栏占据了窗口顶部空间（约 32px），导致客户区（client area）宽高比不等于窗口外部尺寸的 16:9，mpv 默认的 `keepaspect=yes` 在实际客户区比例下产生了 pillarbox（左右黑边）。

之前尝试过两种方案均不理想：
1. `keepaspect=no`：消除黑边但导致视频拉伸变形
2. 动态约束窗口宽高比（宽度驱动高度）：在初始化时视频比例未知，且用户拖拽体验不佳

最终方案：**隐藏 Windows 标题栏**，使客户区 = 窗口完整区域（1920×1080 精确 16:9），同时将视频标题做成覆盖层的悬浮 TopBar，与底部播放控件同步显示/隐藏。

## What Changes

- `PlayerWindow.cs`：在 `ShowAsync()` 中通过 `OverlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)` 隐藏 Windows 标题栏
- 覆盖层 `PlayerWindowOverlay.xaml` 中的 TopBar（含返回按钮、视频标题、设置按钮）作为悬浮层承担原标题栏的功能
- TopBar 与 BottomBar 通过 `ShowControlsImmediate()` / `HideControls()` 同步淡入淡出（已有实现，无需改动）

## Capabilities

### New Capabilities
- `borderless-player-window`：无标题栏播放窗口，视频铺满整个窗口区域，消除因标题栏占位导致的黑边

### Modified Capabilities
- `player-overlay-topbar`：覆盖层 TopBar 承担窗口标题功能（返回、标题显示、设置入口），替代 Windows 原生标题栏

## Impact

- **PlayerWindow.cs**: 添加 `OverlappedPresenter.SetBorderAndTitleBar` 调用（1 行核心代码）
- **窗口交互变化**: 用户无法通过标题栏拖动窗口、双击最大化；关闭/全屏通过覆盖层按钮或快捷键（Esc 关闭、F 全屏、双击切换全屏）
- **窗口边框保留**: `hasBorder: true` 确保用户仍可拖拽窗口边缘调整大小
