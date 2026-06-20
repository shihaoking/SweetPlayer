using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 字幕显示设置服务：读写设置并应用到 mpv。
/// </summary>
public interface ISubtitleSettingsService
{
    /// <summary>读取持久化的字幕设置（首次访问会同步加载）。</summary>
    SubtitleSettings GetSettings();

    /// <summary>将设置应用到指定播放器实例。</summary>
    void ApplySettings(IMpvPlayerService player, SubtitleSettings settings);

    /// <summary>持久化保存字幕设置到本地 JSON 文件。</summary>
    Task SaveSettingsAsync(SubtitleSettings settings);
}
