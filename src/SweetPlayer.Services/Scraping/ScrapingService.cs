using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Scraping;

public interface IScrapingService
{
    /// <summary>
    /// 自动刮削单个视频文件：解析文件名 → 搜索豆瓣 → 搜索TMDB（fallback） → 写入元数据 → 缓存海报。
    /// </summary>
    Task<MovieMetadata?> ScrapeAsync(int videoFileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动搜索豆瓣，返回候选列表，供用户选择。
    /// </summary>
    Task<List<DoubanSearchResult>> ManualSearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将用户选择的豆瓣条目应用到指定视频文件。
    /// </summary>
    Task<MovieMetadata?> ApplyManualMatchAsync(int videoFileId, string doubanId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 元数据刮削服务：协调 FileNameParser、DoubanClient、TmdbClient、PosterCacheService，
/// 自动或手动地将元数据绑定到 VideoFile 上。豆瓣优先，TMDB作为备用数据源。
/// </summary>
public class ScrapingService : IScrapingService
{
    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly IFileNameParser _parser;
    private readonly IDoubanClient _douban;
    private readonly ITmdbClient _tmdb;
    private readonly IPosterCacheService _posterCache;
    private readonly ILogger<ScrapingService> _logger;

    public ScrapingService(
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        IFileNameParser parser,
        IDoubanClient douban,
        ITmdbClient tmdb,
        IPosterCacheService posterCache,
        ILogger<ScrapingService> logger)
    {
        _dbFactory = dbFactory;
        _parser = parser;
        _douban = douban;
        _tmdb = tmdb;
        _posterCache = posterCache;
        _logger = logger;
    }

    public async Task<MovieMetadata?> ScrapeAsync(int videoFileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var video = await db.VideoFiles.FirstOrDefaultAsync(v => v.Id == videoFileId, cancellationToken).ConfigureAwait(false);
        if (video == null)
        {
            _logger.LogWarning("刮削失败：未找到视频文件 ID={VideoFileId}", videoFileId);
            return null;
        }

        _logger.LogInformation("开始刮削：{FileName}", video.FileName);

        var parentFolder = Path.GetDirectoryName(video.FullPath);
        var info = _parser.Parse(video.FileName, parentFolder);

        _logger.LogDebug("文件名解析结果：标题={Title}, 年份={Year}, 类型={MediaType}",
            info.Title, info.Year, info.MediaType);

        // 把解析出的季 / 集 / 版本标签保存到 VideoFile（无论是否搜索成功）
        ApplyParsedToVideoFile(video, info);

        // 优先 ID 直查（暂仅支持 doubanId 走搜索；TMDB / IMDB 走标题搜索回退）
        var query = !string.IsNullOrWhiteSpace(info.Title) ? info.Title : Path.GetFileNameWithoutExtension(video.FileName);
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("刮削失败：无法从文件名提取有效标题 - {FileName}", video.FileName);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // 步骤1：尝试豆瓣搜索
        _logger.LogInformation("搜索豆瓣：查询=\"{Query}\", 年份={Year}", query, info.Year);
        var doubanResults = await _douban.SearchAsync(query, info.Year, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("豆瓣搜索返回 {Count} 个结果", doubanResults.Count);

        var best = SelectBest(doubanResults, info);

        MovieMetadata? metadata = null;

        if (best != null)
        {
            // 豆瓣有结果，使用豆瓣数据
            _logger.LogInformation("选择豆瓣最佳匹配：{Title} ({Year}) - 豆瓣ID={DoubanId}",
                best.Title, best.Year, best.DoubanId);
            metadata = await UpsertMetadataFromDoubanAsync(db, best, info, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // 步骤2：豆瓣无结果，尝试TMDB fallback
            _logger.LogInformation("豆瓣无结果，尝试TMDB fallback：查询=\"{Query}\", 年份={Year}", query, info.Year);
            var tmdbResults = await _tmdb.SearchAsync(query, info.Year, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("TMDB搜索返回 {Count} 个结果", tmdbResults.Count);

            var tmdbBest = SelectBestTmdb(tmdbResults, info);
            if (tmdbBest != null)
            {
                _logger.LogInformation("选择TMDB最佳匹配：{Title} ({Year}) - TMDB ID={TmdbId}",
                    tmdbBest.Title, tmdbBest.Year, tmdbBest.Id);
                metadata = await UpsertMetadataFromTmdbAsync(db, tmdbBest, info, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("刮削失败：豆瓣和TMDB均未找到匹配结果 - {FileName} (查询=\"{Query}\", 年份={Year})",
                    video.FileName, query, info.Year);
            }
        }

        if (metadata != null)
        {
            video.MovieMetadataId = metadata.Id;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("✓ 刮削成功：{FileName} → {Title} ({Year}) [数据源: {DataSource}]",
                video.FileName, metadata.ChineseTitle, metadata.Year, metadata.DataSource);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return metadata;
    }

    public Task<List<DoubanSearchResult>> ManualSearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return _douban.SearchAsync(query, null, cancellationToken);
    }

    public async Task<MovieMetadata?> ApplyManualMatchAsync(int videoFileId, string doubanId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var video = await db.VideoFiles.FirstOrDefaultAsync(v => v.Id == videoFileId, cancellationToken).ConfigureAwait(false);
        if (video == null) return null;

        var detail = await _douban.GetDetailAsync(doubanId, cancellationToken).ConfigureAwait(false);
        if (detail == null) return null;

        var metadata = await db.MovieMetadata.FirstOrDefaultAsync(m => m.DoubanId == doubanId, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            metadata = new MovieMetadata
            {
                DoubanId = doubanId,
                ScrapedAt = DateTime.UtcNow,
                Confidence = MatchConfidence.Manual,
                DataSource = MetadataSource.Douban
            };
            db.MovieMetadata.Add(metadata);
        }
        else
        {
            metadata.Confidence = MatchConfidence.Manual;
            metadata.DataSource = MetadataSource.Douban;
        }

        FillMetadataFromDetail(metadata, detail);

        if (!string.IsNullOrEmpty(detail.PosterUrl))
        {
            metadata.PosterLocalPath = await _posterCache
                .GetOrDownloadPosterAsync(doubanId, detail.PosterUrl, cancellationToken)
                .ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        video.MovieMetadataId = metadata.Id;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return metadata;
    }

    private static void ApplyParsedToVideoFile(VideoFile video, ParsedFileInfo info)
    {
        if (info.MediaType != MediaType.Unknown)
        {
            video.MediaType = info.MediaType;
        }
        if (info.Season.HasValue) video.Season = info.Season;
        if (info.Episode.HasValue) video.Episode = info.Episode;
        if (!string.IsNullOrWhiteSpace(info.EpisodeTitle)) video.EpisodeTitle = info.EpisodeTitle;
        if (!string.IsNullOrWhiteSpace(info.EditionTag)) video.EditionTag = info.EditionTag;
    }

    private static DoubanSearchResult? SelectBest(List<DoubanSearchResult> results, ParsedFileInfo info)
    {
        if (results.Count == 0) return null;

        if (info.Year.HasValue)
        {
            var yearMatch = results.FirstOrDefault(r => r.Year == info.Year.Value);
            if (yearMatch != null) return yearMatch;
        }
        return results[0];
    }

    private static TmdbSearchResult? SelectBestTmdb(List<TmdbSearchResult> results, ParsedFileInfo info)
    {
        if (results.Count == 0) return null;

        if (info.Year.HasValue)
        {
            var yearMatch = results.FirstOrDefault(r => r.Year == info.Year.Value);
            if (yearMatch != null) return yearMatch;
        }
        return results[0];
    }

    private async Task<MovieMetadata?> UpsertMetadataFromDoubanAsync(SweetPlayerDbContext db, DoubanSearchResult best, ParsedFileInfo info, CancellationToken cancellationToken)
    {
        var existing = await db.MovieMetadata.FirstOrDefaultAsync(m => m.DoubanId == best.DoubanId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return existing;
        }

        var detail = await _douban.GetDetailAsync(best.DoubanId, cancellationToken).ConfigureAwait(false);
        var metadata = new MovieMetadata
        {
            DoubanId = best.DoubanId,
            ChineseTitle = best.Title,
            OriginalTitle = best.OriginalTitle,
            Year = best.Year ?? info.Year,
            ContentType = info.MediaType == MediaType.TVEpisode ? MediaContentType.TVSeries : MediaContentType.Movie,
            Confidence = ComputeConfidence(best, info),
            DataSource = MetadataSource.Douban,
            ScrapedAt = DateTime.UtcNow
        };

        if (detail != null)
        {
            FillMetadataFromDetail(metadata, detail);
        }

        db.MovieMetadata.Add(metadata);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var posterUrl = detail?.PosterUrl ?? best.PosterUrl;
        if (!string.IsNullOrEmpty(posterUrl))
        {
            metadata.PosterLocalPath = await _posterCache
                .GetOrDownloadPosterAsync(best.DoubanId, posterUrl, cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return metadata;
    }

    private static void FillMetadataFromDetail(MovieMetadata metadata, DoubanMovieDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.ChineseTitle)) metadata.ChineseTitle = detail.ChineseTitle;
        if (!string.IsNullOrWhiteSpace(detail.OriginalTitle)) metadata.OriginalTitle = detail.OriginalTitle;
        if (detail.Year.HasValue) metadata.Year = detail.Year;
        if (detail.Rating.HasValue) metadata.DoubanRating = detail.Rating;
        if (!string.IsNullOrWhiteSpace(detail.Genres)) metadata.Genres = detail.Genres;
        if (!string.IsNullOrWhiteSpace(detail.Director)) metadata.Director = detail.Director;
        if (!string.IsNullOrWhiteSpace(detail.Cast)) metadata.Cast = detail.Cast;
        if (!string.IsNullOrWhiteSpace(detail.Synopsis)) metadata.Synopsis = detail.Synopsis;

        if (string.Equals(detail.SubType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            metadata.ContentType = MediaContentType.TVSeries;
        }
    }

    private static MatchConfidence ComputeConfidence(DoubanSearchResult best, ParsedFileInfo info)
    {
        if (info.Year.HasValue && best.Year == info.Year.Value)
        {
            return MatchConfidence.High;
        }
        if (!info.Year.HasValue)
        {
            return MatchConfidence.Medium;
        }
        return MatchConfidence.Low;
    }

    private async Task<MovieMetadata?> UpsertMetadataFromTmdbAsync(SweetPlayerDbContext db, TmdbSearchResult best, ParsedFileInfo info, CancellationToken cancellationToken)
    {
        var tmdbId = best.Id.ToString();
        var existing = await db.MovieMetadata.FirstOrDefaultAsync(m => m.TmdbId == tmdbId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return existing;
        }

        var detail = await _tmdb.GetDetailAsync(tmdbId, cancellationToken).ConfigureAwait(false);
        var metadata = new MovieMetadata
        {
            TmdbId = tmdbId,
            ChineseTitle = best.Title ?? best.OriginalTitle ?? "未知",
            OriginalTitle = best.OriginalTitle,
            Year = best.Year ?? info.Year,
            ContentType = info.MediaType == MediaType.TVEpisode ? MediaContentType.TVSeries : MediaContentType.Movie,
            Confidence = ComputeConfidenceTmdb(best, info),
            DataSource = MetadataSource.TMDB,
            ScrapedAt = DateTime.UtcNow
        };

        if (detail != null)
        {
            FillMetadataFromTmdbDetail(metadata, detail);
        }

        db.MovieMetadata.Add(metadata);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var posterPath = detail?.PosterPath ?? best.PosterPath;
        if (!string.IsNullOrEmpty(posterPath))
        {
            var posterUrl = _tmdb.GetPosterUrl(posterPath);
            metadata.PosterLocalPath = await _posterCache
                .GetOrDownloadPosterAsync(tmdbId, posterUrl, cancellationToken)
                .ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return metadata;
    }

    private static void FillMetadataFromTmdbDetail(MovieMetadata metadata, TmdbMovieDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.Title)) metadata.ChineseTitle = detail.Title;
        if (!string.IsNullOrWhiteSpace(detail.OriginalTitle)) metadata.OriginalTitle = detail.OriginalTitle;
        if (detail.Year.HasValue) metadata.Year = detail.Year;
        if (detail.VoteAverage.HasValue) metadata.DoubanRating = detail.VoteAverage; // 使用TMDB评分
        if (!string.IsNullOrWhiteSpace(detail.GenresString)) metadata.Genres = detail.GenresString;
        if (!string.IsNullOrWhiteSpace(detail.Overview)) metadata.Synopsis = detail.Overview;

        // 提取导演和演员
        if (detail.Credits != null)
        {
            var director = detail.Credits.Crew.FirstOrDefault(c => c.Job == "Director");
            if (director != null)
            {
                metadata.Director = director.Name;
            }

            var cast = detail.Credits.Cast.OrderBy(c => c.Order).Take(5).Select(c => c.Name);
            metadata.Cast = string.Join(", ", cast);
        }
    }

    private static MatchConfidence ComputeConfidenceTmdb(TmdbSearchResult best, ParsedFileInfo info)
    {
        if (info.Year.HasValue && best.Year == info.Year.Value)
        {
            return MatchConfidence.High;
        }
        if (!info.Year.HasValue)
        {
            return MatchConfidence.Medium;
        }
        return MatchConfidence.Low;
    }
}
