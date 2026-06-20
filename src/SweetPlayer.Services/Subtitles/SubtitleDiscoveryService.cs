using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// <see cref="ISubtitleDiscoveryService"/> 的默认实现。
/// </summary>
public class SubtitleDiscoveryService : ISubtitleDiscoveryService
{
    private static readonly string[] SupportedExtensions =
    {
        ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt"
    };

    /// <summary>常见的字幕子目录名称。</summary>
    private static readonly string[] SubFolderNames = { "Subs", "Subtitles" };

    /// <summary>常见语言标记到内部语言码的映射。</summary>
    private static readonly Dictionary<string, string> LanguageTokens =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["chs"] = "chs",
            ["sc"] = "chs",
            ["zh-cn"] = "chs",
            ["zh-hans"] = "chs",
            ["chi"] = "chs",
            ["cn"] = "chs",
            ["cht"] = "cht",
            ["tc"] = "cht",
            ["zh-tw"] = "cht",
            ["zh-hk"] = "cht",
            ["zh-hant"] = "cht",
            ["eng"] = "eng",
            ["en"] = "eng",
            ["jpn"] = "jpn",
            ["jp"] = "jpn",
            ["ja"] = "jpn",
            ["kor"] = "kor",
            ["ko"] = "kor",
            ["fre"] = "fre",
            ["fr"] = "fre",
            ["ger"] = "ger",
            ["de"] = "ger",
            ["spa"] = "spa",
            ["es"] = "spa",
            ["rus"] = "rus",
            ["ru"] = "rus",
        };

    private readonly ILogger<SubtitleDiscoveryService>? _logger;

    public SubtitleDiscoveryService(ILogger<SubtitleDiscoveryService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<SubtitleFileInfo> DiscoverLocalSubtitles(string videoFilePath)
    {
        var results = new List<SubtitleFileInfo>();
        if (string.IsNullOrWhiteSpace(videoFilePath))
        {
            return results;
        }

        try
        {
            var directory = Path.GetDirectoryName(videoFilePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return results;
            }

            var videoBaseName = Path.GetFileNameWithoutExtension(videoFilePath);
            if (string.IsNullOrEmpty(videoBaseName))
            {
                return results;
            }

            CollectFromDirectory(directory, videoBaseName, results);

            foreach (var subFolder in SubFolderNames)
            {
                var path = Path.Combine(directory, subFolder);
                if (Directory.Exists(path))
                {
                    CollectFromDirectory(path, videoBaseName, results);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "字幕发现失败：{Path}", videoFilePath);
        }

        return results;
    }

    private static void CollectFromDirectory(string directory, string videoBaseName, List<SubtitleFileInfo> results)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (string.IsNullOrEmpty(ext))
            {
                continue;
            }

            if (!SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileBase = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileBase))
            {
                continue;
            }

            // 严格同名 或 视频名.语言标记[.其他] 形式
            var matches = string.Equals(fileBase, videoBaseName, StringComparison.OrdinalIgnoreCase)
                          || fileBase.StartsWith(videoBaseName + ".", StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                continue;
            }

            results.Add(new SubtitleFileInfo
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                Format = ParseFormat(ext),
                Language = InferLanguage(fileBase, videoBaseName),
                Source = SubtitleSource.Local,
            });
        }
    }

    /// <summary>
    /// 根据扩展名识别字幕格式。
    /// </summary>
    public static SubtitleFormat ParseFormat(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return SubtitleFormat.Unknown;
        }

        return extension.TrimStart('.').ToLowerInvariant() switch
        {
            "srt" => SubtitleFormat.SRT,
            "ass" => SubtitleFormat.ASS,
            "ssa" => SubtitleFormat.SSA,
            "sub" => SubtitleFormat.SUB,
            "idx" => SubtitleFormat.IDX,
            "vtt" => SubtitleFormat.VTT,
            _ => SubtitleFormat.Unknown,
        };
    }

    /// <summary>
    /// 从字幕基础文件名（不含扩展名）中推断语言标记。
    /// </summary>
    private static string? InferLanguage(string fileBase, string videoBaseName)
    {
        if (fileBase.Length <= videoBaseName.Length + 1)
        {
            return null;
        }

        var suffix = fileBase[(videoBaseName.Length + 1)..];
        if (string.IsNullOrEmpty(suffix))
        {
            return null;
        }

        var tokens = suffix.Split(new[] { '.', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (LanguageTokens.TryGetValue(token, out var lang))
            {
                return lang;
            }
        }

        return null;
    }
}
