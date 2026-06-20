using System.Text.RegularExpressions;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// 文件名后缀标记过滤器：移除分辨率、编码、来源、音频、HDR、制作组等噪声标记，
/// 只保留有价值的检索关键词（标题、年份、季集等）。
/// </summary>
public static class TagFilter
{
    // 分辨率
    private static readonly string[] ResolutionTags =
    {
        "2160p", "1080p", "720p", "480p", "4K", "UHD", "FHD", "HD", "SD"
    };

    // 编码
    private static readonly string[] CodecTags =
    {
        "HEVC", "H265", "H.265", "H264", "H.264", "x264", "x265",
        "AV1", "VP9", "MPEG4", "MPEG-4", "XviD", "DivX", "10bit", "8bit"
    };

    // 来源
    private static readonly string[] SourceTags =
    {
        "BluRay", "Blu-Ray", "BDRip", "BDRemux", "BRRip", "WEB-DL", "WEBDL",
        "WEBRip", "WEB", "HDRip", "DVDRip", "DVDScr", "HDTV", "PDTV", "Remux", "CAM", "TS"
    };

    // 音频
    private static readonly string[] AudioTags =
    {
        "DTS-HD", "DTS-X", "DTSHD", "DTSX", "DTS",
        "TrueHD", "Atmos", "AAC", "FLAC", "AC3", "EAC3", "E-AC-3", "AC-3",
        "DD5.1", "DDP5.1", "DD+", "DDP", "DD", "MP3", "Opus", "PCM",
        "5.1", "7.1", "2.0"
    };

    // HDR
    private static readonly string[] HdrTags =
    {
        "HDR10Plus", "HDR10+", "HDR10", "HDR", "DV", "DoVi", "Dolby.Vision", "DolbyVision", "HLG"
    };

    private static readonly Regex BracketGroupRegex = new(@"\[[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex TrailingGroupRegex = new(@"-[A-Za-z0-9]+$", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// 移除文件名中的所有干扰标签，返回干净的文本（保留分隔符以便后续解析）。
    /// </summary>
    public static string StripTags(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var result = input;

        // 移除方括号制作组
        result = BracketGroupRegex.Replace(result, " ");

        // 按长度倒序移除标签，避免前缀冲突（如先 DTS-HD 再 DTS）
        var allTags = ResolutionTags
            .Concat(HdrTags)
            .Concat(CodecTags)
            .Concat(SourceTags)
            .Concat(AudioTags)
            .OrderByDescending(t => t.Length);

        foreach (var tag in allTags)
        {
            // 用单词边界匹配，避免误删片名中的字母组合
            var pattern = @"(?i)(?<![A-Za-z0-9])" + Regex.Escape(tag) + @"(?![A-Za-z0-9])";
            result = Regex.Replace(result, pattern, " ");
        }

        // 移除末尾 -GroupName（必须放在标签去除之后）
        // 注意：年份格式 -2014 不要被误删，所以仅在剩余末尾不是数字时执行
        var trimmed = result.TrimEnd();
        var match = TrailingGroupRegex.Match(trimmed);
        if (match.Success)
        {
            var content = match.Value.TrimStart('-');
            // 仅当内容包含字母时才视为制作组
            if (Regex.IsMatch(content, "[A-Za-z]"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - match.Length);
            }
        }

        // 折叠多余空白
        trimmed = MultiSpaceRegex.Replace(trimmed, " ").Trim();

        return trimmed;
    }
}
