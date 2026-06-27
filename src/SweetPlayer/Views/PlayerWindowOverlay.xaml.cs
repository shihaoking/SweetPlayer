using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SweetPlayer.Services;
using SweetPlayer.Services.Playback;
using SweetPlayer.ViewModels;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace SweetPlayer.Views;

/// <summary>
/// 播放器窗口覆盖层：从 PlayerPage 迁移的完整 UI 布局与交互逻辑。
/// </summary>
public sealed partial class PlayerWindowOverlay : UserControl
{
    private readonly IPlaybackControlService _playback;
    private readonly IKeyboardShortcutService _shortcuts;
    private readonly PlayerWindow _playerWindow;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _upNextTimer;
    private bool _controlsVisible = true;

    public PlayerViewModel ViewModel { get; }

    public PlayerWindowOverlay(PlayerWindow playerWindow, PlayerViewModel viewModel, IPlaybackControlService playback)
    {
        var sp = App.Services;
        _playback = playback;
        _shortcuts = sp.GetRequiredService<IKeyboardShortcutService>();
        _playerWindow = playerWindow;
        ViewModel = viewModel;

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
        ViewModel.ShowOsdHandler = (msg, glyph) => Osd.Show(msg, glyph);

        // 注册键盘快捷键
        _shortcuts.FullScreenToggleHandler = () =>
        {
            return _playerWindow.ToggleFullScreen();
        };
        _shortcuts.ExitPlayerHandler = () =>
        {
            DispatcherQueue.TryEnqueue(ExitPlayer);
        };
        _shortcuts.RegisterPlayerShortcuts(OverlayRoot, _playback);

        // 使用 handledEventsToo=true 注册指针事件，确保即使 Thumb 内部处理了事件仍能收到
        ProgressSlider.AddHandler(PointerPressedEvent,
            new PointerEventHandler(OnSliderPointerPressed), true);
        ProgressSlider.AddHandler(PointerReleasedEvent,
            new PointerEventHandler(OnSliderPointerReleased), true);
        ProgressSlider.AddHandler(PointerCanceledEvent,
            new PointerEventHandler(OnSliderPointerCanceled), true);
        ProgressSlider.AddHandler(PointerCaptureLostEvent,
            new PointerEventHandler(OnSliderPointerCaptureLost), true);

        OverlayRoot.Focus(FocusState.Programmatic);
        _upNextTimer.Start();

        UpdatePlayIcons();
        UpdateVolumeIcon();

        // 更新标题栏拖拽区域
        _playerWindow.UpdateTitleBarDragRects();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _hideTimer.Stop();
        _upNextTimer.Stop();
        _shortcuts.UnregisterShortcuts(OverlayRoot);

        // 移除事件处理器
        ProgressSlider.RemoveHandler(PointerPressedEvent,
            new PointerEventHandler(OnSliderPointerPressed));
        ProgressSlider.RemoveHandler(PointerReleasedEvent,
            new PointerEventHandler(OnSliderPointerReleased));
        ProgressSlider.RemoveHandler(PointerCanceledEvent,
            new PointerEventHandler(OnSliderPointerCanceled));
        ProgressSlider.RemoveHandler(PointerCaptureLostEvent,
            new PointerEventHandler(OnSliderPointerCaptureLost));

        ViewModel.DetachEvents();
        ViewModel.ShowOsdHandler = null;
    }

    // ---------- 自动隐藏控制 ----------

    private void OnHideTimerTick(object? sender, object e)
    {
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
        AnimateOpacity(PlayerTitleBar, 1.0, 200);
    }

    private void HideControls()
    {
        if (!_controlsVisible) return;
        _controlsVisible = false;
        AnimateOpacity(TopBar, 0.0, 300);
        AnimateOpacity(BottomBar, 0.0, 300);
        AnimateOpacity(CenterPlayButton, 0.0, 300);
        AnimateOpacity(PlayerTitleBar, 0.0, 300);
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
        if (e.OriginalSource is FrameworkElement fe && IsOverlayHit(fe))
            return;

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
            return;

        _playerWindow.ToggleFullScreen();
        FullScreenIcon.Glyph = _playerWindow.IsFullScreen ? "\uE73F" : "\uE740";
    }

    private static bool IsOverlayHit(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is Button or Slider or NumberBox or ComboBox or ListView or RadioButtons or Pivot or PivotItem)
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    // ---------- 进度条 ----------

    private bool _isDragging = false;

