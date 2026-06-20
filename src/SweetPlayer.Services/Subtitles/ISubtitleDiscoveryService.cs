using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 本地字幕自动发现服务。
/// </summary>
public interface ISubtitleDiscoveryService
{
    /// <summary>
    /// 在视频文件所在目录及常见子目录（Subs/Subtitles）中查找匹配的字幕文件。
    /// </summary>
    /// <param name="videoFilePath">视频文件完整路径。</param>
    /// <returns>发现的本地字幕条目集合（可能为空）。</returns>
    List<SubtitleFileInfo> DiscoverLocalSubtitles(string videoFilePath);
}
