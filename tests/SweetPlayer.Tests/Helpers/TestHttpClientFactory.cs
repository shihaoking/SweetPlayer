using System.Net;
using System.Net.Http;

namespace SweetPlayer.Tests.Helpers;

/// <summary>
/// 测试用 HttpClient 工厂：将所有创建的 <see cref="HttpClient"/> 绑定到给定 <see cref="HttpMessageHandler"/>。
/// </summary>
internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public TestHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name)
    {
        // 多次创建共享同一 handler
        return new HttpClient(_handler, disposeHandler: false);
    }
}

/// <summary>
/// 测试用消息处理器：根据回调返回响应；可模拟超时等异常。
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public static StubHttpMessageHandler AlwaysThrow(Exception ex)
        => new((_, _) => Task.FromException<HttpResponseMessage>(ex));

    public static StubHttpMessageHandler Status(HttpStatusCode statusCode)
        => new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}
