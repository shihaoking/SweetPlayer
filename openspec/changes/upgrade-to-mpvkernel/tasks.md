## 1. 依赖和项目配置

- [x] 1.1 修改 `SweetPlayer.Services.csproj`：移除 `LibMpv.Client`、`SharpDX.Direct3D11`、`SharpDX.DXGI` PackageReference，添加 `Richasy.MpvKernel.Interop` 和 `Richasy.MpvKernel.Core`，TargetFramework 升级为 `net8.0-windows10.0.22621.0`
- [x] 1.2 修改 `SweetPlayer.csproj`：添加 `Richasy.MpvKernel.WinUI` PackageReference
- [x] 1.3 修改 `App.xaml.cs`：应用启动时调用 `MpvNative.Initialize("libmpv-2.dll")` 初始化 DLL 加载路径
- [x] 1.4 修改 `App.xaml.cs` DI 注册：新增 `services.AddTransient<PlayerWindow>()`

## 2. 接口和服务层重写

- [x] 2.1 重写 `IMpvPlayerService.cs`：移除 `InitializeRenderer`/`IsRendererReady`/`WaitForRendererReadyAsync`/`Resize`/`DisposeRenderer`，新增 `InitializeAsync(IntPtr windowHandle)` 和 `SetDecodeOptionsAsync(DecodeMode mode)`
- [x] 2.2 完全重写 `MpvPlayerService.cs`：使用 MpvClient + MpvNative 实现所有播放控制和配置方法（~250 行），包含 InitializeAsync、PlayAsync、PauseAsync、SeekAsync、SetSubtitleTrack、SetAudioTrack、SetAspectRatio、SetAudioBoost、LoadExternalSubtitle 等
- [x] 2.3 适配 `PlaybackControlService.cs`：移除 `WaitForRendererReadyAsync()`、`DisposeRenderer()`、`Resize()` 调用，保留进度持久化和 HDR 管理逻辑

## 3. 独立播放窗口

- [x] 3.1 创建 `src/SweetPlayer/Views/PlayerWindow.cs`：实现 AppWindow + DesktopWindowXamlSource 窗口管理，包含 Show/Hide/Close、全屏/小窗切换、HWND 获取、窗口尺寸变化同步、Closed 事件
- [x] 3.2 创建 `src/SweetPlayer/Views/PlayerWindowOverlay.xaml`：从 PlayerPage.xaml 迁移完整 UI 布局（顶部信息栏、中央播放按钮、底部控制栏含进度条和按钮行、设置侧边面板 4 个标签页、OSD 通知、Up Next 覆盖层）
- [x] 3.3 创建 `src/SweetPlayer/Views/PlayerWindowOverlay.xaml.cs`：实现控件自动隐藏/显示（3 秒超时）、光标显隐控制、进度条拖拽 seek 逻辑、双击全屏切换

## 4. PlayerPage 和 ViewModel 适配

- [x] 4.1 简化 `PlayerPage.xaml`：移除所有播放 UI 控件，保留全黑背景 Grid
- [x] 4.2 简化 `PlayerPage.xaml.cs`：OnNavigatedTo 接收视频参数后打开 PlayerWindow 并开始播放，监听 Closed 事件后 GoBack()
- [x] 4.3 适配 `PlayerViewModel.cs`：Initialize() 方法增加 PlayerWindow 引用，DetachEvents() 适配窗口关闭场景，全屏状态同步到 PlayerWindow.AppWindow

## 5. 键盘快捷键迁移

- [x] 5.1 修改 `KeyboardShortcutService.cs`：快捷键注册目标从 PlayerPage 的 PlayerRoot Grid 改为 PlayerWindowOverlay 的根 Grid，保持所有快捷键映射不变

## 6. 清理

- [x] 6.1 删除 `src/SweetPlayer.Services/Playback/ISwapChainPanelNative.cs`
- [x] 6.2 删除 `src/SweetPlayer.Services/Playback/MpvInterop.cs`
- [x] 6.3 删除 `src/SweetPlayer/Controls/MpvPlayerControl.xaml` 和 `src/SweetPlayer/Controls/MpvPlayerControl.xaml.cs`
- [x] 6.4 从 `SweetPlayer.csproj` 中移除 MpvPlayerControl 的 Page 编译项引用

## 7. 编译和验证

- [x] 7.1 执行 `dotnet build SweetPlayer.sln` 确保编译通过，修复所有编译错误
- [ ] 7.2 运行应用验证：播放视频弹出独立窗口、画面正常渲染、顶部/底部栏布局正确
- [ ] 7.3 验证配置功能：设置面板 4 个 Tab 切换、字幕轨道/字号/颜色/延迟、音轨切换、画面比例、倍速、音量（含增强）
- [ ] 7.4 验证交互功能：进度条拖动 seek、键盘快捷键全部有效、全屏/退出全屏、窗口关闭自动导航返回、进度保存
