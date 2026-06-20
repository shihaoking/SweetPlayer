using System.Net;
using System.Net.Http;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Security;
using SweetPlayer.Tests.Helpers;
using Xunit;

namespace SweetPlayer.Tests.Integration;

/// <summary>
/// WebDAV 文件源添加路径的集成测试：覆盖无效 URL 与连接异常处理。
/// </summary>
public class WebDavTests
{
    [Fact]
    public async Task WebDavSource_InvalidUrl_ShouldThrow()
    {
        using var dbFactory = new SqliteInMemoryDbContextFactory();
        var protector = new Base64PasswordProtector();
        var httpFactory = new TestHttpClientFactory(StubHttpMessageHandler.Status(HttpStatusCode.OK));
        var service = new MediaSourceService(dbFactory, httpFactory, protector);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddWebDavSourceAsync("not a url", "user", "pass", "无效源"));

        // 协议非 http(s) 也应拒绝
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddWebDavSourceAsync("ftp://example.com/dav/", "user", "pass", "FTP"));
    }

    [Fact]
    public async Task WebDavSource_ConnectionTimeout_ShouldHandleGracefully()
    {
        using var dbFactory = new SqliteInMemoryDbContextFactory();
        var protector = new Base64PasswordProtector();

        // 使用 Mock HttpClient 模拟超时（HttpRequestException 模拟网络层失败）
        var handler = StubHttpMessageHandler.AlwaysThrow(new HttpRequestException("simulated connection timeout"));
        var httpFactory = new TestHttpClientFactory(handler);
        var service = new MediaSourceService(dbFactory, httpFactory, protector);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddWebDavSourceAsync("https://example.com/dav/", "user", "pass", "WebDAV"));
        Assert.Contains("WebDAV", ex.Message);
    }

    [Fact]
    public async Task WebDavSource_Unauthorized_ShouldThrowInvalidOperation()
    {
        using var dbFactory = new SqliteInMemoryDbContextFactory();
        var protector = new Base64PasswordProtector();
        var handler = StubHttpMessageHandler.Status(HttpStatusCode.Unauthorized);
        var httpFactory = new TestHttpClientFactory(handler);
        var service = new MediaSourceService(dbFactory, httpFactory, protector);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddWebDavSourceAsync("https://example.com/dav/", "user", "wrong", "WebDAV"));
        Assert.Contains("凭据", ex.Message);
    }
}
