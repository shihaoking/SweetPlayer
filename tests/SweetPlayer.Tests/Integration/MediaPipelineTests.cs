using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Scanning;
using SweetPlayer.Services.Scraping;
using SweetPlayer.Services.Security;
using SweetPlayer.Services.Detection;
using SweetPlayer.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SweetPlayer.Tests.Integration;

/// <summary>
/// 媒体管道集成测试：覆盖文件源添加、扫描、文件名解析、标签过滤等核心流程。
/// </summary>
public class MediaPipelineTests
{
    [Fact]
    public async Task AddLocalSource_Scan_ShouldDiscoverVideoFiles()
    {
        // 准备：临时目录 + 三个空 .mkv 文件 + 一个干扰 .txt
        var tempDir = Path.Combine(Path.GetTempPath(), "sweetplayer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllBytes(Path.Combine(tempDir, "Interstellar (2014).mkv"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(tempDir, "Breaking.Bad.S01E02.720p.BluRay.mkv"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(tempDir, "movie.mp4"), Array.Empty<byte>());
            File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "ignored");

            using var dbFactory = new SqliteInMemoryDbContextFactory();
            var protector = new Base64PasswordProtector();
            var httpFactory = new TestHttpClientFactory(StubHttpMessageHandler.Status(System.Net.HttpStatusCode.OK));

            var sourceService = new MediaSourceService(dbFactory, httpFactory, protector);
            var mockScrapingQueue = new MockScrapingQueueService();
            var scanner = new MediaScannerService(dbFactory, httpFactory, protector, mockScrapingQueue,
                new VideoAnalysisService(), new HdrDetectionService(), NullLogger<MediaScannerService>.Instance);

            // 行为：添加本地源 → 扫描
            var source = await sourceService.AddLocalSourceAsync(tempDir, "测试源");
            Assert.NotEqual(0, source.Id);
            Assert.Equal(MediaSourceType.Local, source.Type);

            var result = await scanner.ScanSourceAsync(source);

            // 断言：发现 3 个视频文件
            Assert.False(result.HasError);
            Assert.Equal(3, result.AddedCount);
            Assert.Equal(3, result.TotalCount);

            await using var db = dbFactory.CreateDbContext();
            var videos = await db.VideoFiles.AsNoTracking().ToListAsync();
            Assert.Equal(3, videos.Count);
            Assert.All(videos, v => Assert.Equal(source.Id, v.MediaSourceId));
            Assert.Contains(videos, v => v.FileName == "Interstellar (2014).mkv");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* 忽略清理错误 */ }
        }
    }

    [Fact]
    public void FileNameParser_ShouldParseStandardMovieFormat()
    {
        var parser = new FileNameParser();
        var result = parser.Parse("Interstellar (2014).mkv");

        Assert.Equal(MediaType.Movie, result.MediaType);
        Assert.Equal("Interstellar", result.Title);
        Assert.Equal(2014, result.Year);
    }

    [Fact]
    public void FileNameParser_ShouldParseTVShowSxxExx()
    {
        var parser = new FileNameParser();
        var result = parser.Parse("Breaking.Bad.S01E02.720p.BluRay.mkv");

        Assert.Equal(MediaType.TVEpisode, result.MediaType);
        Assert.Equal(1, result.Season);
        Assert.Equal(2, result.Episode);
        // 标题部分以剧名开头（允许后续标准化保留下划线/点已被替换为空格）
        Assert.StartsWith("Breaking", result.Title);
        Assert.Contains("Bad", result.Title);
    }

    [Fact]
    public void TagFilter_ShouldRemoveResolutionAndCodecTags()
    {
        // 验证 1080p、x265、HEVC、BluRay、Atmos 等标签均被移除
        const string raw = "Inception 1080p x265 BluRay DTS-HD Atmos-RARBG";
        var stripped = TagFilter.StripTags(raw);

        Assert.DoesNotContain("1080p", stripped);
        Assert.DoesNotContain("x265", stripped);
        Assert.DoesNotContain("BluRay", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTS-HD", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Atmos", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RARBG", stripped, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Inception", stripped.Trim());
    }

    [Fact]
    public void HdrDetection_ShouldDetectHDR10()
    {
        var service = new SweetPlayer.Services.Detection.HdrDetectionService();
        var info = new VideoStreamInfo
        {
            ColorTransfer = "smpte2084",
            ColorPrimaries = "bt2020",
            BitDepth = 10,
        };

        var result = service.Detect(info);

        Assert.True(result.IsHdr);
        Assert.Equal(HdrFormat.HDR10, result.Format);
    }
}

/// <summary>
/// Mock 刮削队列服务用于测试
/// </summary>
internal class MockScrapingQueueService : IScrapingQueueService
{
    public ValueTask EnqueueAsync(int videoFileId, CancellationToken cancellationToken = default)
    {
        // 测试中不执行实际刮削
        return ValueTask.CompletedTask;
    }

    public Task LoadUnscrapedFilesAsync(CancellationToken cancellationToken = default)
    {
        // 测试中不需要加载
        return Task.CompletedTask;
    }

    public void Start(CancellationToken cancellationToken)
    {
        // 测试中不需要启动
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
