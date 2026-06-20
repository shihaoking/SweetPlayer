using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Detection;

/// <summary>
/// 基于色彩传输/基准/位深以及 Dolby Vision 标志识别 HDR 格式。
/// </summary>
public class HdrDetectionService : IHdrDetectionService
{
    /// <inheritdoc />
    public HdrDetectionResult Detect(VideoStreamInfo streamInfo)
    {
        if (streamInfo is null)
        {
            return new HdrDetectionResult();
        }

        // Dolby Vision 优先级最高
        if (streamInfo.HasDolbyVisionRpu || streamInfo.DolbyVisionProfile.HasValue)
        {
            return new HdrDetectionResult
            {
                IsHdr = true,
                Format = HdrFormat.DolbyVision,
            };
        }

        var transfer = streamInfo.ColorTransfer?.ToLowerInvariant() ?? string.Empty;
        var primaries = streamInfo.ColorPrimaries?.ToLowerInvariant() ?? string.Empty;
        var bitDepth = streamInfo.BitDepth ?? 0;

        var isBt2020 = primaries.Contains("bt2020");

        // HLG: ARIB STD-B67 + BT.2020
        if (transfer == "arib-std-b67" && isBt2020)
        {
            return new HdrDetectionResult { IsHdr = true, Format = HdrFormat.HLG };
        }

        // HDR10 / HDR10+: ST 2084 + BT.2020 + 10bit 及以上
        if (transfer == "smpte2084" && isBt2020 && bitDepth >= 10)
        {
            return new HdrDetectionResult
            {
                IsHdr = true,
                Format = streamInfo.HasHdr10PlusMetadata ? HdrFormat.HDR10Plus : HdrFormat.HDR10,
            };
        }

        return new HdrDetectionResult { IsHdr = false, Format = null };
    }
}
