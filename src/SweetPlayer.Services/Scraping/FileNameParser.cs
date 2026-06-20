using System.Text.RegularExpressions;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// 视频文件名解析引擎，兼容 Infuse 命名规范。
/// 解析顺序：数据库 ID → 版本 / 多部分标签 → TV 模式 1-5（文件名内）→ TV 模式 6-10（依赖父文件夹）→ 电影标准格式 → 简单格式。
/// </summary>
public class FileNameParser : IFileNameParser
{
    private static readonly Regex TmdbIdRegex = new(@"\{tmdb-(\d+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImdbIdRegex = new(@"\{imdb-(tt\d+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CustomEditionRegex = new(@"\{edition-([^}]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 标准版本标签
    private static readonly string[] StandardEditions =
    {
        "Director's Cut", "Directors Cut", "Director Cut",
        "Extended Cut", "Extended Edition", "Extended",
        "Theatrical Cut", "Theatrical",
        "IMAX",
        "Unrated",
        "Remastered",
        "Special Edition",
        "Final Cut",
        "Ultimate Edition"
    };

    private static readonly Regex PartRegex = new(@"(?<![A-Za-z0-9])(?:cd|disc|part)\s*(\d+)(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // TV 模式：在 stem（去扩展名）上运行
    // 1) s01.e02 / s01_e02 / s01-e02 / s01 e02
    private static readonly Regex Tv1 = new(@"(?<![A-Za-z0-9])s(\d{1,2})[._\-\s]+e(\d{1,3})(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 2) s1e2 / S01E02 连写
    private static readonly Regex Tv2 = new(@"(?<![A-Za-z0-9])s(\d{1,2})e(\d{1,3})(?![A-Za-z0-9])X", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 3) 1x02
    private static readonly Regex Tv3 = new(@"(?<![A-Za-z0-9])(\d{1,2})x(\d{1,3})(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 4) se1.ep2 / se01_ep02
    private static readonly Regex Tv4 = new(@"(?<![A-Za-z0-9])se(\d{1,2})[._\-\s]+ep(\d{1,3})(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 5) season1.episode2 / season 1 episode 2
    private static readonly Regex Tv5 = new(@"(?<![A-Za-z0-9])season[._\-\s]*(\d{1,2})[._\-\s]+episode[._\-\s]*(\d{1,3})(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 文件夹季提取：season 1 / s1 / s01 / 第1季
    private static readonly Regex FolderSeasonRegex = new(@"(?<![A-Za-z0-9])(?:season[._\-\s]*|s)(\d{1,2})(?![A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ChineseSeasonRegex = new(@"第\s*(\d{1,2})\s*季", RegexOptions.Compiled);

    // 文件夹模式 - 仅集号：02 EpisodeName / e02 - EpisodeName
    private static readonly Regex FolderEpisodeOnlyRegex = new(@"^(?:e|episode[._\-\s]*)?(\d{1,3})(?:\s*[-_.\s]\s*(.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 文件夹模式 - 季.集 / 季-集：01.02 EpisodeName / 1-02 EpisodeName
    private static readonly Regex FolderSeasonEpisodeRegex = new(@"^(\d{1,2})[._\-x](\d{1,3})(?:[\s\-_.]+(.+))?$", RegexOptions.Compiled);

    // 文件夹模式 - S01E02 简短形式
    private static readonly Regex FolderShortSeRegex = new(@"^s(\d{1,2})\s*e(\d{1,3})(?:[\s\-_.]+(.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 电影：名称 (YYYY)
    private static readonly Regex MovieParenYear = new(@"^(.*?)[\s._\-]*\((\d{4})\)", RegexOptions.Compiled);

    // 电影：名称.YYYY 或 名称 YYYY 或 名称-YYYY（年份范围 1900-2099）
    private static readonly Regex MovieBareYear = new(@"^(.*?)[\s._\-]+(19\d{2}|20\d{2})(?![A-Za-z0-9])", RegexOptions.Compiled);

    public ParsedFileInfo Parse(string fileName, string? parentFolderPath = null)
    {
        var result = new ParsedFileInfo();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return result;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);

        // 1) 数据库 ID
        var tmdbMatch = TmdbIdRegex.Match(stem);
        if (tmdbMatch.Success)
        {
            result.TmdbId = tmdbMatch.Groups[1].Value;
            stem = TmdbIdRegex.Replace(stem, " ");
        }

        var imdbMatch = ImdbIdRegex.Match(stem);
        if (imdbMatch.Success)
        {
            result.ImdbId = imdbMatch.Groups[1].Value;
            stem = ImdbIdRegex.Replace(stem, " ");
        }

        // 2) 自定义版本 {edition-xxx}
        var customEdition = CustomEditionRegex.Match(stem);
        if (customEdition.Success)
        {
            result.EditionTag = customEdition.Groups[1].Value.Trim();
            stem = CustomEditionRegex.Replace(stem, " ");
        }
        else
        {
            // 标准版本标签
            foreach (var edition in StandardEditions)
            {
                var pattern = @"(?<![A-Za-z0-9])" + Regex.Escape(edition).Replace("\\ ", "[._\\-\\s]+") + @"(?![A-Za-z0-9])";
                var m = Regex.Match(stem, pattern, RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    result.EditionTag = edition;
                    stem = Regex.Replace(stem, pattern, " ", RegexOptions.IgnoreCase);
                    break;
                }
            }
        }

        // 3) 多部分
        var partMatch = PartRegex.Match(stem);
        if (partMatch.Success && int.TryParse(partMatch.Groups[1].Value, out var partNum))
        {
            result.PartNumber = partNum;
            stem = PartRegex.Replace(stem, " ");
        }

        // 4) 电视剧模式（文件名内）
        if (TryParseTvFromStem(stem, result))
        {
            // 尝试从父文件夹回填剧名（若 stem 没有剧名）
            if (string.IsNullOrWhiteSpace(result.Title))
            {
                result.Title = ExtractShowTitleFromFolder(parentFolderPath) ?? string.Empty;
            }

            FinalizeTitle(result);
            return result;
        }

        // 5) 文件夹辅助的 TV 模式 6-10
        if (TryParseTvFromFolder(stem, parentFolderPath, result))
        {
            FinalizeTitle(result);
            return result;
        }

        // 6) 电影：标准格式（带年份）
        if (TryParseMovieWithYear(stem, result))
        {
            FinalizeTitle(result);
            return result;
        }

        // 7) 电影：简单格式（无年份）
        var cleaned = TagFilter.StripTags(stem);
        cleaned = NormalizeSeparators(cleaned);
        result.Title = cleaned.Trim();
        result.MediaType = string.IsNullOrWhiteSpace(result.Title) ? MediaType.Unknown : MediaType.Movie;

        FinalizeTitle(result);
        return result;
    }

    private static bool TryParseTvFromStem(string stem, ParsedFileInfo result)
    {
        Match match;
        // 优先级 1
        match = Tv1.Match(stem);
        if (match.Success)
        {
            return AssignTv(stem, match, result);
        }

        // 优先级 2
        match = Tv2.Match(stem);
        if (match.Success)
        {
            return AssignTv(stem, match, result);
        }

        // 优先级 3
        match = Tv3.Match(stem);
        if (match.Success)
        {
            return AssignTv(stem, match, result);
        }

        // 优先级 4
        match = Tv4.Match(stem);
        if (match.Success)
        {
            return AssignTv(stem, match, result);
        }

        // 优先级 5
        match = Tv5.Match(stem);
        if (match.Success)
        {
            return AssignTv(stem, match, result);
        }

        return false;
    }

    private static bool AssignTv(string stem, Match match, ParsedFileInfo result)
    {
        if (!int.TryParse(match.Groups[1].Value, out var season)) return false;
        if (!int.TryParse(match.Groups[2].Value, out var episode)) return false;

        result.MediaType = MediaType.TVEpisode;
        result.Season = season;
        result.Episode = episode;

        // 剧名 = match 之前的部分；集名 = match 之后的部分
        var before = stem.Substring(0, match.Index);
        var after = match.Index + match.Length < stem.Length ? stem.Substring(match.Index + match.Length) : string.Empty;

        var title = TagFilter.StripTags(before);
        title = NormalizeSeparators(title);
        result.Title = title.Trim().TrimEnd('-', '_', '.', ' ');

        var ep = TagFilter.StripTags(after);
        ep = NormalizeSeparators(ep);
        ep = ep.Trim().TrimStart('-', '_', '.', ' ');
        if (!string.IsNullOrWhiteSpace(ep))
        {
            result.EpisodeTitle = ep;
        }

        return true;
    }

    private static bool TryParseTvFromFolder(string stem, string? parentFolderPath, ParsedFileInfo result)
    {
        if (string.IsNullOrWhiteSpace(parentFolderPath))
        {
            return false;
        }

        var folders = SplitFolders(parentFolderPath);
        if (folders.Length == 0)
        {
            return false;
        }

        var nearest = folders[^1];
        var seasonFromFolder = ExtractSeasonFromFolder(nearest);
        var normalizedStem = NormalizeSeparators(stem).Trim();

        // 模式 6: stem 是 S01E02
        var shortSe = FolderShortSeRegex.Match(normalizedStem);
        if (shortSe.Success)
        {
            if (int.TryParse(shortSe.Groups[1].Value, out var s) && int.TryParse(shortSe.Groups[2].Value, out var e))
            {
                result.MediaType = MediaType.TVEpisode;
                result.Season = s;
                result.Episode = e;
                if (shortSe.Groups[3].Success)
                {
                    var epTitle = TagFilter.StripTags(shortSe.Groups[3].Value);
                    result.EpisodeTitle = NormalizeSeparators(epTitle).Trim();
                }
                result.Title = ExtractShowTitleFromFolder(parentFolderPath) ?? string.Empty;
                return true;
            }
        }

        // 模式 7: stem 是 01.02 EpisodeName（季.集）
        var seasonEp = FolderSeasonEpisodeRegex.Match(normalizedStem);
        if (seasonEp.Success
            && int.TryParse(seasonEp.Groups[1].Value, out var sn)
            && int.TryParse(seasonEp.Groups[2].Value, out var en)
            && sn <= 50 && en <= 999)
        {
            // 仅当父级未提供季时使用此模式（避免误把电影名 1-02 当季集）
            // 但若父级提供了 season N 且与 stem 第一段一致，仍可使用
            result.MediaType = MediaType.TVEpisode;
            result.Season = seasonFromFolder ?? sn;
            result.Episode = en;
            if (seasonEp.Groups[3].Success)
            {
                var epTitle = TagFilter.StripTags(seasonEp.Groups[3].Value);
                result.EpisodeTitle = NormalizeSeparators(epTitle).Trim();
            }
            result.Title = ExtractShowTitleFromFolder(parentFolderPath) ?? string.Empty;
            return true;
        }

        // 模式 8/10: stem 是 02 EpisodeName 或 episode 02 - EpisodeName，需父文件夹含 season
        if (seasonFromFolder.HasValue)
        {
            var epOnly = FolderEpisodeOnlyRegex.Match(normalizedStem);
            if (epOnly.Success && int.TryParse(epOnly.Groups[1].Value, out var epNum) && epNum <= 999)
            {
                result.MediaType = MediaType.TVEpisode;
                result.Season = seasonFromFolder.Value;
                result.Episode = epNum;
                if (epOnly.Groups[2].Success)
                {
                    var epTitle = TagFilter.StripTags(epOnly.Groups[2].Value);
                    result.EpisodeTitle = NormalizeSeparators(epTitle).Trim();
                }
                result.Title = ExtractShowTitleFromFolder(parentFolderPath) ?? string.Empty;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseMovieWithYear(string stem, ParsedFileInfo result)
    {
        // 先用 (YYYY)
        var paren = MovieParenYear.Match(stem);
        if (paren.Success && int.TryParse(paren.Groups[2].Value, out var py))
        {
            var title = TagFilter.StripTags(paren.Groups[1].Value);
            title = NormalizeSeparators(title);
            result.Title = title.Trim();
            result.Year = py;
            result.MediaType = MediaType.Movie;
            return true;
        }

        // 再用 .YYYY 或 -YYYY
        var bare = MovieBareYear.Match(stem);
        if (bare.Success && int.TryParse(bare.Groups[2].Value, out var by))
        {
            var title = TagFilter.StripTags(bare.Groups[1].Value);
            title = NormalizeSeparators(title);
            result.Title = title.Trim();
            result.Year = by;
            result.MediaType = MediaType.Movie;
            return true;
        }

        return false;
    }

    private static int? ExtractSeasonFromFolder(string folderName)
    {
        var match = FolderSeasonRegex.Match(folderName);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var s))
        {
            return s;
        }

        var cn = ChineseSeasonRegex.Match(folderName);
        if (cn.Success && int.TryParse(cn.Groups[1].Value, out var sc))
        {
            return sc;
        }

        return null;
    }

    private static string? ExtractShowTitleFromFolder(string? parentFolderPath)
    {
        if (string.IsNullOrWhiteSpace(parentFolderPath))
        {
            return null;
        }

        var folders = SplitFolders(parentFolderPath);
        // 从最近向外查找：跳过 season/季 文件夹，使用上一级作为剧名
        for (var i = folders.Length - 1; i >= 0; i--)
        {
            var name = folders[i];
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (FolderSeasonRegex.IsMatch(name)) continue;
            if (ChineseSeasonRegex.IsMatch(name)) continue;
            // 跳过纯 specials/extras
            if (string.Equals(name, "Specials", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(name, "Extras", StringComparison.OrdinalIgnoreCase)) continue;

            var clean = TagFilter.StripTags(name);
            clean = NormalizeSeparators(clean).Trim();
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }
        return null;
    }

    private static string[] SplitFolders(string parentFolderPath)
    {
        return parentFolderPath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizeSeparators(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        // 将 . _ 替换为空格；保留 - 由后续 Trim 处理
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch == '.' || ch == '_')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(ch);
            }
        }
        var s = sb.ToString();
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    private static void FinalizeTitle(ParsedFileInfo info)
    {
        if (!string.IsNullOrEmpty(info.Title))
        {
            info.Title = info.Title.Trim().Trim('-', '_', '.', ' ').Trim();
        }
    }
}
