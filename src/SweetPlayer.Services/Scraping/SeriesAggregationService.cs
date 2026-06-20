using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Scraping;

public interface ISeriesAggregationService
{
    /// <summary>
    /// 聚合所有 TV 类型 VideoFile 到对应 MovieMetadata 上：统计 TotalSeasons、SeasonsEpisodeCount。
    /// </summary>
    Task AggregateSeriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 电视剧季聚合服务：扫描 VideoFile 中所有 TVEpisode，按 MovieMetadataId 分组，
/// 写回每个剧集 MovieMetadata 的总季数和每季集数。
/// </summary>
public class SeriesAggregationService : ISeriesAggregationService
{
    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;

    public SeriesAggregationService(IDbContextFactory<SweetPlayerDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task AggregateSeriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var episodes = await db.VideoFiles
            .Where(v => v.MediaType == MediaType.TVEpisode
                        && v.MovieMetadataId.HasValue
                        && v.Season.HasValue
                        && v.Episode.HasValue)
            .Select(v => new { v.MovieMetadataId, v.Season, v.Episode })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grouped = episodes
            .GroupBy(e => e.MovieMetadataId!.Value);

        foreach (var group in grouped)
        {
            var metadata = await db.MovieMetadata
                .FirstOrDefaultAsync(m => m.Id == group.Key, cancellationToken)
                .ConfigureAwait(false);
            if (metadata == null) continue;

            var seasons = group
                .GroupBy(e => e.Season!.Value)
                .OrderBy(g => g.Key)
                .ToList();

            metadata.TotalSeasons = seasons.Count;
            metadata.ContentType = MediaContentType.TVSeries;

            // 序列化为 "S1:12,S2:13" 形式
            metadata.SeasonsEpisodeCount = string.Join(",",
                seasons.Select(s => $"S{s.Key}:{s.Select(e => e.Episode!.Value).Distinct().Count()}"));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
