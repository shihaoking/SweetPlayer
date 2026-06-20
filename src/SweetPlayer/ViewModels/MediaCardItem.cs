using SweetPlayer.Core.Models;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 海报墙卡片数据传输对象，代表一部电影或一部电视剧（聚合后的剧集条目）。
/// </summary>
public sealed class MediaCardItem
{
    /// <summary>对应的 MovieMetadata.Id。</summary>
    public int MetadataId { get; init; }

    /// <summary>显示用的中文标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>海报本地路径（可能为 null）。</summary>
    public string? PosterPath { get; init; }

    /// <summary>背景图本地路径（可能为 null）。</summary>
    public string? BackdropPath { get; init; }

    /// <summary>媒体类型（电影 / 电视剧）。</summary>
    public MediaContentType MediaType { get; init; }

    /// <summary>本地源识别到的季数（仅电视剧有效）。</summary>
    public int SeasonCount { get; init; }

    /// <summary>是否电视剧（用于 UI 绑定 InfoBadge 可见性）。</summary>
    public bool IsSeries => MediaType == MediaContentType.TVSeries;

    /// <summary>季数显示文本，如 "5季"。</summary>
    public string SeasonBadgeText => SeasonCount > 0 ? $"{SeasonCount}季" : string.Empty;

    /// <summary>是否含 HDR 视频文件。</summary>
    public bool HasHdr { get; init; }

    /// <summary>是否含杜比视界。</summary>
    public bool HasDolbyVision { get; init; }

    /// <summary>是否含杜比全景声。</summary>
    public bool HasDolbyAtmos { get; init; }

    /// <summary>豆瓣评分（仅电影显示在副标题）。</summary>
    public double? DoubanRating { get; init; }

    /// <summary>发行年份。</summary>
    public int? Year { get; init; }
}
