using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 手动加载本地字幕文件的服务。
/// </summary>
public interface ISubtitleLoadService
{
    /// <summary>
    /// 校验本地字幕文件并构造 <see cref="SubtitleFileInfo"/>。
    /// </summary>
    /// <param name="subtitleFilePath">字幕文件完整路径。</param>
    /// <exception cref="FileNotFoundException">文件不存在。</exception>
    /// <exception cref="NotSupportedException">扩展名不在支持列表中。</exception>
    SubtitleFileInfo LoadFromFile(string subtitleFilePath);

    /// <summary>
    /// 将本地字幕文件应用到当前播放器（通过 sub-add 命令）。
    /// </summary>
    void ApplyToPlayer(IMpvPlayerService player, string subtitleFilePath);
}
