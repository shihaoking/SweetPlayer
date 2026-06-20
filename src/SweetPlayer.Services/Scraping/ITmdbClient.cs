namespace SweetPlayer.Services.Scraping;

/// <summary>
/// TMDB API客户端接口
/// </summary>
public interface ITmdbClient
{
    /// <summary>
    /// 搜索电影或电视剧（中文）
    /// </summary>
    Task<List<TmdbSearchResult>> SearchAsync(string query, int? year = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取电影详情（中文）
    /// </summary>
    Task<TmdbMovieDetail?> GetDetailAsync(string tmdbId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 构建完整的海报URL
    /// </summary>
    string GetPosterUrl(string posterPath);
}
