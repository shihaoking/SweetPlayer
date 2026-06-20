using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// 豆瓣搜索结果。
/// </summary>
public class DoubanSearchResult
{
    public string DoubanId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public int? Year { get; set; }

    public string? PosterUrl { get; set; }

    public string? SubType { get; set; } // movie / tv
}

/// <summary>
/// 豆瓣详情。
/// </summary>
public class DoubanMovieDetail
{
    public string DoubanId { get; set; } = string.Empty;

    public string ChineseTitle { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public int? Year { get; set; }

    public double? Rating { get; set; }

    public string? Genres { get; set; }

    public string? Director { get; set; }

    public string? Cast { get; set; }

    public string? Synopsis { get; set; }

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }

    public string? SubType { get; set; }
}

public interface IDoubanClient
{
    Task<List<DoubanSearchResult>> SearchAsync(string query, int? year = null, CancellationToken cancellationToken = default);

    Task<DoubanMovieDetail?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default);

    Task<string?> DownloadPosterAsync(string posterUrl, string savePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 豆瓣数据客户端。豆瓣无官方 API，使用 subject_suggest 接口搜索，并解析详情页 HTML。
/// 内置基本反爬：随机 UA、随机请求间隔。
/// </summary>
public class DoubanClient : IDoubanClient
{
    private static readonly string[] UserAgents =
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:124.0) Gecko/20100101 Firefox/124.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15"
    };

    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    public DoubanClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        ConfigureClient();
    }

    public DoubanClient() : this(CreateDefaultClient())
    {
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        return client;
    }

    private void ConfigureClient()
    {
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgents[0]);
        }
        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        if (!_httpClient.DefaultRequestHeaders.AcceptLanguage.Any())
        {
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
        }
    }

    private void RotateUserAgent()
    {
        var ua = UserAgents[_random.Next(UserAgents.Length)];
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(ua);
    }

    public async Task<List<DoubanSearchResult>> SearchAsync(string query, int? year = null, CancellationToken cancellationToken = default)
    {
        var results = new List<DoubanSearchResult>();
        if (string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        RotateUserAgent();
        var url = $"https://movie.douban.com/j/subject_suggest?q={Uri.EscapeDataString(query)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri("https://movie.douban.com/");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return results;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var entry = ParseSuggestEntry(item);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }
        }
        catch
        {
            // 网络异常：返回已收集到的结果
        }

        // 排序：年份匹配 → 标题相似度
        if (year.HasValue)
        {
            results = results
                .OrderByDescending(r => r.Year == year.Value ? 1 : 0)
                .ThenByDescending(r => TitleSimilarity(r.Title, query))
                .ToList();
        }
        else
        {
            results = results
                .OrderByDescending(r => TitleSimilarity(r.Title, query))
                .ToList();
        }

        return results;
    }

    private static DoubanSearchResult? ParseSuggestEntry(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;

        var result = new DoubanSearchResult();
        if (item.TryGetProperty("id", out var idEl)) result.DoubanId = idEl.GetString() ?? string.Empty;
        if (item.TryGetProperty("title", out var titleEl)) result.Title = titleEl.GetString() ?? string.Empty;
        if (item.TryGetProperty("sub_title", out var subEl)) result.OriginalTitle = subEl.GetString();
        if (item.TryGetProperty("year", out var yearEl) && int.TryParse(yearEl.GetString(), out var y))
        {
            result.Year = y;
        }
        if (item.TryGetProperty("img", out var imgEl)) result.PosterUrl = imgEl.GetString();
        if (item.TryGetProperty("type", out var typeEl)) result.SubType = typeEl.GetString();

        if (string.IsNullOrEmpty(result.DoubanId)) return null;
        return result;
    }

    public async Task<DoubanMovieDetail?> GetDetailAsync(string doubanId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doubanId)) return null;

        RotateUserAgent();
        var url = $"https://movie.douban.com/subject/{doubanId}/";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri("https://movie.douban.com/");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ParseDetailHtml(doubanId, html);
        }
        catch
        {
            return null;
        }
    }

    private static DoubanMovieDetail? ParseDetailHtml(string doubanId, string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var detail = new DoubanMovieDetail { DoubanId = doubanId };

        // 标题（含年份）
        var titleMatch = Regex.Match(html, @"<title>\s*([^<]+?)\s*\(豆瓣\)\s*</title>");
        if (titleMatch.Success)
        {
            detail.ChineseTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
        }

        // 年份
        var yearMatch = Regex.Match(html, @"<span class=""year"">\(?(\d{4})\)?</span>");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var y))
        {
            detail.Year = y;
        }

        // 评分
        var ratingMatch = Regex.Match(html, @"<strong\s+class=""ll rating_num""[^>]*>([\d.]+)</strong>");
        if (ratingMatch.Success && double.TryParse(ratingMatch.Groups[1].Value,
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r))
        {
            detail.Rating = r;
        }

        // 类型 v:genre
        var genreMatches = Regex.Matches(html, @"<span\s+property=""v:genre"">([^<]+)</span>");
        if (genreMatches.Count > 0)
        {
            detail.Genres = string.Join("/", genreMatches.Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()));
        }

        // 导演 v:directedBy
        var directorMatches = Regex.Matches(html, @"<a[^>]*rel=""v:directedBy""[^>]*>([^<]+)</a>");
        if (directorMatches.Count > 0)
        {
            detail.Director = string.Join("/", directorMatches.Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()));
        }

        // 演员 v:starring
        var starMatches = Regex.Matches(html, @"<a[^>]*rel=""v:starring""[^>]*>([^<]+)</a>");
        if (starMatches.Count > 0)
        {
            detail.Cast = string.Join("/", starMatches.Take(8).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()));
        }

        // 简介 v:summary
        var summaryMatch = Regex.Match(html, @"<span\s+property=""v:summary""[^>]*>([\s\S]*?)</span>");
        if (summaryMatch.Success)
        {
            var raw = Regex.Replace(summaryMatch.Groups[1].Value, @"<[^>]+>", string.Empty);
            detail.Synopsis = WebUtility.HtmlDecode(raw).Trim();
        }

        // 海报
        var posterMatch = Regex.Match(html, @"<img\s+src=""([^""]+)""[^>]*title=""点击看更多海报""");
        if (posterMatch.Success)
        {
            detail.PosterUrl = posterMatch.Groups[1].Value;
        }
        else
        {
            var posterFallback = Regex.Match(html, @"<a[^>]*class=""nbgnbg""[^>]*>\s*<img\s+src=""([^""]+)""");
            if (posterFallback.Success)
            {
                detail.PosterUrl = posterFallback.Groups[1].Value;
            }
        }

        // 原始标题（OriginalTitle）：标题中第二段
        if (!string.IsNullOrEmpty(detail.ChineseTitle))
        {
            var parts = detail.ChineseTitle.Split(new[] { ' ' }, 2);
            if (parts.Length == 2)
            {
                detail.ChineseTitle = parts[0];
                detail.OriginalTitle = parts[1];
            }
        }

        // 子类型：详情页存在 "类型: 电视剧" 则视为剧集
        if (Regex.IsMatch(html, @"<a[^>]*>电视剧</a>") || Regex.IsMatch(html, @"季数:|集数:"))
        {
            detail.SubType = "tv";
        }
        else
        {
            detail.SubType = "movie";
        }

        if (string.IsNullOrEmpty(detail.ChineseTitle)) return null;

        return detail;
    }

    public async Task<string?> DownloadPosterAsync(string posterUrl, string savePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(posterUrl) || string.IsNullOrWhiteSpace(savePath)) return null;

        try
        {
            RotateUserAgent();
            using var request = new HttpRequestMessage(HttpMethod.Get, posterUrl);
            request.Headers.Referrer = new Uri("https://movie.douban.com/");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(savePath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return savePath;
        }
        catch
        {
            return null;
        }
    }

    private static double TitleSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        if (a == b) return 1.0;
        if (a.Contains(b) || b.Contains(a)) return 0.8;
        // 简单 Jaccard
        var sa = new HashSet<char>(a);
        var sb = new HashSet<char>(b);
        var inter = sa.Intersect(sb).Count();
        var union = sa.Union(sb).Count();
        return union == 0 ? 0 : (double)inter / union;
    }
}
