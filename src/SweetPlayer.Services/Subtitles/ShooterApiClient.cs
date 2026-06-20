using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 射手网 API 客户端实现。
/// </summary>
/// <remarks>
/// 射手网哈希算法：取文件 4 个位置（4096 / size/3*2 / size/3 / size-8192）各 4096 字节计算 MD5，
/// 4 个 MD5 用 ';' 连接作为 filehash 参数提交至 https://www.shooter.cn/api/subapi.php。
/// 网络异常时返回空列表，避免阻塞 UI。
/// </remarks>
public class ShooterApiClient : IShooterApiClient
{
    /// <summary>注册到 IHttpClientFactory 的命名客户端。</summary>
    public const string HttpClientName = "shooter";

    private const string SearchEndpoint = "https://www.shooter.cn/api/subapi.php";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShooterApiClient>? _logger;

    public ShooterApiClient(IHttpClientFactory httpClientFactory, ILogger<ShooterApiClient>? logger = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<OnlineSubtitleResult>> SearchByHashAsync(string videoFilePath)
    {
        if (string.IsNullOrWhiteSpace(videoFilePath) || !File.Exists(videoFilePath))
        {
            return new List<OnlineSubtitleResult>();
        }

        try
        {
            var hash = ComputeShooterHash(videoFilePath);
            if (string.IsNullOrEmpty(hash))
            {
                return new List<OnlineSubtitleResult>();
            }

            var fileName = Path.GetFileName(videoFilePath);
            var content = new MultipartFormDataContent
            {
                { new StringContent(fileName), "filename" },
                { new StringContent(hash), "filehash" },
                { new StringContent("Chn"), "lang" },
                { new StringContent("xml"), "format" },
            };

            return await PostAndParseAsync(content);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "射手网哈希搜索失败：{Path}", videoFilePath);
            return new List<OnlineSubtitleResult>();
        }
    }

    /// <inheritdoc />
    public async Task<List<OnlineSubtitleResult>> SearchByNameAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<OnlineSubtitleResult>();
        }

        try
        {
            var content = new MultipartFormDataContent
            {
                { new StringContent(query), "filename" },
                { new StringContent(string.Empty), "filehash" },
                { new StringContent("Chn"), "lang" },
                { new StringContent("xml"), "format" },
            };

            return await PostAndParseAsync(content);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "射手网名称搜索失败：{Query}", query);
            return new List<OnlineSubtitleResult>();
        }
    }

    /// <inheritdoc />
    public async Task<string> DownloadSubtitleAsync(string downloadUrl, string savePath)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("下载地址不能为空", nameof(downloadUrl));
        }
        if (string.IsNullOrWhiteSpace(savePath))
        {
            throw new ArgumentException("保存路径不能为空", nameof(savePath));
        }

        var dir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var client = CreateHttpClient();
        using var resp = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var fs = File.Create(savePath);
        await resp.Content.CopyToAsync(fs).ConfigureAwait(false);
        return savePath;
    }

    private async Task<List<OnlineSubtitleResult>> PostAndParseAsync(MultipartFormDataContent content)
    {
        var client = CreateHttpClient();
        using var resp = await client.PostAsync(SearchEndpoint, content).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _logger?.LogDebug("射手网返回非成功状态：{Code}", resp.StatusCode);
            return new List<OnlineSubtitleResult>();
        }

        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body) || body.Trim() == "\xff")
        {
            return new List<OnlineSubtitleResult>();
        }

        return ParseResponse(body);
    }

    /// <summary>
    /// 解析射手网响应；优先尝试 JSON（旧版返回），其次返回空列表。
    /// </summary>
    internal static List<OnlineSubtitleResult> ParseResponse(string body)
    {
        var list = new List<OnlineSubtitleResult>();
        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{"))
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var desc = item.TryGetProperty("Desc", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                foreach (var file in files.EnumerateArray())
                {
                    var ext = file.TryGetProperty("Ext", out var e) ? e.GetString() ?? string.Empty : string.Empty;
                    var link = file.TryGetProperty("Link", out var l) ? l.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrEmpty(link))
                    {
                        continue;
                    }

                    list.Add(new OnlineSubtitleResult
                    {
                        Title = desc,
                        Language = "chs",
                        DownloadUrl = link,
                        Format = ext.ToLowerInvariant(),
                        Rating = null,
                    });
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON 响应直接忽略
        }

        return list;
    }

    private HttpClient CreateHttpClient()
    {
        HttpClient client;
        try
        {
            client = _httpClientFactory.CreateClient(HttpClientName);
        }
        catch
        {
            client = _httpClientFactory.CreateClient();
        }

        if (client.Timeout == Timeout.InfiniteTimeSpan || client.Timeout > TimeSpan.FromSeconds(30))
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }

        if (!client.DefaultRequestHeaders.UserAgent.Any())
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SweetPlayer", "1.0"));
        }

        return client;
    }

    /// <summary>
    /// 计算射手网文件特征哈希。
    /// </summary>
    public static string ComputeShooterHash(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        var fi = new FileInfo(filePath);
        var size = fi.Length;
        const int blockSize = 4096;

        // 文件过小则无法采样 4 段，返回单段 MD5。
        if (size < blockSize * 2)
        {
            using var fs = File.OpenRead(filePath);
            var buf = new byte[(int)Math.Min(blockSize, size)];
            fs.ReadExactly(buf, 0, buf.Length);
            return Md5Hex(buf);
        }

        var offsets = new long[]
        {
            blockSize,
            size / 3 * 2,
            size / 3,
            Math.Max(0, size - 8192),
        };

        var sb = new StringBuilder();
        using (var fs = File.OpenRead(filePath))
        {
            var buffer = new byte[blockSize];
            for (var i = 0; i < offsets.Length; i++)
            {
                var offset = offsets[i];
                if (offset + blockSize > size)
                {
                    offset = Math.Max(0, size - blockSize);
                }

                fs.Seek(offset, SeekOrigin.Begin);
                var read = ReadFully(fs, buffer, blockSize);
                var slice = read == blockSize ? buffer : buffer.AsSpan(0, read).ToArray();
                if (i > 0) sb.Append(';');
                sb.Append(Md5Hex(slice));
            }
        }

        return sb.ToString();
    }

    private static int ReadFully(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }

    private static string Md5Hex(byte[] data)
    {
        var hash = MD5.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
