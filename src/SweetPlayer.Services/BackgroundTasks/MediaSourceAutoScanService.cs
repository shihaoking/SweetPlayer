using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SweetPlayer.Services.Scanning;

namespace SweetPlayer.Services.BackgroundTasks;

/// <summary>
/// 定时自动重扫描后台服务：每 30 分钟触发一次全部媒体源的增量扫描。
/// </summary>
public sealed class MediaSourceAutoScanService : IHostedService, IAsyncDisposable
{
    /// <summary>
    /// 默认扫描间隔。
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 启动后首次延迟，避免与应用启动初始化抢占。
    /// </summary>
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    private readonly IMediaScannerService _scanner;
    private readonly ILogger<MediaSourceAutoScanService> _logger;
    private readonly CancellationTokenSource _cts = new();

    private Task? _runningTask;

    public MediaSourceAutoScanService(
        IMediaScannerService scanner,
        ILogger<MediaSourceAutoScanService> logger)
    {
        _scanner = scanner;
        _logger = logger;
    }

    public TimeSpan Interval { get; set; } = DefaultInterval;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _runningTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runningTask is null)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            await _runningTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                _logger.LogInformation("开始自动扫描所有媒体源");
                var results = await _scanner.ScanAllAsync(stoppingToken).ConfigureAwait(false);
                foreach (var r in results)
                {
                    if (r.HasError)
                    {
                        _logger.LogWarning("自动扫描媒体源 {SourceId} 失败：{Error}", r.SourceId, r.ErrorMessage);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "自动扫描媒体源 {SourceId} 完成：新增 {Added}，移除 {Removed}，总计 {Total}",
                            r.SourceId, r.AddedCount, r.RemovedCount, r.TotalCount);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动扫描循环发生异常");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _cts.Dispose();
    }
}
