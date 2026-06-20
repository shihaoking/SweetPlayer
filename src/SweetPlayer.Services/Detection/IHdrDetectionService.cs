using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Detection;

/// <summary>
/// HDR 格式检测服务。基于 <see cref="VideoStreamInfo"/> 推断 HDR/HLG/Dolby Vision。
/// </summary>
public interface IHdrDetectionService
{
    /// <summary>
    /// 根据视频流信息检测 HDR 格式。
    /// </summary>
    HdrDetectionResult Detect(VideoStreamInfo streamInfo);
}
