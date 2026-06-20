namespace SweetPlayer.Core.Models;

/// <summary>
/// 在线字幕搜索返回的结果项。
/// </summary>
public class OnlineSubtitleResult
{
    /// <summary>字幕标题（通常为视频名或服务返回的描述）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>语言标记（如 chs/cht/eng）。</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>字幕下载链接。</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>字幕格式（小写扩展名，如 srt/ass）。</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>评分（如有）。</summary>
    public double? Rating { get; set; }
}
