using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Detection;

/// <summary>
/// 视频流元数据分析服务，使用 FFprobe 提取容器、视频流和音频流信息。
/// </summary>
public interface IVideoAnalysisService
{
    /// <summary>
    /// 分析指定视频文件，返回流信息。
    /// </summary>
    /// <param name="filePath">视频文件本地路径。</param>
    /// <returns>视频流元数据；若分析失败将返回空结构。</returns>
    Task<VideoStreamInfo> AnalyzeAsync(string filePath);
}

/// <summary>
/// VideoAnalysisService 配置项。
/// </summary>
public class VideoAnalysisOptions
{
    /// <summary>
    /// FFprobe 可执行文件路径。若为空则按 PATH 查找 "ffprobe"。
    /// </summary>
    public string? FfprobePath { get; set; }

    /// <summary>
    /// 单次分析超时（毫秒）。默认 30 秒。
    /// </summary>
    public int TimeoutMs { get; set; } = 30_000;
}
