using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Security;

namespace SweetPlayer.Services.Browse;

/// <summary>
/// 目录浏览服务：提供本地和 WebDAV 文件源的单层目录列举功能
/// </summary>
public sealed class DirectoryBrowseService : IDirectoryBrowseService
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".ts", ".m2ts", ".webm", ".rmvb"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPasswordProtector _passwordProtector;
    private readonly ILogger<DirectoryBrowseService> _logger;

    public DirectoryBrowseService(
        IHttpClientFactory httpClientFactory,
        IPasswordProtector passwordProtector,
        ILogger<DirectoryBrowseService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _passwordProtector = passwordProtector;
        _logger = logger;
    }

    public async Task<List<BrowseEntry>> ListDirectoryAsync(MediaSource source, string relativePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Type switch
        {
            MediaSourceType.Local => ListLocalDirectory(source, relativePath, ct),
            MediaSourceType.WebDAV => await ListWebDavDirectoryAsync(source, relativePath, ct).ConfigureAwait(false),
            _ => throw new NotSupportedException($"不支持的媒体源类型: {source.Type}")
        };
    }

    private List<BrowseEntry> ListLocalDirectory(MediaSource source, string relativePath, CancellationToken ct)
    {
        var fullPath = string.IsNullOrEmpty(relativePath)
            ? source.Path
            : Path.Combine(source.Path, relativePath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"目录不存在：{fullPath}");
        }

        var entries = new List<BrowseEntry>();

        // 列举文件夹
        try
        {
            foreach (var dirPath in Directory.EnumerateDirectories(fullPath))
            {
                ct.ThrowIfCancellationRequested();

                var dirName = Path.GetFileName(dirPath);
                if (string.IsNullOrEmpty(dirName) || dirName.StartsWith('.'))
                {
                    continue; // 跳过隐藏文件夹
                }

                var relPath = string.IsNullOrEmpty(relativePath)
                    ? dirName
                    : Path.Combine(relativePath, dirName);

                entries.Add(new BrowseEntry
                {
                    Name = dirName,
                    RelativePath = relPath,
                    IsDirectory = true,
                    FileSize = null
                });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "无权访问目录：{Path}", fullPath);
        }

        // 列举视频文件
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(fullPath))
            {
                ct.ThrowIfCancellationRequested();

                var fileName = Path.GetFileName(filePath);
                var ext = Path.GetExtension(fileName);

                if (string.IsNullOrEmpty(ext) || !VideoExtensions.Contains(ext))
                {
                    continue; // 仅视频文件
                }

                long fileSize = 0;
                try
                {
                    fileSize = new FileInfo(filePath).Length;
                }
                catch (Exception)
                {
                    // 忽略无法获取大小的文件
                }

                var relPath = string.IsNullOrEmpty(relativePath)
                    ? fileName
                    : Path.Combine(relativePath, fileName);

                entries.Add(new BrowseEntry
                {
                    Name = fileName,
                    RelativePath = relPath,
                    IsDirectory = false,
                    FileSize = fileSize
                });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "无权访问目录文件：{Path}", fullPath);
        }

        // 排序：文件夹在前，文件在后
        return entries
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<BrowseEntry>> ListWebDavDirectoryAsync(MediaSource source, string relativePath, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(MediaSourceService.WebDavHttpClientName);

        var baseUri = new Uri(source.Path, UriKind.Absolute);

        // 构建目标 URI
        Uri targetUri;
        if (string.IsNullOrEmpty(relativePath))
        {
            targetUri = baseUri;
        }
        else
        {
            // 将反斜杠转换为斜杠，并逐段编码路径
            var pathSegments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var encodedPath = string.Join('/', pathSegments.Select(Uri.EscapeDataString));
            targetUri = new Uri(baseUri, encodedPath);
        }

        // 确保目录 URI 以 / 结尾
        if (!targetUri.AbsolutePath.EndsWith('/'))
        {
            targetUri = new Uri(targetUri.ToString() + '/');
        }

        _logger.LogInformation("WebDAV 浏览请求: {Uri}", targetUri);

        var request = new HttpRequestMessage(PropFindMethod, targetUri);
        request.Headers.Add("Depth", "1");

        // 认证
        var password = string.IsNullOrEmpty(source.Password)
            ? string.Empty
            : _passwordProtector.Unprotect(source.Password);

        if (!string.IsNullOrEmpty(source.Username))
        {
            var authBytes = Encoding.UTF8.GetBytes($"{source.Username}:{password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            _logger.LogInformation("使用用户名认证: {Username}", source.Username);
        }

        // PROPFIND 请求体
        const string propfindXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <D:propfind xmlns:D="DAV:">
              <D:prop>
                <D:displayname/>
                <D:getcontentlength/>
                <D:resourcetype/>
              </D:prop>
            </D:propfind>
            """;

        request.Content = new StringContent(propfindXml, Encoding.UTF8, "application/xml");

        var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        _logger.LogInformation("WebDAV 响应状态: {StatusCode}", response.StatusCode);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("WebDAV 认证失败");
        }

        response.EnsureSuccessStatusCode();

        var responseXml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("WebDAV 响应 XML: {Xml}", responseXml);

        var result = ParseWebDavResponse(responseXml, targetUri, relativePath);
        _logger.LogInformation("WebDAV 解析结果: {Count} 个条目", result.Count);

        return result;
    }

    private List<BrowseEntry> ParseWebDavResponse(string xml, Uri requestUri, string relativePath)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("DAV:");

        var entries = new List<BrowseEntry>();

        _logger.LogInformation("开始解析 WebDAV 响应，请求 URI: {Uri}", requestUri.AbsolutePath);

        foreach (var responseNode in doc.Descendants(ns + "response"))
        {
            var hrefNode = responseNode.Element(ns + "href");
            if (hrefNode == null)
            {
                _logger.LogWarning("跳过：没有 href 节点");
                continue;
            }

            var href = Uri.UnescapeDataString(hrefNode.Value);
            _logger.LogDebug("处理条目: {Href}", href);

            // 跳过当前目录本身
            if (href.TrimEnd('/').Equals(requestUri.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("跳过当前目录本身: {Href}", href);
                continue;
            }

            var propStatNode = responseNode.Element(ns + "propstat");
            if (propStatNode == null)
            {
                _logger.LogWarning("跳过：没有 propstat 节点，href: {Href}", href);
                continue;
            }

            var propNode = propStatNode.Element(ns + "prop");
            if (propNode == null)
            {
                _logger.LogWarning("跳过：没有 prop 节点，href: {Href}", href);
                continue;
            }

            // 判断是否为目录
            var resourceTypeNode = propNode.Element(ns + "resourcetype");
            var isDirectory = resourceTypeNode?.Element(ns + "collection") != null;

            // 获取名称
            var displayNameNode = propNode.Element(ns + "displayname");
            var name = displayNameNode?.Value ?? Path.GetFileName(href.TrimEnd('/'));

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("跳过：名称为空，href: {Href}", href);
                continue;
            }

            _logger.LogDebug("条目名称: {Name}, 是否目录: {IsDirectory}", name, isDirectory);

            // 如果是文件，检查是否为视频文件
            if (!isDirectory)
            {
                var ext = Path.GetExtension(name);
                if (string.IsNullOrEmpty(ext) || !VideoExtensions.Contains(ext))
                {
                    _logger.LogDebug("跳过非视频文件: {Name}, 扩展名: {Ext}", name, ext);
                    continue;
                }
            }

            // 获取文件大小
            long? fileSize = null;
            if (!isDirectory)
            {
                var contentLengthNode = propNode.Element(ns + "getcontentlength");
                if (contentLengthNode != null && long.TryParse(contentLengthNode.Value, out var size))
                {
                    fileSize = size;
                }
            }

            // 构建相对路径
            var entryRelPath = string.IsNullOrEmpty(relativePath)
                ? name
                : Path.Combine(relativePath, name);

            entries.Add(new BrowseEntry
            {
                Name = name,
                RelativePath = entryRelPath,
                IsDirectory = isDirectory,
                FileSize = fileSize
            });

            _logger.LogInformation("添加条目: {Name}, 类型: {Type}", name, isDirectory ? "目录" : "文件");
        }

        _logger.LogInformation("解析完成，共 {Count} 个条目", entries.Count);

        // 排序：文件夹在前，文件在后
        return entries
            .OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
