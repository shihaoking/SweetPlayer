using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// 播放进度持久化服务接口。
/// </summary>
public interface IPlaybackProgressService
{
    /// <summary>保存或更新指定视频的播放进度；超过 90% 自动标记为已完成。</summary>
    Task SaveProgressAsync(int videoFileId, TimeSpan position, TimeSpan duration);

    /// <summary>查询视频的最近播放进度。</summary>
    Task<PlaybackProgress?> GetProgressAsync(int videoFileId);

    /// <summary>显式标记视频为已观看完毕。</summary>
    Task MarkCompletedAsync(int videoFileId);
}

/// <summary>
/// 默认播放进度持久化实现：使用 EF Core 写入 SQLite。
/// </summary>
public class PlaybackProgressService : IPlaybackProgressService
{
    /// <summary>视频被视为"已完成"的进度阈值（90%）。</summary>
    public const double CompletedThreshold = 0.9;

    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly ILogger<PlaybackProgressService> _logger;

    public PlaybackProgressService(
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        ILogger<PlaybackProgressService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task SaveProgressAsync(int videoFileId, TimeSpan position, TimeSpan duration)
    {
        if (videoFileId <= 0) return;
        if (duration <= TimeSpan.Zero) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var record = await db.PlaybackProgress
                .FirstOrDefaultAsync(p => p.VideoFileId == videoFileId);

            var ratio = position.TotalSeconds / duration.TotalSeconds;
            var completed = ratio >= CompletedThreshold;

            if (record is null)
            {
                record = new PlaybackProgress
                {
                    VideoFileId = videoFileId,
                    Position = position,
                    Duration = duration,
                    LastPlayedAt = DateTime.UtcNow,
                    IsCompleted = completed,
                };
                db.PlaybackProgress.Add(record);
            }
            else
            {
                record.Position = position;
                record.Duration = duration;
                record.LastPlayedAt = DateTime.UtcNow;
                if (completed)
                {
                    record.IsCompleted = true;
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存播放进度失败：videoFileId={VideoFileId}", videoFileId);
        }
    }

    public async Task<PlaybackProgress?> GetProgressAsync(int videoFileId)
    {
        if (videoFileId <= 0) return null;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.PlaybackProgress
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.VideoFileId == videoFileId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询播放进度失败：videoFileId={VideoFileId}", videoFileId);
            return null;
        }
    }

    public async Task MarkCompletedAsync(int videoFileId)
    {
        if (videoFileId <= 0) return;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var record = await db.PlaybackProgress
                .FirstOrDefaultAsync(p => p.VideoFileId == videoFileId);

            if (record is null)
            {
                record = new PlaybackProgress
                {
                    VideoFileId = videoFileId,
                    Position = TimeSpan.Zero,
                    Duration = TimeSpan.Zero,
                    LastPlayedAt = DateTime.UtcNow,
                    IsCompleted = true,
                };
                db.PlaybackProgress.Add(record);
            }
            else
            {
                record.IsCompleted = true;
                record.LastPlayedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "标记完成失败：videoFileId={VideoFileId}", videoFileId);
        }
    }
}
