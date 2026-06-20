using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// <see cref="ISubtitleTrackService"/> 的默认实现。
/// </summary>
/// <remarks>
/// 由于当前 <see cref="IMpvPlayerService"/> 未暴露 track-list 查询能力，
/// 本服务通过 <see cref="RegisterEmbeddedTrack"/> / <see cref="RegisterExternalSubtitle"/>
/// 接收来自播放页或上层逻辑注册的轨道信息，统一管理。
/// </remarks>
public class SubtitleTrackService : ISubtitleTrackService
{
    private readonly object _sync = new();
    private readonly List<SubtitleTrackInfo> _tracks = new();
    private int _activeTrackId;
    private int _nextExternalId = 1000;
    private readonly ILogger<SubtitleTrackService>? _logger;

    public SubtitleTrackService(ILogger<SubtitleTrackService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 清空当前轨道列表（通常在切换视频时调用）。
    /// </summary>
    public void Reset()
    {
        lock (_sync)
        {
            _tracks.Clear();
            _activeTrackId = 0;
            _nextExternalId = 1000;
        }
    }

    /// <summary>
    /// 注册内嵌字幕轨道（来自 mpv track-list）。
    /// </summary>
    public void RegisterEmbeddedTrack(int trackId, string title, string? language)
    {
        if (trackId <= 0) return;
        lock (_sync)
        {
            if (_tracks.Any(t => t.TrackId == trackId && t.Source == SubtitleSource.Embedded))
            {
                return;
            }
            _tracks.Add(new SubtitleTrackInfo
            {
                TrackId = trackId,
                Title = string.IsNullOrWhiteSpace(title) ? $"内嵌字幕 #{trackId}" : title,
                Language = language,
                Source = SubtitleSource.Embedded,
                IsActive = false,
            });
        }
    }

    /// <summary>
    /// 注册外挂字幕（本地或在线下载），返回分配的 trackId。
    /// </summary>
    public int RegisterExternalSubtitle(SubtitleFileInfo info)
    {
        if (info is null) throw new ArgumentNullException(nameof(info));
        lock (_sync)
        {
            var existing = _tracks.FirstOrDefault(t =>
                t.Source != SubtitleSource.Embedded
                && string.Equals(t.Title, info.FileName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing.TrackId;
            }

            var id = _nextExternalId++;
            _tracks.Add(new SubtitleTrackInfo
            {
                TrackId = id,
                Title = string.IsNullOrEmpty(info.FileName) ? Path.GetFileName(info.FilePath) : info.FileName,
                Language = info.Language,
                Source = info.Source,
                IsActive = false,
            });
            return id;
        }
    }

    /// <inheritdoc />
    public List<SubtitleTrackInfo> GetAvailableTracks(IMpvPlayerService player)
    {
        lock (_sync)
        {
            return _tracks
                .Select(t => new SubtitleTrackInfo
                {
                    TrackId = t.TrackId,
                    Title = t.Title,
                    Language = t.Language,
                    Source = t.Source,
                    IsActive = t.TrackId == _activeTrackId,
                })
                .ToList();
        }
    }

    /// <inheritdoc />
    public void SwitchTrack(IMpvPlayerService player, int trackId)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));

        if (trackId <= 0)
        {
            DisableSubtitles(player);
            return;
        }

        try
        {
            player.SetSubtitleTrack(trackId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "切换字幕轨道失败：{TrackId}", trackId);
            return;
        }

        lock (_sync)
        {
            _activeTrackId = trackId;
            foreach (var t in _tracks)
            {
                t.IsActive = t.TrackId == trackId;
            }
        }
    }

    /// <inheritdoc />
    public void DisableSubtitles(IMpvPlayerService player)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        try
        {
            player.SetSubtitleTrack(0);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "关闭字幕失败");
            return;
        }

        lock (_sync)
        {
            _activeTrackId = 0;
            foreach (var t in _tracks)
            {
                t.IsActive = false;
            }
        }
    }
}
