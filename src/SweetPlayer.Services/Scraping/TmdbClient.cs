using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// TMDB API客户端实现
/// </summary>
public class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;
    private readonly ILogger<TmdbClient> _logger;
    private const string ChineseLanguage = "zh-CN";

    public TmdbClient(
        HttpClient httpClient,
        IOptions<TmdbOptions> options,
        ILogger<TmdbClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("TMDB API Key未配置，TMDB功能将不可用");
        }
    }

    public async Task<List<TmdbSearchResult>> SearchAsync(string query, int? year = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("TMDB API Key未配置，跳过TMDB搜索");
            return new List<TmdbSearchResult>();
        }

        try
        {
            var url = $"{_options.BaseUrl}/search/movie?api_key={_options.ApiKey}&language={ChineseLanguage}&query={Uri.EscapeDataString(query)}";

            if (year.HasValue)
            {
                url += $"&year={year.Value}";
            }

            _logger.LogInformation("TMDB搜索：查询=\"{Query}\", 年份={Year}", query, year);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB API速率限制触发（429），请求过于频繁");
                }
                else
                {
                    _logger.LogError("TMDB搜索失败：HTTP {StatusCode}", response.StatusCode);
                }
                return new List<TmdbSearchResult>();
            }

            var searchResponse = await response.Content.ReadFromJsonAsync<TmdbSearchResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (searchResponse?.Results == null || searchResponse.Results.Count == 0)
            {
                _logger.LogInformation("TMDB搜索无结果：查询=\"{Query}\"", query);
                return new List<TmdbSearchResult>();
            }

            _logger.LogInformation("TMDB搜索返回 {Count} 个结果", searchResponse.Results.Count);
            return searchResponse.Results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "TMDB搜索HTTP请求失败：查询=\"{Query}\"", query);
            return new List<TmdbSearchResult>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "TMDB搜索响应解析失败：查询=\"{Query}\"", query);
            return new List<TmdbSearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TMDB搜索发生未知错误：查询=\"{Query}\"", query);
            return new List<TmdbSearchResult>();
        }
    }

    public async Task<TmdbMovieDetail?> GetDetailAsync(string tmdbId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("TMDB API Key未配置，跳过TMDB详情获取");
            return null;
        }

        try
        {
            var url = $"{_options.BaseUrl}/movie/{tmdbId}?api_key={_options.ApiKey}&language={ChineseLanguage}&append_to_response=credits";

            _logger.LogInformation("TMDB获取详情：ID={TmdbId}", tmdbId);

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("TMDB API速率限制触发（429），等待后重试");
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);

                    // 重试一次
                    response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("TMDB详情获取失败（重试后）：HTTP {StatusCode}", response.StatusCode);
                        return null;
                    }
                }
                else
                {
                    _logger.LogError("TMDB详情获取失败：HTTP {StatusCode}", response.StatusCode);
                    return null;
                }
            }

            var detail = await response.Content.ReadFromJsonAsync<TmdbMovieDetail>(cancellationToken: cancellationToken).ConfigureAwait(false);

            if (detail == null)
            {
                _logger.LogWarning("TMDB详情解析为空：ID={TmdbId}", tmdbId);
                return null;
            }

            _logger.LogInformation("TMDB详情获取成功：{Title} ({Year})", detail.Title, detail.Year);
            return detail;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "TMDB详情HTTP请求失败：ID={TmdbId}", tmdbId);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "TMDB详情响应解析失败：ID={TmdbId}", tmdbId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TMDB详情获取发生未知错误：ID={TmdbId}", tmdbId);
            return null;
        }
    }

    public string GetPosterUrl(string posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
        {
            return string.Empty;
        }

        // 移除开头的斜杠（如果有）
        posterPath = posterPath.TrimStart('/');

        return $"{_options.ImageBaseUrl}/{posterPath}";
    }
}

/// <summary>
/// TMDB配置选项
/// </summary>
public class TmdbOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3";
    public string ImageBaseUrl { get; set; } = "https://image.tmdb.org/t/p/w500";
}
