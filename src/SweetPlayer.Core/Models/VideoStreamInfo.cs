namespace SweetPlayer.Core.Models;

/// <summary>
/// 视频文件流元数据，由 FFprobe 等工具分析得到。
/// </summary>
public class VideoStreamInfo
{
    /// <summary>视频编解码器标识，如 hevc、h264、av1。</summary>
    public string? VideoCodec { get; set; }

    /// <summary>视频宽度（像素）。</summary>
    public int? Width { get; set; }

    /// <summary>视频高度（像素）。</summary>
    public int? Height { get; set; }

    /// <summary>颜色空间，如 bt2020nc、bt709。</summary>
    public string? ColorSpace { get; set; }

    /// <summary>颜色传输特性，如 smpte2084、arib-std-b67。</summary>
    public string? ColorTransfer { get; set; }

    /// <summary>颜色基准，如 bt2020、bt709。</summary>
    public string? ColorPrimaries { get; set; }

    /// <summary>位深（8、10、12）。</summary>
    public int? BitDepth { get; set; }

    /// <summary>是否包含 Dolby Vision RPU。</summary>
    public bool HasDolbyVisionRpu { get; set; }

    /// <summary>Dolby Vision Profile（5、7、8 等）。</summary>
    public int? DolbyVisionProfile { get; set; }

    /// <summary>是否包含 HDR10+ 动态元数据。</summary>
    public bool HasHdr10PlusMetadata { get; set; }

    /// <summary>音频流列表。</summary>
    public List<AudioStreamInfo> AudioStreams { get; set; } = new();
}

/// <summary>
/// 音频流元数据。
/// </summary>
public class AudioStreamInfo
{
    /// <summary>音频编解码器，如 eac3、truehd、dts、aac。</summary>
    public string? Codec { get; set; }

    /// <summary>音轨语言。</summary>
    public string? Language { get; set; }

    /// <summary>声道数。</summary>
    public int? Channels { get; set; }

    /// <summary>是否为 Dolby Atmos。</summary>
    public bool IsAtmos { get; set; }

    /// <summary>音轨标题（用于辅助识别 Atmos 等标签）。</summary>
    public string? Title { get; set; }

    /// <summary>音频流 profile 字符串。</summary>
    public string? Profile { get; set; }
}

/// <summary>
/// HDR 检测结果。
/// </summary>
public class HdrDetectionResult
{
    /// <summary>是否为 HDR 内容（含 Dolby Vision）。</summary>
    public bool IsHdr { get; set; }

    /// <summary>HDR 格式。</summary>
    public HdrFormat? Format { get; set; }
}
