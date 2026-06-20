namespace SweetPlayer.Core.Models;

/// <summary>
/// 字幕文件格式枚举。
/// </summary>
public enum SubtitleFormat
{
    /// <summary>SubRip 字幕。</summary>
    SRT,
    /// <summary>Advanced SubStation Alpha。</summary>
    ASS,
    /// <summary>SubStation Alpha。</summary>
    SSA,
    /// <summary>SUB（MicroDVD/SubViewer 等）。</summary>
    SUB,
    /// <summary>VobSub 索引文件。</summary>
    IDX,
    /// <summary>WebVTT 字幕。</summary>
    VTT,
    /// <summary>未知或不支持的格式。</summary>
    Unknown,
}

/// <summary>
/// 字幕来源类型。
/// </summary>
public enum SubtitleSource
{
    /// <summary>视频文件内嵌字幕轨道。</summary>
    Embedded,
    /// <summary>本地外挂字幕文件。</summary>
    Local,
    /// <summary>在线下载的字幕文件。</summary>
    Online,
}

/// <summary>
/// 描述一个字幕文件（本地发现或在线下载后的本地副本）。
/// </summary>
public class SubtitleFileInfo
{
    /// <summary>字幕文件的完整路径。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>字幕文件名（含扩展名）。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>识别出的字幕格式。</summary>
    public SubtitleFormat Format { get; set; } = SubtitleFormat.Unknown;

    /// <summary>从文件名推断出的语言标记（如 chs/cht/eng/jpn）。</summary>
    public string? Language { get; set; }

    /// <summary>字幕来源（Embedded/Local/Online）。</summary>
    public SubtitleSource Source { get; set; } = SubtitleSource.Local;
}
