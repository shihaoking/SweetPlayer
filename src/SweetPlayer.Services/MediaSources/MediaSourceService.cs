using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Security;

namespace SweetPlayer.Services.MediaSources;

/// <summary>
/// 媒体源管理服务实现：本地路径校验 + WebDAV PROPFIND 校验。
/// </summary>
public sealed class MediaSourceService : IMediaSourceService
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");

    /// <summary>
    /// 常见 WebDAV 端点路径后缀。当用户仅输入根地址时，按顺序自动尝试这些路径以发现真实端点。
    /// 第一项为空字符串，表示先尝试用户输入的原始路径。
    /// </summary>
    private static readonly string[] CommonWebDavPaths = new[]
    {
        string.Empty,         // 用户输入的原始路径
        "/webdav",
        "/dav",
        "/remote.php/dav",    // Nextcloud
        "/remote.php/webdav", // ownCloud
    };

    private readonly IDbContextFactory<SweetPlayerDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPasswordProtector _passwordProtector;

    public MediaSourceService(
        IDbContextFactory<SweetPlayerDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IPasswordProtector passwordProtector)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _passwordProtector = passwordProtector;
    }

    public async Task<MediaSource> AddLocalSourceAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("路径不能为空。", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("名称不能为空。", nameof(name));
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"本地路径不存在：{path}");
        }

        var fullPath = Path.GetFullPath(path);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.MediaSources
            .FirstOrDefaultAsync(s => s.Type == MediaSourceType.Local && s.Path == fullPath, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"该本地路径已被添加为文件源：{fullPath}");
        }

        var source = new MediaSource
        {
            Name = name.Trim(),
            Type = MediaSourceType.Local,
            Path = fullPath,
            Username = null,
            Password = null,
            CreatedAt = DateTime.UtcNow,
            LastScanAt = DateTime.MinValue,
            FileCount = 0,
            ScanStatus = ScanStatus.Idle
        };

        db.MediaSources.Add(source);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return source;
    }

    public async Task<MediaSource> AddWebDavSourceAsync(string url, string username, string password, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL 不能为空。", nameof(url));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("名称不能为空。", nameof(name));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("WebDAV URL 必须是 http(s) 绝对地址。", nameof(url));
        }

        var normalized = NormalizeWebDavUrl(uri);

        // 自动检测有效的 WebDAV 端点（可能与用户输入不同）
        var effectiveUrl = await ValidateWebDavConnectionAsync(normalized, username, password, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.MediaSources
            .FirstOrDefaultAsync(s => s.Type == MediaSourceType.WebDAV && s.Path == effectiveUrl, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"该 WebDAV URL 已被添加为文件源：{effectiveUrl}");
        }

        var source = new MediaSource
        {
            Name = name.Trim(),
            Type = MediaSourceType.WebDAV,
            Path = effectiveUrl,
            Username = string.IsNullOrEmpty(username) ? null : username,
            Password = string.IsNullOrEmpty(password) ? null : _passwordProtector.Protect(password),
            CreatedAt = DateTime.UtcNow,
            LastScanAt = DateTime.MinValue,
            FileCount = 0,
            ScanStatus = ScanStatus.Idle
        };

        db.MediaSources.Add(source);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return source;
    }

    public async Task<IReadOnlyList<MediaSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.MediaSources
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> RemoveSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var source = await db.MediaSources
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return false;
        }

        db.MediaSources.Remove(source);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 验证 WebDAV 连接，支持自动检测端点路径。
    /// 当 PROPFIND 在原始 URL 上返回 404 时，会依次尝试常见的 WebDAV 端点（如 /webdav、/dav、Nextcloud / ownCloud 默认路径）。
    /// 返回首个成功响应的实际有效 URL；若全部失败则抛出包含尝试列表的诊断信息。
    /// </summary>
    private async Task<string> ValidateWebDavConnectionAsync(
        string url, string username, string password, CancellationToken cancellationToken)
    {
        var triedUrls = new List<string>();
        HttpStatusCode lastStatus = HttpStatusCode.NotFound;
        string? lastReason = null;

        foreach (var suffix in CommonWebDavPaths)
        {
            var candidate = BuildCandidateUrl(url, suffix);
            triedUrls.Add(candidate);

            using var response = await SendPropFindAsync(candidate, username, password, cancellationToken).ConfigureAwait(false);

            // 凭据错误：直接报错，不必重试其他路径
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("WebDAV 服务器拒绝凭据，请检查用户名/密码。");
            }

            // 207 Multi-Status 是 WebDAV 标准响应；200/301 等也视为可达
            if (response.IsSuccessStatusCode || (int)response.StatusCode == 207)
            {
                return candidate;
            }

            lastStatus = response.StatusCode;
            lastReason = response.ReasonPhrase;

            // 仅当 404 时继续尝试下一个候选端点；其余错误直接终止
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                break;
            }
        }

        var message = lastStatus switch
        {
            HttpStatusCode.NotFound =>
                "WebDAV 端点未找到 (404)。已尝试以下路径均失败：\n" +
                string.Join("\n", triedUrls) +
                "\n\n请确认 WebDAV 服务的完整 URL 路径。",
            HttpStatusCode.MethodNotAllowed =>
                "服务器不支持 WebDAV (PROPFIND 方法被拒绝)。请确认该服务器已启用 WebDAV 功能。",
            _ => $"WebDAV 连接失败：HTTP {(int)lastStatus} {lastReason}"
        };

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 发送一次 PROPFIND（Depth: 0）请求，封装网络异常为统一的 InvalidOperationException。
    /// </summary>
    private async Task<HttpResponseMessage> SendPropFindAsync(
        string url, string username, string password, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(WebDavHttpClientName);

        using var request = new HttpRequestMessage(PropFindMethod, url);
        request.Headers.Add("Depth", "0");
        request.Content = new StringContent(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:propfind xmlns:D=\"DAV:\"><D:prop><D:resourcetype/></D:prop></D:propfind>",
            System.Text.Encoding.UTF8,
            "application/xml");

        if (!string.IsNullOrEmpty(username))
        {
            var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
        }

        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"无法连接 WebDAV 服务器：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 拼接候选 WebDAV URL：保持用户输入的原始 URL 不变（suffix 为空），否则在去尾斜杠后追加 suffix 并补齐尾斜杠。
    /// </summary>
    private static string BuildCandidateUrl(string baseUrl, string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
        {
            return baseUrl;
        }

        var trimmed = baseUrl.TrimEnd('/');
        var combined = trimmed + suffix;
        return combined.EndsWith("/", StringComparison.Ordinal) ? combined : combined + "/";
    }

    private static string NormalizeWebDavUrl(Uri uri)
    {
        var raw = uri.GetLeftPart(UriPartial.Path);
        return raw.EndsWith("/", StringComparison.Ordinal) ? raw : raw + "/";
    }

    /// <summary>
    /// HttpClientFactory 中用于 WebDAV 请求的命名客户端。
    /// </summary>
    public const string WebDavHttpClientName = "SweetPlayer.WebDav";
}
