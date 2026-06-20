using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Scanning;

/// <summary>
/// 单次扫描结果摘要，用于上层（UI/日志）展示。
/// </summary>
public sealed record MediaScanResult(
    int SourceId,
    int AddedCount,
    int RemovedCount,
    int TotalCount,
    bool HasError,
    string? ErrorMessage);

/// <summary>
/// 媒体扫描器：递归遍历本地或 WebDAV 文件源，按扩展名过滤视频文件，
/// 将新增文件写入 VideoFile 表，并标记/移除已删除的文件。
/// </summary>
public interface IMediaScannerService
{
    /// <summary>
    /// 受支持的视频文件扩展名（小写，含点号）。
    /// </summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// 扫描单个媒体源，仅处理增量变化。
    /// </summary>
    Task<MediaScanResult> ScanSourceAsync(MediaSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫描全部媒体源。
    /// </summary>
    Task<IReadOnlyList<MediaScanResult>> ScanAllAsync(CancellationToken cancellationToken = default);
}
