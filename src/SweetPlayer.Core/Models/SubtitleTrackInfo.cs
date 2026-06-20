namespace SweetPlayer.Core.Models;

/// <summary>
/// 描述播放器内可用的一条字幕轨道（内嵌、外挂或在线）。
/// </summary>
public class SubtitleTrackInfo
{
    /// <summary>mpv 中的字幕轨道编号（sid）。0 表示关闭。</summary>
    public int TrackId { get; set; }

    /// <summary>轨道显示名称。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>轨道语言（如 chs/cht/eng/jpn）。</summary>
    public string? Language { get; set; }

    /// <summary>轨道来源类型。</summary>
    public SubtitleSource Source { get; set; }

    /// <summary>是否为当前选中的轨道。</summary>
    public bool IsActive { get; set; }
}