    private void OnProgressSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // 仅在指针事件（PointerPressed）已标记拖拽时，才同步 slider 值到 ViewModel
        // 不使用 diff 自动检测，避免首次位置跳变被误判为拖拽
        if (_isDragging || ViewModel.IsUserSeeking)
        {
            ViewModel.PositionSeconds = e.NewValue;
        }
    }

    /// <summary>提交 seek 前，始终从 ProgressSlider.Value 同步真实值到 ViewModel。
    /// 因为 ValueChanged 可能在 PointerPressed 之前触发，导致 PositionSeconds 未被更新。</summary>
    private void CommitSliderSeek()
    {
        ViewModel.PositionSeconds = ProgressSlider.Value;
        ViewModel.CommitSeekFromSlider();
    }

    private void OnProgressGridPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var isSliderHit = e.OriginalSource is Microsoft.UI.Xaml.Controls.Slider ||
            (e.OriginalSource is FrameworkElement fe && FindParent<Slider>(fe) is not null);

        if (isSliderHit)
        {
            ViewModel.IsUserSeeking = true;
            ViewModel.IsDraggingSlider = true;
            _isDragging = true;
        }
    }

    private void OnProgressGridPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsUserSeeking || _isDragging)
        {
            CommitSliderSeek();
            _isDragging = false;
            ViewModel.IsDraggingSlider = false;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T typed) return typed;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void OnSliderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Slider] PointerPressed: isDragging={_isDragging} → true");
        ViewModel.IsUserSeeking = true;
        ViewModel.IsDraggingSlider = true;
        _isDragging = true;
    }

    private void OnSliderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsUserSeeking || _isDragging)
        {
            System.Diagnostics.Debug.WriteLine($"[Slider] PointerReleased: committing seek to {ProgressSlider.Value:F1}s");
            CommitSliderSeek();
            _isDragging = false;
            ViewModel.IsDraggingSlider = false;
        }
    }

    private void OnSliderPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsUserSeeking || _isDragging)
        {
            System.Diagnostics.Debug.WriteLine($"[Slider] PointerCanceled: committing seek to {ProgressSlider.Value:F1}s");
            CommitSliderSeek();
            _isDragging = false;
            ViewModel.IsDraggingSlider = false;
        }
    }

    private void OnSliderPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsUserSeeking || _isDragging)
        {
            System.Diagnostics.Debug.WriteLine($"[Slider] PointerCaptureLost: committing seek to {ProgressSlider.Value:F1}s");
            CommitSliderSeek();
            _isDragging = false;
            ViewModel.IsDraggingSlider = false;
        }
    }

    private void OnSliderTapped(object sender, TappedRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Slider] Tapped: seeking to {ProgressSlider.Value:F1}s");
        CommitSliderSeek();
        _isDragging = false;
        ViewModel.IsDraggingSlider = false;
        e.Handled = true;
    }

    // ---------- 控制按钮 ----------

    private void OnBackClick(object sender, RoutedEventArgs e) => ExitPlayer();

    private void OnGearClick(object sender, RoutedEventArgs e) =>
        ViewModel.ToggleSettingsPaneCommand.Execute(null);

    private void OnCloseSettingsClick(object sender, RoutedEventArgs e) =>
        ViewModel.IsSettingsPaneOpen = false;

    private void OnSettingsMaskTapped(object sender, TappedRoutedEventArgs e)
    {
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

    private void OnFullScreenClick(object sender, RoutedEventArgs e)
    {
        _playerWindow.ToggleFullScreen();
        FullScreenIcon.Glyph = _playerWindow.IsFullScreen ? "\uE73F" : "\uE740";
    }

    private void OnSubtitleTrackClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TrackItem t)
            ViewModel.SelectSubtitleCommand.Execute(t);
    }

    private void OnAudioTrackClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TrackItem t)
            ViewModel.SelectAudioCommand.Execute(t);
    }

    private void OnChapterClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChapterItem c)
            ViewModel.JumpToChapterCommand.Execute(c);
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

            if (_playerWindow.Handle != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(picker, _playerWindow.Handle);
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
        ViewModel.ShowOsd("正在搜索在线字幕...", "\uE721");
    }

    // ---------- 退出 ----------

    private void ExitPlayer()
    {
        if (_playerWindow.IsFullScreen) _playerWindow.ToggleFullScreen();
        _playerWindow.Close();
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
        var glyph = _playback.IsPlaying ? "\uE769" : "\uE768";
        if (CenterPlayIcon is not null) CenterPlayIcon.Glyph = glyph;
        if (BottomPlayIcon is not null) BottomPlayIcon.Glyph = glyph;
    }

    private void UpdateVolumeIcon()
    {
        if (VolumeIcon is null) return;
        if (ViewModel.IsMuted || ViewModel.Volume <= 0.5)
        {
            VolumeIcon.Glyph = "\uE74F";
        }
        else if (ViewModel.IsVolumeBoosted)
        {
            VolumeIcon.Glyph = "\uE995";
        }
        else
        {
            VolumeIcon.Glyph = "\uE767";
        }
    }
}
