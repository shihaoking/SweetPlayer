using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// <see cref="ISubtitleLoadService"/> 的默认实现。
/// </summary>
public class SubtitleLoadService : ISubtitleLoadService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt"
        };

    private readonly ILogger<SubtitleLoadService>? _logger;

    public SubtitleLoadService(ILogger<SubtitleLoadService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public SubtitleFileInfo LoadFromFile(string subtitleFilePath)
    {
        if (string.IsNullOrWhiteSpace(subtitleFilePath))
        {
            throw new ArgumentException("字幕文件路径不能为空", nameof(subtitleFilePath));
        }

        if (!File.Exists(subtitleFilePath))
        {
            throw new FileNotFoundException("字幕文件不存在", subtitleFilePath);
        }

        var ext = Path.GetExtension(subtitleFilePath);
        if (!SupportedExtensions.Contains(ext))
        {
            throw new NotSupportedException($"不支持的字幕格式：{ext}");
        }

        return new SubtitleFileInfo
        {
            FilePath = subtitleFilePath,
            FileName = Path.GetFileName(subtitleFilePath),
            Format = SubtitleDiscoveryService.ParseFormat(ext),
            Language = null,
            Source = SubtitleSource.Local,
        };
    }

    /// <inheritdoc />
    public void ApplyToPlayer(IMpvPlayerService player, string subtitleFilePath)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        var info = LoadFromFile(subtitleFilePath);
        try
        {
            player.LoadExternalSubtitle(info.FilePath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "加载字幕到播放器失败：{Path}", subtitleFilePath);
            throw;
        }
    }
}
