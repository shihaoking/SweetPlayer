using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 射手网（shooter.cn）字幕 API 客户端。
/// </summary>
public interface IShooterApiClient
{
    /// <summary>
    /// 通过文件特征哈希在射手网搜索字幕。
    /// </summary>
    Task<List<OnlineSubtitleResult>> SearchByHashAsync(string videoFilePath);

    /// <summary>
    /// 通过文件名 / 中文片名搜索字幕（fallback）。
    /// </summary>
    Task<List<OnlineSubtitleResult>> SearchByNameAsync(string query);

    /// <summary>
    /// 下载字幕文件到指定路径，返回保存路径。
    /// </summary>
    Task<string> DownloadSubtitleAsync(string downloadUrl, string savePath);
}
