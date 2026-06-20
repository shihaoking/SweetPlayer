using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.Services.Playback;
using SweetPlayer.ViewModels;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;

namespace SweetPlayer.Views;

/// <summary>
/// 播放器页面：覆盖式 Overlay 布局 + 自动隐藏 + 全屏切换 + 键盘快捷键。
/// </summary>
public sealed partial class PlayerPage : Page
{
    private readonly IPlaybackControlService _playback;
    private readonly IKeyboardShortcutService _shortcuts;
    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _upNextTimer;
    private bool _controlsVisible = true;
    private bool _isFullScreen;
    private bool _isAttached;

    public PlayerViewModel ViewModel { get; }

    public PlayerPage()
    {
        var sp = App.Services;
        _playback = sp.GetRequiredService<IPlaybackControlService>();
        _shortcuts = sp.GetRequiredService<IKeyboardShortcutService>();
        _navigation = sp.GetRequiredService<INavigationService>();
        ViewModel = sp.GetRequiredService<PlayerViewModel>();

        InitializeComponent();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hideTimer.Tick += OnHideTimerTick;

        _upNextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _upNextTimer.Tick += OnUpNextTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ---------- 生命周期 ----------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 关联渲染
        VideoPlayer.AttachPlayer(_playback.MpvPlayer);

        // 初始 Speed 与 Volume 同步给 mpv（只赋值显示用，不重复触发）
        ViewModel.ShowOsdHandler = (msg, glyph) => Osd.Show(msg, glyph);

        // 注册键盘快捷键
        _shortcuts.FullScreenToggleHandler = () =>
        {
            ToggleFullScreen();
            return _isFullScreen;
        };
        _shortcuts.ExitPlayerHandler = () =>
        {
            DispatcherQueue.TryEnqueue(ExitPlayer);
        };
        _shortcuts.RegisterPlayerShortcuts(PlayerRoot, _playback);
        _isAttached = true;

        PlayerRoot.Focus(FocusState.Programmatic);

        _hideTimer.Start();
        _upNextTimer.Start();

        UpdatePlayIcons();
        UpdateVolumeIcon();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hideTimer.Stop();
        _upNextTimer.Stop();
        if (_isAttached)
        {
            _shortcuts.UnregisterShortcuts(PlayerRoot);
            _isAttached = false;
        }
        ViewModel.DetachEvents();
        ViewModel.ShowOsdHandler = null;
        ShowMouseCursor();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is VideoFile vf)
        {
            // 上层调用方决定是否在导航前已经调用 PlayVideoAsync。
            // 这里仅刷新 ViewModel 标题/时长。
            ViewModel.Initialize(vf);
        }
        else
        {
            ViewModel.Initialize(_playback.CurrentVideo);
        }
        ViewModel.LoadChaptersFromMpv();
    }

    // ---------- 自动隐藏控制 ----------

    private void OnHideTimerTick(object? sender, object e)
    {
        // 设置面板打开时不隐藏
        if (ViewModel.IsSettingsPaneOpen)
        {
            ResetHideTimer();
            return;
        }
        HideControls();
    }

    private void ResetHideTimer()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void ShowControlsImmediate()
    {
        if (_controlsVisible) return;
        _controlsVisible = true;
        AnimateOpacity(TopBar, 1.0, 200);
        AnimateOpacity(BottomBar, 1.0, 200);
        AnimateOpacity(CenterPlayButton, 1.0, 200);
        ShowMouseCursor();
    }

    private void HideControls()
    {
        if (!_controlsVisible) return;
        _controlsVisible = false;
        AnimateOpacity(TopBar, 0.0, 300);
        AnimateOpacity(BottomBar, 0.0, 300);
        AnimateOpacity(CenterPlayButton, 0.0, 300);
        HideMouseCursor();
    }

    private static void AnimateOpacity(UIElement target, double to, int durationMs)
    {
        var anim = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }

    private void ShowMouseCursor()
    {
        try
        {
            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        }
        catch { /* ignore */ }
    }

    private void HideMouseCursor()
    {
        try
        {
            // WinUI 3 中通过 ProtectedCursor 设为 null 隐藏；某些环境可能不支持。
            ProtectedCursor = null;
        }
        catch { /* ignore */ }
    }

    // ---------- 输入事件 ----------

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        ShowControlsImmediate();
        ResetHideTimer();
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        ShowControlsImmediate();
        ResetHideTimer();

        // 章节快捷键 (Ctrl+Left / Ctrl+Right)
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (ctrl)
        {
            switch (e.Key)
            {
                case VirtualKey.Left:
                    ViewModel.PrevChapterCommand.Execute(null);
                    e.Handled = true;
                    break;
                case VirtualKey.Right:
                    ViewModel.NextChapterCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void OnRootTapped(object sender, TappedRoutedEventArgs e)
    {
        // 单击视频区域：切换控制层可见性
        if (e.OriginalSource is FrameworkElement fe && IsOverlayHit(fe))
        {
            return;
        }
        if (_controlsVisible)
        {
            HideControls();
            _hideTimer.Stop();
        }
        else
        {
            ShowControlsImmediate();
            ResetHideTimer();
        }
    }

    private void OnRootDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && IsOverlayHit(fe))
        {
            return;
        }
        ToggleFullScreen();
    }

    private static bool IsOverlayHit(DependencyObject element)
    {
        // 沿 Visual 树向上查找，命中按钮/滑块/边框等控件即视为 overlay 命中
        var current = element;
        while (current is not null)
        {
            if (current is Button or Slider or NumberBox or ComboBox or ListView or RadioButtons or Pivot or PivotItem)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    // ---------- 进度条 ----------

    private void OnSliderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.IsUserSeeking = true;
    }

    private void OnSliderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsUserSeeking)
        {
            ViewModel.CommitSeekFromSlider();
        }
    }

    // ---------- 控制按钮 ----------

    private void OnBackClick(object sender, RoutedEventArgs e) => ExitPlayer();

    private void OnGearClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSettingsPaneCommand.Execute(null);
    }

    private void OnCloseSettingsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsSettingsPaneOpen = false;
    }

    private void OnSettingsMaskTapped(object sender, TappedRoutedEventArgs e)
    {
        // 点击遮罩区域关闭设置面板
        ViewModel.IsSettingsPaneOpen = false;
        e.Handled = true;
    }

    private void OnTogglePlayPauseClick(object sender, RoutedEventArgs e)
    {
        ViewModel.TogglePlayPauseCommand.Execute(null);
        UpdatePlayIcons();
    }

    private void OnSeekForwardClick(object sender, RoutedEventArgs e) =>
        ViewModel.SeekForwardCommand.Execute(null);

    private void OnSeekBackwardClick(object sender, RoutedEventArgs e) =>
        ViewModel.SeekBackwardCommand.Execute(null);

    private void OnMuteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleMuteCommand.Execute(null);
        UpdateVolumeIcon();
    }

    private void OnSpeedItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string raw &&
            double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
        {
            var option = ViewModel.Speeds.FirstOrDefault(s => Math.Abs(s.Value - v) < 0.001);
            if (option is not null)
            {
                ViewModel.SelectSpeedCommand.Execute(option);
            }
        }
    }

    private void OnSubtitleClick(object sender, RoutedEventArgs e) =>
        ViewModel.OpenSettingsTabCommand.Execute(2);

    private void OnAudioClick(object sender, RoutedEventArgs e) =>
        ViewModel.OpenSettingsTabCommand.Execute(1);

    private void OnFullScreenClick(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void OnSubtitleTrackClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TrackItem t)
        {
            ViewModel.SelectSubtitleCommand.Execute(t);
        }
    }

    private void OnAudioTrackClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TrackItem t)
        {
            ViewModel.SelectAudioCommand.Execute(t);
        }
    }

    private void OnChapterClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChapterItem c)
        {
            ViewModel.JumpToChapterCommand.Execute(c);
        }
    }

    private async void OnAddLocalSubtitleClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".srt");
            picker.FileTypeFilter.Add(".ass");
            picker.FileTypeFilter.Add(".ssa");
            picker.FileTypeFilter.Add(".sub");

            var window = MainWindowAccessor.Current;
            if (window is not null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                _playback.MpvPlayer.LoadExternalSubtitle(file.Path);
                ViewModel.SubtitleTracks.Add(new TrackItem
                {
                    TrackId = ViewModel.SubtitleTracks.Count + 1,
                    Title = Path.GetFileName(file.Path)
                });
                ViewModel.ShowOsd($"已加载字幕: {Path.GetFileName(file.Path)}", "\uED1E");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddLocalSubtitle 失败: {ex.Message}");
        }
    }

    private void OnSearchOnlineSubtitleClick(object sender, RoutedEventArgs e)
    {
        // 在线字幕搜索由字幕模块（任务 6）提供，此处仅提示。
        ViewModel.ShowOsd("正在搜索在线字幕...", "\uE721");
    }

    // ---------- 全屏 ----------

    private void ToggleFullScreen()
    {
        var window = MainWindowAccessor.Current;
        if (window is null) return;

        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (_isFullScreen)
            {
                appWindow.SetPresenter(AppWindowPresenterKind.Default);
                _isFullScreen = false;
            }
            else
            {
                appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                _isFullScreen = true;
            }
            FullScreenIcon.Glyph = _isFullScreen ? "\uE73F" : "\uE740";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleFullScreen 失败: {ex.Message}");
        }
    }

    private void ExitPlayer()
    {
        if (_isFullScreen) ToggleFullScreen();
        _playback.Stop();
        if (_navigation.CanGoBack)
        {
            _navigation.GoBack();
        }
        else
        {
            _navigation.NavigateTo(typeof(HomePage));
        }
    }

    // ---------- Up Next & 图标更新 ----------

    private void OnUpNextTick(object? sender, object e)
    {
        UpdatePlayIcons();
        UpdateVolumeIcon();

        if (ViewModel.UpNextVisible)
        {
            ViewModel.UpNextCountdown = Math.Max(0, ViewModel.UpNextCountdown - 1);
            if (ViewModel.UpNextCountdown <= 0)
            {
                ViewModel.PlayNextEpisodeCommand.Execute(null);
            }
        }
    }

    private void UpdatePlayIcons()
    {
        var glyph = _playback.IsPlaying ? "\uE769" : "\uE768"; // pause / play
        if (CenterPlayIcon is not null) CenterPlayIcon.Glyph = glyph;
        if (BottomPlayIcon is not null) BottomPlayIcon.Glyph = glyph;
    }

    private void UpdateVolumeIcon()
    {
        if (VolumeIcon is null) return;
        if (ViewModel.IsMuted || ViewModel.Volume <= 0.5)
        {
            VolumeIcon.Glyph = "\uE74F"; // Mute
        }
        else if (ViewModel.IsVolumeBoosted)
        {
            VolumeIcon.Glyph = "\uE995"; // Boost (loud)
        }
        else
        {
            VolumeIcon.Glyph = "\uE767"; // Volume
        }
    }
}
