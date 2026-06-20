using SweetPlayer.Core.Models;
using SweetPlayer.Services.Subtitles;
using Xunit;

namespace SweetPlayer.Tests.Unit;

/// <summary>
/// 字幕发现与射手网哈希计算的单元测试。
/// </summary>
public class SubtitleTests
{
    [Fact]
    public void Discover_SameNameSrt_ShouldFind()
    {
        var dir = CreateTempDir();
        try
        {
            var videoPath = Path.Combine(dir, "movie.mkv");
            var srtPath = Path.Combine(dir, "movie.srt");
            File.WriteAllBytes(videoPath, Array.Empty<byte>());
            File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nhello\n");

            var service = new SubtitleDiscoveryService();
            var subs = service.DiscoverLocalSubtitles(videoPath);

            Assert.Single(subs);
            Assert.Equal(srtPath, subs[0].FilePath);
            Assert.Equal(SubtitleFormat.SRT, subs[0].Format);
            Assert.Equal(SubtitleSource.Local, subs[0].Source);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public void Discover_LanguageSuffix_ShouldParseLanguage()
    {
        var dir = CreateTempDir();
        try
        {
            var videoPath = Path.Combine(dir, "movie.mkv");
            var chsSrt = Path.Combine(dir, "movie.chs.srt");
            File.WriteAllBytes(videoPath, Array.Empty<byte>());
            File.WriteAllText(chsSrt, string.Empty);

            var service = new SubtitleDiscoveryService();
            var subs = service.DiscoverLocalSubtitles(videoPath);

            var hit = Assert.Single(subs);
            Assert.Equal("chs", hit.Language);
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    [Fact]
    public void ShooterHash_ShouldComputeCorrectly()
    {
        // 创建一个大于 8KB 的稳定内容文件，验证哈希计算结果稳定且包含 4 段 MD5。
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "deterministic.bin");
            var bytes = new byte[64 * 1024];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 251);
            }
            File.WriteAllBytes(path, bytes);

            var hash1 = ShooterApiClient.ComputeShooterHash(path);
            var hash2 = ShooterApiClient.ComputeShooterHash(path);

            Assert.False(string.IsNullOrEmpty(hash1));
            Assert.Equal(hash1, hash2);

            var segments = hash1.Split(';');
            Assert.Equal(4, segments.Length);
            Assert.All(segments, s => Assert.Equal(32, s.Length));
        }
        finally
        {
            CleanupTempDir(dir);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sweetplayer-subs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupTempDir(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* 忽略清理错误 */ }
    }
}
