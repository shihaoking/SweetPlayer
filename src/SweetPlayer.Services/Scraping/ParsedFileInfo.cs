using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// 文件名解析结果。
/// </summary>
public class ParsedFileInfo
{
    public MediaType MediaType { get; set; } = MediaType.Unknown;

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public string? EpisodeTitle { get; set; }

    public string? TmdbId { get; set; }

    public string? ImdbId { get; set; }

    /// <summary>
    /// 版本标签，如 Director's Cut、Extended Cut 等，或自定义 {edition-xxx}。
    /// </summary>
    public string? EditionTag { get; set; }

    /// <summary>
    /// 多部分序号（cd1/cd2、disc1、part2 等），1 起始。
    /// </summary>
    public int? PartNumber { get; set; }
}

public interface IFileNameParser
{
    ParsedFileInfo Parse(string fileName, string? parentFolderPath = null);
}
