using SweetPlayer.Core.Models;
using SweetPlayer.Services.Detection;
using Xunit;

namespace SweetPlayer.Tests.Unit;

/// <summary>
/// HDR 格式检测单元测试：覆盖 HDR10 / HLG / Dolby Vision / SDR 四个分支。
/// </summary>
public class HdrDetectionTests
{
    private readonly HdrDetectionService _service = new();

    [Fact]
    public void Detect_HDR10Stream_ShouldReturnHDR10()
    {
        var info = new VideoStreamInfo
        {
            ColorTransfer = "smpte2084",
            ColorPrimaries = "bt2020",
            BitDepth = 10,
        };

        var result = _service.Detect(info);

        Assert.True(result.IsHdr);
        Assert.Equal(HdrFormat.HDR10, result.Format);
    }

    [Fact]
    public void Detect_HLGStream_ShouldReturnHLG()
    {
        var info = new VideoStreamInfo
        {
            ColorTransfer = "arib-std-b67",
            ColorPrimaries = "bt2020",
            BitDepth = 10,
        };

        var result = _service.Detect(info);

        Assert.True(result.IsHdr);
        Assert.Equal(HdrFormat.HLG, result.Format);
    }

    [Fact]
    public void Detect_DolbyVision_ShouldReturnDV()
    {
        var info = new VideoStreamInfo
        {
            HasDolbyVisionRpu = true,
            DolbyVisionProfile = 8,
        };

        var result = _service.Detect(info);

        Assert.True(result.IsHdr);
        Assert.Equal(HdrFormat.DolbyVision, result.Format);
    }

    [Fact]
    public void Detect_SDRStream_ShouldReturnNotHdr()
    {
        var info = new VideoStreamInfo
        {
            ColorTransfer = "bt709",
            ColorPrimaries = "bt709",
            BitDepth = 8,
        };

        var result = _service.Detect(info);

        Assert.False(result.IsHdr);
        Assert.Null(result.Format);
    }

    [Fact]
    public void Detect_Hdr10PlusStream_ShouldReturnHDR10Plus()
    {
        var info = new VideoStreamInfo
        {
            ColorTransfer = "smpte2084",
            ColorPrimaries = "bt2020",
            BitDepth = 10,
            HasHdr10PlusMetadata = true,
        };

        var result = _service.Detect(info);

        Assert.True(result.IsHdr);
        Assert.Equal(HdrFormat.HDR10Plus, result.Format);
    }
}
