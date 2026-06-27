using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Detection;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Scraping;
using SweetPlayer.Services.Security;

namespace SweetPlayer.Services.Scanning;

/// <summary>
/// 媒体扫描器实现：本地通过 Directory.EnumerateFiles 递归，
/// WebDAV 通过 PROPFIND Depth:1 BFS 递归。
/// </summary>
public sealed class MediaScannerService : IMediaScannerService
{
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");

    private static readonly HashSet<string> DefaultExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".flv", ".ts", ".m2ts", ".webm", ".rmvb"
    };

    private readonly IDbContextFactory<SweetPlayerDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPasswordProtector _passwordProtector;
    private readonly IScrapingQueueService _scrapingQueue;
    private readonly IVideoAnalysisService _videoAnalysis;
    private readonly IHdrDetectionService _hdrDetection;
    private readonly ILogger<MediaScannerService> _logger;

    public MediaScannerService(
        IDbContextFactory<SweetPlayerDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IPasswordProtector passwordProtector,
        IScrapingQueueService scrapingQueue,
        IVideoAnalysisService videoAnalysis,
        IHdrDetectionService hdrDetection,
        ILogger<MediaScannerService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _passwordProtector = passwordProtector;
        _scrapingQueue = scrapingQueue;
        _videoAnalysis = videoAnalysis;
        _hdrDetection = hdrDetection;
        _logger = logger;
    }

    public IReadOnlyCollection<string> SupportedExtensions => DefaultExtensions;

    public async Task<IReadOnlyList<MediaScanResult>> ScanAllAsync(CancellationToken cancellationToken = default)
    {
        List<MediaSource> sources;
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            sources = await db.MediaSources.AsNoTracking()
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var results = new List<MediaScanResult>(sources.Count);
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ScanSourceAsync(source, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<MediaScanResult> ScanSourceAsync(MediaSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await SetScanStatusAsync(source.Id, ScanStatus.Scanning, cancellationToken).ConfigureAwait(false);

        try
        {
            IReadOnlyList<DiscoveredFile> discovered = source.Type switch
            {
                MediaSourceType.Local => EnumerateLocal(source, cancellationToken),
                MediaSourceType.WebDAV => await EnumerateWebDavAsync(source, cancellationToken).ConfigureAwait(false),
                _ => Array.Empty<DiscoveredFile>()
            };

            var (added, removed, total, newFileIds) = await SyncDatabaseAsync(source, discovered, cancellationToken).ConfigureAwait(false);

            await UpdateAfterScanAsync(source.Id, total, ScanStatus.Completed, error: false, cancellationToken).ConfigureAwait(false);

            // 如果有新增或删除的文件，触发刮削
            if (added > 0 || removed > 0)
            {
                _logger.LogInformation("扫描完成：新增 {Added} 个文件，移除 {Removed} 个文件，总计 {Total} 个文件", added, removed, total);

                if (newFileIds.Count > 0)
                {
                    _logger.LogInformation("将 {Count} 个新文件加入刮削队列", newFileIds.Count);
                    foreach (var fileId in newFileIds)
                    {
                        await _scrapingQueue.EnqueueAsync(fileId, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                _logger.LogInformation("扫描完成：没有文件变化，总计 {Total} 个文件", total);
            }

            return new MediaScanResult(source.Id, added, removed, total, HasError: false, ErrorMessage: null);
        }
        catch (OperationCanceledException)
        {
            await SetScanStatusAsync(source.Id, ScanStatus.Idle, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描媒体源 {SourceId} ({Path}) 失败", source.Id, source.Path);
            await UpdateAfterScanAsync(source.Id, fileCount: null, ScanStatus.Error, error: true, cancellationToken).ConfigureAwait(false);
            return new MediaScanResult(source.Id, 0, 0, 0, HasError: true, ErrorMessage: ex.Message);
        }
    }

    private IReadOnlyList<DiscoveredFile> EnumerateLocal(MediaSource source, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source.Path))
        {
            throw new DirectoryNotFoundException($"本地路径不存在：{source.Path}");
        }

        var results = new List<DiscoveredFile>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(source.Path, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext) || !DefaultExtensions.Contains(ext))
            {
                continue;
            }

            long size;
            try
            {
                size = new FileInfo(path).Length;
            }
            catch (Exception)
            {
                continue;
            }

            results.Add(new DiscoveredFile(
                FullPath: path,
                FileName: Path.GetFileName(path),
                FileSize: size,
                Container: ext.TrimStart('.').ToLowerInvariant()));
        }

        return results;
    }

    private async Task<IReadOnlyList<DiscoveredFile>> EnumerateWebDavAsync(MediaSource source, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(MediaSourceService.WebDavHttpClientName);

        var rootUri = new Uri(source.Path, UriKind.Absolute);
        var queue = new Queue<Uri>();
        queue.Enqueue(rootUri);

        var results = new List<DiscoveredFile>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var password = string.IsNullOrEmpty(source.Password)
            ? string.Empty
            : _passwordProtector.Unprotect(source.Password);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            if (!visited.Add(current.AbsoluteUri))
            {
                continue;
            }

            IReadOnlyList<WebDavEntry> entries;
            try
            {
                entries = await PropFindAsync(client, current, source.Username, password, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 单个子路径出错不应中断整个源的扫描，记录警告并继续
                _logger.LogWarning(ex, "跳过无法访问的 WebDAV 路径：{Path}", current.AbsoluteUri);
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsCollection)
                {
                    if (!UriEquals(entry.Uri, current))
                    {
                        queue.Enqueue(entry.Uri);
                    }
                    continue;
                }

                var fileName = Uri.UnescapeDataString(entry.Uri.Segments[^1]).TrimEnd('/');
                var ext = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(ext) || !DefaultExtensions.Contains(ext))
                {
                    continue;
                }

                results.Add(new DiscoveredFile(
                    FullPath: entry.Uri.AbsoluteUri,
                    FileName: fileName,
                    FileSize: entry.ContentLength,
                    Container: ext.TrimStart('.').ToLowerInvariant()));
            }
        }

        return results;
    }

    private static bool UriEquals(Uri a, Uri b)
    {
        return string.Equals(
            a.AbsoluteUri.TrimEnd('/'),
            b.AbsoluteUri.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<WebDavEntry>> PropFindAsync(
        HttpClient client,
        Uri uri,
        string? username,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(PropFindMethod, uri);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><D:propfind xmlns:D=\"DAV:\"><D:prop><D:resourcetype/><D:getcontentlength/></D:prop></D:propfind>",
            Encoding.UTF8,
            "application/xml");

        if (!string.IsNullOrEmpty(username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode != 207 && !response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("WebDAV 服务器拒绝凭据。");
            }

            // 404/410 等：路径不存在或服务器拒绝识别，作为空集合返回以跳过
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone)
            {
                return Array.Empty<WebDavEntry>();
            }

            throw new InvalidOperationException($"WebDAV PROPFIND 失败：HTTP {(int)response.StatusCode}");
        }

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseMultiStatus(xml, uri);
    }

    private static IReadOnlyList<WebDavEntry> ParseMultiStatus(string xml, Uri baseUri)
    {
        var entries = new List<WebDavEntry>();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return entries;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return entries;
        }

        XNamespace dav = "DAV:";
        foreach (var resp in doc.Descendants(dav + "response"))
        {
            var hrefValue = resp.Element(dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(hrefValue))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, hrefValue, out var entryUri))
            {
                continue;
            }

            var resourceType = resp.Descendants(dav + "resourcetype").FirstOrDefault();
            var isCollection = resourceType?.Element(dav + "collection") is not null;

            long.TryParse(
                resp.Descendants(dav + "getcontentlength").FirstOrDefault()?.Value,
                out var length);

            entries.Add(new WebDavEntry(entryUri, isCollection, length));
        }

        return entries;
    }

    private async Task<(int added, int removed, int total, List<int> newFileIds)> SyncDatabaseAsync(
        MediaSource source,
        IReadOnlyList<DiscoveredFile> discovered,
        CancellationToken cancellationToken)
    {
        var sourceId = source.Id;
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existing = await db.VideoFiles
            .Where(v => v.MediaSourceId == sourceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("文件源 {SourceId} 已存储文件数量：{Count}", sourceId, existing.Count);

        var existingByPath = existing.ToDictionary(v => v.FullPath, StringComparer.OrdinalIgnoreCase);
        var discoveredByPath = new Dictionary<string, DiscoveredFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in discovered)
        {
            discoveredByPath[file.FullPath] = file;
        }

        var added = 0;
        foreach (var file in discovered)
        {
            if (existingByPath.ContainsKey(file.FullPath))
            {
                var current = existingByPath[file.FullPath];
                if (current.FileSize != file.FileSize)
                {
                    current.FileSize = file.FileSize;
                }
                continue;
            }

            var videoFile = new VideoFile
            {
                MediaSourceId = sourceId,
                FileName = file.FileName,
                FullPath = file.FullPath,
                FileSize = file.FileSize,
                Container = file.Container,
                MediaType = MediaType.Unknown,
                DiscoveredAt = DateTime.UtcNow
            };

            // 执行 HDR / 杜比视界 / 杜比全景声检测（本地文件直接分析，WebDAV 通过 ffprobe HTTP 支持）
            var analyzeTarget = source.Type switch
            {
                MediaSourceType.Local when File.Exists(file.FullPath) => file.FullPath,
                MediaSourceType.WebDAV => BuildWebDavUrl(file.FullPath, source),
                _ => null
            };
            if (analyzeTarget != null)
            {
                await DetectHdrAndAtmosAsync(videoFile, analyzeTarget, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("识别文件源 {SourceId} 中的新文件，向DB中添加，文件名：{FileName}", sourceId, videoFile.FileName);
            db.VideoFiles.Add(videoFile);
            added++;
        }

        var toRemove = existing.Where(v => !discoveredByPath.ContainsKey(v.FullPath)).ToList();
        foreach (var item in toRemove)
        {
            _logger.LogDebug("识别到文件源 {SourceId} 里有已经删除的文件，从DB中删除，文件名：{FileName}", sourceId, item.FileName);
            db.VideoFiles.Remove(item);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 获取新添加的文件ID（SaveChanges 后才有ID）
        var newFileIds = new List<int>();
        if (added > 0)
        {
            var newFiles = await db.VideoFiles
                .Where(v => v.MediaSourceId == sourceId && v.MovieMetadataId == null)
                .OrderByDescending(v => v.DiscoveredAt)
                .Take(added)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            newFileIds.AddRange(newFiles);
        }

        return (added, toRemove.Count, discovered.Count, newFileIds);
    }

    /// <summary>
    /// 为 WebDAV URL 注入认证凭据，供 ffprobe 直接访问。
    /// </summary>
    private string? BuildWebDavUrl(string fileUrl, MediaSource source)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // 无凭据时直接使用原始 URL
        if (string.IsNullOrEmpty(source.Username))
        {
            return fileUrl;
        }

        var password = string.IsNullOrEmpty(source.Password)
            ? string.Empty
            : _passwordProtector.Unprotect(source.Password);

        // 将凭据嵌入 URL：http://user:pass@host/path
        var builder = new UriBuilder(uri)
        {
            UserName = Uri.EscapeDataString(source.Username),
            Password = Uri.EscapeDataString(password)
        };
        return builder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// 调用 FFprobe 分析视频流，并通过 HDR 检测服务填充 VideoFile 的 HDR / 杜比视界 / 杜比全景声字段。
    /// </summary>
    private async Task DetectHdrAndAtmosAsync(VideoFile videoFile, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var streamInfo = await _videoAnalysis.AnalyzeAsync(filePath).ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("FFprobe 分析完成：{FileName}，耗时 {ElapsedMs} ms", videoFile.FileName, sw.ElapsedMilliseconds);

            if (cancellationToken.IsCancellationRequested) return;

            // HDR / 杜比视界判定
            var hdrResult = _hdrDetection.Detect(streamInfo);
            videoFile.HasHDR = hdrResult.IsHdr;
            videoFile.HdrFormat = hdrResult.Format;
            videoFile.HasDolbyVision = hdrResult.Format == HdrFormat.DolbyVision;

            // 杜比全景声：遍历音频流检测
            videoFile.HasDolbyAtmos = streamInfo.AudioStreams.Any(a => a.IsAtmos);

            if (videoFile.HasHDR || videoFile.HasDolbyAtmos)
            {
                _logger.LogDebug("文件 {FileName}：HDR={Hdr}, 格式={Format}, Atmos={Atmos}",
                    videoFile.FileName, videoFile.HasHDR, videoFile.HdrFormat, videoFile.HasDolbyAtmos);
            }
        }
        catch (Exception ex)
        {
            // FFprobe 不可用或文件损坏不影响主流程，仅记录警告
            _logger.LogWarning(ex, "HDR/杜比检测失败，跳过：{FileName}", videoFile.FileName);
        }
    }

    private async Task SetScanStatusAsync(int sourceId, ScanStatus status, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.MediaSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }
        entity.ScanStatus = status;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateAfterScanAsync(int sourceId, int? fileCount, ScanStatus status, bool error, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await db.MediaSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.ScanStatus = status;
        entity.LastScanAt = DateTime.UtcNow;
        if (fileCount.HasValue)
        {
            entity.FileCount = fileCount.Value;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _ = error; // 仅用于占位，未来可记录错误细节
    }

    private sealed record DiscoveredFile(string FullPath, string FileName, long FileSize, string Container);

    private sealed record WebDavEntry(Uri Uri, bool IsCollection, long ContentLength);
}
