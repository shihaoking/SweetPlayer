namespace SweetPlayer.Core.Models;

/// <summary>
/// 字幕显示尺寸预设。
/// </summary>
public enum SubtitleSize
{
    /// <summary>小号字体（mpv sub-font-size ≈ 36）。</summary>
    Small,
    /// <summary>中号字体（≈ 48，默认）。</summary>
    Medium,
    /// <summary>大号字体（≈ 60）。</summary>
    Large,
    /// <summary>超大字体（≈ 72）。</summary>
    ExtraLarge,
}

/// <summary>
/// 字幕颜色预设。
/// </summary>
public enum SubtitleColor
{
    /// <summary>白色（默认）。</summary>
    White,
    /// <summary>黄色。</summary>
    Yellow,
}

/// <summary>
/// 字幕显示位置预设。
/// </summary>
public enum SubtitlePosition
{
    /// <summary>显示于画面底部（默认）。</summary>
    Bottom,
    /// <summary>显示于画面顶部。</summary>
    Top,
}

/// <summary>
/// 字幕显示设置（持久化为 JSON）。
/// </summary>
public class SubtitleSettings
{
    /// <summary>字体大小预设。</summary>
    public SubtitleSize Size { get; set; } = SubtitleSize.Medium;

    /// <summary>字幕颜色预设。</summary>
    public SubtitleColor Color { get; set; } = SubtitleColor.White;

    /// <summary>字幕位置预设。</summary>
    public SubtitlePosition Position { get; set; } = SubtitlePosition.Bottom;

    /// <summary>字幕延迟（秒），范围 -5.0 到 +5.0。</summary>
    public double DelaySeconds { get; set; } = 0;
}
