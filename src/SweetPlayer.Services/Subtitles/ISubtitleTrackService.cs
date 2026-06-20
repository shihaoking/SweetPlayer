using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// 字幕轨道管理服务：合并内嵌、外挂、在线字幕，统一切换。
/// </summary>
public interface ISubtitleTrackService
{
    /// <summary>获取当前可用的字幕轨道列表。</summary>
    List<SubtitleTrackInfo> GetAvailableTracks(IMpvPlayerService player);

    /// <summary>切换到指定轨道（trackId 为 0 表示关闭）。</summary>
    void SwitchTrack(IMpvPlayerService player, int trackId);

    /// <summary>关闭字幕显示。</summary>
    void DisableSubtitles(IMpvPlayerService player);
}
