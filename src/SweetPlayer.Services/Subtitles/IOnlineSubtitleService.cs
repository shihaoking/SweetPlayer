using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 在线字幕搜索 / 下载 / 缓存服务。
/// </summary>
public interface IOnlineSubtitleService
{
    /// <summary>
    /// 搜索在线字幕：先按文件哈希，失败则按文件名 / 中文片名。
    /// </summary>
    /// <param name="videoFilePath">视频文件路径。</param>
    /// <param name="chineseTitle">可选中文片名（用于 fallback 搜索）。</param>
    Task<List<OnlineSubtitleResult>> SearchSubtitlesAsync(string videoFilePath, string? chineseTitle = null);

    /// <summary>
    /// 下载并缓存指定的在线字幕，返回本地缓存信息。
    /// </summary>
    /// <param name="result">要下载的在线字幕条目。</param>
    /// <param name="videoFileName">视频文件名（用于构造缓存子目录）。</param>
    Task<SubtitleFileInfo> DownloadAndCacheAsync(OnlineSubtitleResult result, string videoFileName);
}
