using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace SweetPlayer.Controls;

/// <summary>
/// 屏幕显示通知（Volume / Speed / Seek / Track）。位于屏幕中上方，
/// 显示 1.5 秒后通过淡出动画消失。
/// </summary>
public sealed partial class OsdNotification : UserControl
{
    private readonly DispatcherTimer _hideTimer;
    private Storyboard? _activeStoryboard;

    public OsdNotification()
    {
        InitializeComponent();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            FadeOut();
        };
    }

    /// <summary>
    /// 显示一条 OSD 通知。
    /// </summary>
    /// <param name="message">主体文本，如 "75%" / "1.5x" / "+10s" / "简体中文"。</param>
    /// <param name="glyph">Segoe Fluent Icons 字形 (PUA Glyph)。</param>
    public void Show(string message, string glyph = "\uE767")
    {
        OsdText.Text = message ?? string.Empty;
        OsdIcon.Glyph = string.IsNullOrEmpty(glyph) ? "\uE767" : glyph;

        _hideTimer.Stop();
        _activeStoryboard?.Stop();

        var fadeIn = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, Capsule);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var slide = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, CapsuleTranslate);
        Storyboard.SetTargetProperty(slide, "Y");

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Children.Add(slide);
        _activeStoryboard = sb;
        sb.Begin();

        _hideTimer.Start();
    }

    private void FadeOut()
    {
        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(260)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(fadeOut, Capsule);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");

        var slide = new DoubleAnimation
        {
            To = -8,
            Duration = new Duration(TimeSpan.FromMilliseconds(260)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slide, CapsuleTranslate);
        Storyboard.SetTargetProperty(slide, "Y");

        var sb = new Storyboard();
        sb.Children.Add(fadeOut);
        sb.Children.Add(slide);
        _activeStoryboard = sb;
        sb.Begin();
    }
}
