using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SweetPlayer.Services.Playback;
using Windows.System;

namespace SweetPlayer.Services;

/// <summary>
/// 播放器键盘快捷键绑定服务。
/// </summary>
public interface IKeyboardShortcutService
{
    /// <summary>请求切换全屏的回调（由调用方提供窗口级别实现）。</summary>
    Func<bool>? FullScreenToggleHandler { get; set; }

    /// <summary>请求退出播放器的回调。</summary>
    Action? ExitPlayerHandler { get; set; }

    /// <summary>为播放器界面注册标准快捷键集合。</summary>
    /// <param name="target">承载 KeyDown 事件的 UI 元素（通常是播放页根元素）。</param>
    /// <param name="playbackService">需要被快捷键操作的播放服务。</param>
    void RegisterPlayerShortcuts(UIElement target, IPlaybackControlService playbackService);

    /// <summary>取消已注册的快捷键。</summary>
    void UnregisterShortcuts(UIElement target);
}

/// <summary>
/// 默认键盘快捷键绑定实现：
/// Space 播放/暂停、左右方向键 ±10s、上下方向键 ±5% 音量、F 全屏、Esc 退出全屏、
/// M 静音、S/A 切换字幕/音轨、[/] 减/加速。
/// </summary>
public class KeyboardShortcutService : IKeyboardShortcutService
{
    private const double VolumeStep = 5;
    private const int SeekStepSeconds = 10;
    private const double MinSpeed = 0.5;
    private const double MaxSpeed = 2.0;
    private const double SpeedStep = 0.25;

    private readonly Dictionary<UIElement, KeyEventHandler> _handlers = new();
    private bool _muted;
    private double _lastVolume = 100;
    private bool _isFullScreen;
    private double _currentVolume = 100;
    private double _currentSpeed = 1.0;
    private int _currentSubtitleTrack;
    private int _currentAudioTrack = 1;

    /// <summary>请求切换全屏的回调（由调用方提供窗口级别实现）。</summary>
    public Func<bool>? FullScreenToggleHandler { get; set; }

    /// <summary>请求退出播放器的回调。</summary>
    public Action? ExitPlayerHandler { get; set; }

    public void RegisterPlayerShortcuts(UIElement target, IPlaybackControlService playbackService)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(playbackService);

        if (_handlers.ContainsKey(target))
        {
            UnregisterShortcuts(target);
        }

        KeyEventHandler handler = (sender, e) => OnKeyDown(e, playbackService);
        target.KeyDown += handler;
        _handlers[target] = handler;
    }

    public void UnregisterShortcuts(UIElement target)
    {
        if (target is null) return;
        if (_handlers.Remove(target, out var handler))
        {
            target.KeyDown -= handler;
        }
    }

    private void OnKeyDown(KeyRoutedEventArgs e, IPlaybackControlService svc)
    {
        switch (e.Key)
        {
            case VirtualKey.Space:
                svc.TogglePlayPause();
                e.Handled = true;
                break;
            case VirtualKey.Left:
                svc.SeekBackward(SeekStepSeconds);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                svc.SeekForward(SeekStepSeconds);
                e.Handled = true;
                break;
            case VirtualKey.Up:
                _currentVolume = Math.Clamp(_currentVolume + VolumeStep, 0, 100);
                _muted = false;
                svc.SetVolume(_currentVolume);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                _currentVolume = Math.Clamp(_currentVolume - VolumeStep, 0, 100);
                _muted = false;
                svc.SetVolume(_currentVolume);
                e.Handled = true;
                break;
            case VirtualKey.F:
                if (FullScreenToggleHandler is not null)
                {
                    _isFullScreen = FullScreenToggleHandler.Invoke();
                }
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                if (_isFullScreen && FullScreenToggleHandler is not null)
                {
                    _isFullScreen = FullScreenToggleHandler.Invoke();
                }
                else
                {
                    ExitPlayerHandler?.Invoke();
                }
                e.Handled = true;
                break;
            case VirtualKey.M:
                if (_muted)
                {
                    _muted = false;
                    svc.SetVolume(_lastVolume);
                    _currentVolume = _lastVolume;
                }
                else
                {
                    _muted = true;
                    _lastVolume = _currentVolume;
                    svc.SetVolume(0);
                }
                e.Handled = true;
                break;
            case VirtualKey.S:
                _currentSubtitleTrack++;
                if (_currentSubtitleTrack > 5) _currentSubtitleTrack = 0;
                svc.MpvPlayer.SetSubtitleTrack(_currentSubtitleTrack);
                e.Handled = true;
                break;
            case VirtualKey.A:
                _currentAudioTrack++;
                if (_currentAudioTrack > 5) _currentAudioTrack = 1;
                svc.MpvPlayer.SetAudioTrack(_currentAudioTrack);
                e.Handled = true;
                break;
            // [
            case (VirtualKey)219:
                _currentSpeed = Math.Max(MinSpeed, Math.Round(_currentSpeed - SpeedStep, 2));
                svc.SetSpeed(_currentSpeed);
                e.Handled = true;
                break;
            // ]
            case (VirtualKey)221:
                _currentSpeed = Math.Min(MaxSpeed, Math.Round(_currentSpeed + SpeedStep, 2));
                svc.SetSpeed(_currentSpeed);
                e.Handled = true;
                break;
        }
    }
}
