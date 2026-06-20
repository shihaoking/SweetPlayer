using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Data;

namespace SweetPlayer.Services.Scraping;

/// <summary>
/// 刮削队列配置。
/// </summary>
public class ScrapingQueueOptions
{
    /// <summary>请求间最小延迟（毫秒），默认 2000。</summary>
    public int MinDelayMs { get; set; } = 2000;

    /// <summary>请求间最大延迟（毫秒），默认 5000。</summary>
    public int MaxDelayMs { get; set; } = 5000;
}

public interface IScrapingQueueService
{
    /// <summary>
    /// 将视频文件加入待刮削队列。
    /// </summary>
    ValueTask EnqueueAsync(int videoFileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从数据库加载所有未刮削的视频文件并加入队列。
    /// </summary>
    Task LoadUnscrapedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动后台消费者。
    /// </summary>
    void Start(CancellationToken cancellationToken);

    /// <summary>
    /// 停止后台消费者。
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// 异步刮削队列：使用 Channel 缓冲请求，单消费者按可配置延迟处理；
/// 已刮削过的视频（即 MovieMetadataId 已绑定）跳过避免重复请求。
/// </summary>
public class ScrapingQueueService : IScrapingQueueService
{
    private readonly Channel<int> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly ScrapingQueueOptions _options;
    private readonly ILogger<ScrapingQueueService> _logger;
    private readonly Random _random = new();
    private readonly HashSet<int> _processed = new();
    private readonly object _processedLock = new();
    private CancellationTokenSource? _internalCts;
    private Task? _consumerTask;

    public ScrapingQueueService(
        IServiceProvider serviceProvider,
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        ILogger<ScrapingQueueService> logger,
        ScrapingQueueOptions? options = null)
    {
        _serviceProvider = serviceProvider;
        _dbFactory = dbFactory;
        _logger = logger;
        _options = options ?? new ScrapingQueueOptions();
        _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async ValueTask EnqueueAsync(int videoFileId, CancellationToken cancellationToken = default)
    {
        lock (_processedLock)
        {
            if (_processed.Contains(videoFileId))
            {
                return;
            }
        }
        await _channel.Writer.WriteAsync(videoFileId, cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadUnscrapedFilesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // 查询所有未刮削的视频文件（MovieMetadataId 为 null）
        var unscrapedFileIds = await db.VideoFiles
            .Where(v => v.MovieMetadataId == null)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unscrapedFileIds.Count > 0)
        {
            _logger.LogInformation("从数据库加载了 {Count} 个未刮削的视频文件，加入刮削队列", unscrapedFileIds.Count);
            foreach (var fileId in unscrapedFileIds)
            {
                await EnqueueAsync(fileId, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            _logger.LogInformation("数据库中没有未刮削的视频文件");
        }
    }

    public void Start(CancellationToken cancellationToken)
    {
        if (_consumerTask != null && !_consumerTask.IsCompleted) return;

        _logger.LogInformation("刮削队列服务已启动");
        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _internalCts.Token;
        _consumerTask = Task.Run(() => ConsumeAsync(token), token);
    }

    public async Task StopAsync()
    {
        _channel.Writer.TryComplete();
        if (_internalCts != null)
        {
            _internalCts.Cancel();
        }
        if (_consumerTask != null)
        {
            try { await _consumerTask.ConfigureAwait(false); } catch { }
        }
    }

    private async Task ConsumeAsync(CancellationToken token)
    {
        var successCount = 0;
        var failureCount = 0;
        var skippedCount = 0;

        try
        {
            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                while (_channel.Reader.TryRead(out var videoFileId))
                {
                    if (token.IsCancellationRequested) return;

                    lock (_processedLock)
                    {
                        if (_processed.Contains(videoFileId))
                        {
                            skippedCount++;
                            continue;
                        }
                    }

                    var alreadyDone = await IsAlreadyScrapedAsync(videoFileId, token).ConfigureAwait(false);
                    if (alreadyDone)
                    {
                        lock (_processedLock) { _processed.Add(videoFileId); }
                        skippedCount++;
                        _logger.LogDebug("跳过已刮削的视频文件 ID={VideoFileId}", videoFileId);
                        continue;
                    }

                    try
                    {
                        await using var scope = _serviceProvider.CreateAsyncScope();
                        var scrapingService = scope.ServiceProvider.GetService<IScrapingService>();
                        if (scrapingService != null)
                        {
                            var result = await scrapingService.ScrapeAsync(videoFileId, token).ConfigureAwait(false);
                            if (result != null)
                            {
                                successCount++;
                                _logger.LogInformation("刮削队列统计：成功 {Success} | 失败 {Failure} | 跳过 {Skipped}",
                                    successCount, failureCount, skippedCount);
                            }
                            else
                            {
                                failureCount++;
                                _logger.LogInformation("刮削队列统计：成功 {Success} | 失败 {Failure} | 跳过 {Skipped}",
                                    successCount, failureCount, skippedCount);
                            }
                        }
                        lock (_processedLock) { _processed.Add(videoFileId); }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "刮削视频文件失败 ID={VideoFileId}", videoFileId);
                        _logger.LogInformation("刮削队列统计：成功 {Success} | 失败 {Failure} | 跳过 {Skipped}",
                            successCount, failureCount, skippedCount);
                    }

                    var delay = _random.Next(_options.MinDelayMs, Math.Max(_options.MinDelayMs + 1, _options.MaxDelayMs));
                    try
                    {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        finally
        {
            _logger.LogInformation("刮削队列已停止。最终统计：成功 {Success} | 失败 {Failure} | 跳过 {Skipped}",
                successCount, failureCount, skippedCount);
        }
    }

    private async Task<bool> IsAlreadyScrapedAsync(int videoFileId, CancellationToken token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(token).ConfigureAwait(false);
        var record = await db.VideoFiles.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == videoFileId, token).ConfigureAwait(false);
        return record != null && record.MovieMetadataId.HasValue;
    }
}
