using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Detection;

/// <summary>
/// 通过调用 FFprobe 命令行工具获取视频/音频流元数据的实现。
/// </summary>
public class VideoAnalysisService : IVideoAnalysisService
{
    private readonly VideoAnalysisOptions _options;

    public VideoAnalysisService() : this(new VideoAnalysisOptions())
    {
    }

    public VideoAnalysisService(VideoAnalysisOptions options)
    {
        _options = options ?? new VideoAnalysisOptions();
    }

    /// <inheritdoc />
    public async Task<VideoStreamInfo> AnalyzeAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("filePath 不能为空", nameof(filePath));
        }

        var info = new VideoStreamInfo();

        var ffprobe = string.IsNullOrWhiteSpace(_options.FfprobePath) ? "ffprobe" : _options.FfprobePath!;
        var psi = new ProcessStartInfo
        {
            FileName = ffprobe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("quiet");
        psi.ArgumentList.Add("-print_format");
        psi.ArgumentList.Add("json");
        psi.ArgumentList.Add("-show_format");
        psi.ArgumentList.Add("-show_streams");
        psi.ArgumentList.Add(filePath);

        string stdout;
        try
        {
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                return info;
            }

            using var cts = new CancellationTokenSource(_options.TimeoutMs);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { /* ignore */ }
                return info;
            }

            stdout = await stdoutTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // ffprobe 不存在或调用失败，返回空结果
            return info;
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return info;
        }

        try
        {
            ParseFfprobeJson(stdout, info);
        }
        catch (JsonException)
        {
            // JSON 解析失败时静默返回当前结果
        }

        return info;
    }

    /// <summary>
    /// 解析 ffprobe JSON 输出并填充流信息。
    /// </summary>
    private static void ParseFfprobeJson(string json, VideoStreamInfo info)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var videoFilled = false;
        foreach (var stream in streams.EnumerateArray())
        {
            var codecType = GetString(stream, "codec_type");
            if (string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase) && !videoFilled)
            {
                FillVideoStream(stream, info);
                videoFilled = true;
            }
            else if (string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase))
            {
                info.AudioStreams.Add(BuildAudioStream(stream));
            }
        }
    }

    private static void FillVideoStream(JsonElement stream, VideoStreamInfo info)
    {
        info.VideoCodec = GetString(stream, "codec_name");
        info.Width = GetInt(stream, "width");
        info.Height = GetInt(stream, "height");
        info.ColorSpace = GetString(stream, "color_space");
        info.ColorTransfer = GetString(stream, "color_transfer");
        info.ColorPrimaries = GetString(stream, "color_primaries");

        // 位深：优先 bits_per_raw_sample，其次根据 pix_fmt 推断
        var bitDepth = GetInt(stream, "bits_per_raw_sample");
        if (bitDepth is null)
        {
            var pixFmt = GetString(stream, "pix_fmt");
            bitDepth = InferBitDepthFromPixFmt(pixFmt);
        }
        info.BitDepth = bitDepth;

        // 通过 codec_tag_string 识别 dvhe / dvh1 等 Dolby Vision 编码标签
        var codecTag = GetString(stream, "codec_tag_string");
        if (!string.IsNullOrEmpty(codecTag))
        {
            var lower = codecTag.ToLowerInvariant();
            if (lower.Contains("dvhe") || lower.Contains("dvh1") || lower.Contains("dvav") || lower.Contains("dva1"))
            {
                info.HasDolbyVisionRpu = true;
            }
        }

        // side_data_list 中查找 DOVI configuration / HDR10+
        if (stream.TryGetProperty("side_data_list", out var sideList) && sideList.ValueKind == JsonValueKind.Array)
        {
            foreach (var side in sideList.EnumerateArray())
            {
                var sideType = GetString(side, "side_data_type") ?? string.Empty;
                var lowerType = sideType.ToLowerInvariant();
                if (lowerType.Contains("dovi") || lowerType.Contains("dolby vision"))
                {
                    info.HasDolbyVisionRpu = true;
                    var profile = GetInt(side, "dv_profile");
                    if (profile.HasValue)
                    {
                        info.DolbyVisionProfile = profile;
                    }
                }
                else if (lowerType.Contains("hdr dynamic metadata") || lowerType.Contains("hdr10+"))
                {
                    info.HasHdr10PlusMetadata = true;
                }
            }
        }
    }

    private static AudioStreamInfo BuildAudioStream(JsonElement stream)
    {
        var audio = new AudioStreamInfo
        {
            Codec = GetString(stream, "codec_name"),
            Channels = GetInt(stream, "channels"),
            Profile = GetString(stream, "profile"),
        };

        if (stream.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object)
        {
            audio.Language = GetString(tags, "language");
            audio.Title = GetString(tags, "title");
        }

        audio.IsAtmos = DetectAtmos(audio, stream);
        return audio;
    }

    /// <summary>
    /// 通过 codec / profile / title / side_data 推断是否为 Dolby Atmos。
    /// </summary>
    private static bool DetectAtmos(AudioStreamInfo audio, JsonElement stream)
    {
        var codec = audio.Codec?.ToLowerInvariant();
        var profile = audio.Profile?.ToLowerInvariant() ?? string.Empty;
        var title = audio.Title?.ToLowerInvariant() ?? string.Empty;

        // E-AC-3 JOC（Joint Object Coding）：profile 通常包含 "joc"
        if (codec == "eac3" && profile.Contains("joc"))
        {
            return true;
        }

        // TrueHD Atmos：profile 或 title 含 "atmos"
        if (codec == "truehd" && (profile.Contains("atmos") || title.Contains("atmos")))
        {
            return true;
        }

        // 通用兜底：标题中含 atmos 字样
        if (title.Contains("atmos"))
        {
            return true;
        }

        // side_data_list 含 atmos / joc
        if (stream.TryGetProperty("side_data_list", out var sideList) && sideList.ValueKind == JsonValueKind.Array)
        {
            foreach (var side in sideList.EnumerateArray())
            {
                var sideType = GetString(side, "side_data_type")?.ToLowerInvariant() ?? string.Empty;
                if (sideType.Contains("atmos") || sideType.Contains("joc"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int? InferBitDepthFromPixFmt(string? pixFmt)
    {
        if (string.IsNullOrEmpty(pixFmt))
        {
            return null;
        }

        var lower = pixFmt.ToLowerInvariant();
        if (lower.Contains("p012") || lower.Contains("12le") || lower.Contains("12be")) return 12;
        if (lower.Contains("p010") || lower.Contains("10le") || lower.Contains("10be")) return 10;
        return 8;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static int? GetInt(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(value.GetString(), out var i) => i,
            _ => null,
        };
    }
}
