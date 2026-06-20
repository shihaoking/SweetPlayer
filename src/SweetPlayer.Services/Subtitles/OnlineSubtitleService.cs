using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// <see cref="IOnlineSubtitleService"/> 默认实现，基于 <see cref="IShooterApiClient"/>。
/// </summary>
/// <remarks>
/// 缓存路径：%LocalAppData%/SweetPlayer/subtitles/{videoFileName}/{subtitle_hash}.ext
/// </remarks>
public class OnlineSubtitleService : IOnlineSubtitleService
{
    private readonly IShooterApiClient _shooter;
    private readonly ILogger<OnlineSubtitleService>? _logger;
    private readonly string _cacheRoot;

    public OnlineSubtitleService(IShooterApiClient shooter, ILogger<OnlineSubtitleService>? logger = null)
    {
        _shooter = shooter ?? throw new ArgumentNullException(nameof(shooter));
        _logger = logger;
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SweetPlayer",
            "subtitles");
    }

    /// <inheritdoc />
    public async Task<List<OnlineSubtitleResult>> SearchSubtitlesAsync(string videoFilePath, string? chineseTitle = null)
    {
        var results = new List<OnlineSubtitleResult>();

        if (!string.IsNullOrWhiteSpace(videoFilePath) && File.Exists(videoFilePath))
        {
            try
            {
                results = await _shooter.SearchByHashAsync(videoFilePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "哈希搜索异常");
            }
        }

        if (results.Count == 0)
        {
            var query = !string.IsNullOrWhiteSpace(chineseTitle)
                ? chineseTitle!
                : (string.IsNullOrWhiteSpace(videoFilePath) ? string.Empty : Path.GetFileNameWithoutExtension(videoFilePath));

            if (!string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    results = await _shooter.SearchByNameAsync(query).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "名称搜索异常：{Query}", query);
                }
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<SubtitleFileInfo> DownloadAndCacheAsync(OnlineSubtitleResult result, string videoFileName)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
        {
            throw new ArgumentException("DownloadUrl 不能为空", nameof(result));
        }

        var safeVideoName = MakeSafeFolderName(string.IsNullOrWhiteSpace(videoFileName) ? "unknown" : videoFileName);
        var folder = Path.Combine(_cacheRoot, safeVideoName);
        Directory.CreateDirectory(folder);

        var ext = NormalizeExtension(result.Format);
        var hash = HashOf($"{result.DownloadUrl}|{result.Language}|{result.Title}");
        var fileName = $"{hash}{ext}";
        var savePath = Path.Combine(folder, fileName);

        if (!File.Exists(savePath))
        {
            await _shooter.DownloadSubtitleAsync(result.DownloadUrl, savePath).ConfigureAwait(false);
        }

        return new SubtitleFileInfo
        {
            FilePath = savePath,
            FileName = fileName,
            Format = SubtitleDiscoveryService.ParseFormat(ext),
            Language = string.IsNullOrWhiteSpace(result.Language) ? null : result.Language,
            Source = SubtitleSource.Online,
        };
    }

    private static string NormalizeExtension(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return ".srt";
        var f = format.Trim().TrimStart('.').ToLowerInvariant();
        return f switch
        {
            "srt" or "ass" or "ssa" or "sub" or "idx" or "vtt" => "." + f,
            _ => ".srt",
        };
    }

    private static string MakeSafeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString();
    }

    private static string HashOf(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
