using SweetPlayer.Core.Models;
using SweetPlayer.Services.Detection;

namespace HdrDetectionTool;

/// <summary>
/// HDR / 杜比视界检测测试工具。
/// 用法：HdrDetectionTool [视频文件路径]
///   - 若未提供路径，将提示输入。
///   - 可选环境变量 FFPROBE_PATH 指定 ffprobe 路径。
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ── 获取视频文件路径 ──────────────────────────────────────────────
        var filePath = args.Length > 0 ? args[0] : null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"文件不存在：{filePath}");
                Console.ResetColor();
            }

            Console.Write("请输入视频文件路径：");
            filePath = Console.ReadLine()?.Trim().Trim('"');
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误：未提供有效的视频文件路径。");
            Console.ResetColor();
            return;
        }

        // ── 构建服务 ────────────────────────────────────────────────────
        var options = new VideoAnalysisOptions
        {
            FfprobePath = Environment.GetEnvironmentVariable("FFPROBE_PATH"),
            TimeoutMs   = 60_000,
        };

        var analysisService = new VideoAnalysisService(options);
        var hdrService      = new HdrDetectionService();

        // ── 执行分析 ────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine($"文件：{filePath}");
        Console.WriteLine($"大小：{new FileInfo(filePath).Length:N0} 字节");
        Console.WriteLine();

        Console.WriteLine("正在调用 ffprobe 分析视频流……");
        Console.WriteLine();

        VideoStreamInfo streamInfo;
        try
        {
            streamInfo = await analysisService.AnalyzeAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"分析失败：{ex.Message}");
            Console.ResetColor();
            return;
        }

        // ── 打印视频流原始数据 ────────────────────────────────────────────
        PrintSection("视频流原始数据（FFprobe 提取）", ConsoleColor.Cyan);
        PrintRow("编解码器 (codec_name)  ", streamInfo.VideoCodec);
        PrintRow("分辨率                  ", streamInfo.Width.HasValue ? $"{streamInfo.Width} x {streamInfo.Height}" : null);
        PrintRow("色彩空间 (color_space)  ", streamInfo.ColorSpace);
        PrintRow("色彩传输 (color_transfer)", streamInfo.ColorTransfer);
        PrintRow("色彩基准 (color_primaries)", streamInfo.ColorPrimaries);
        PrintRow("位深 (bit_depth)         ", streamInfo.BitDepth?.ToString());
        PrintRow("Dolby Vision RPU 标志   ", streamInfo.HasDolbyVisionRpu ? "是" : "否");
        PrintRow("Dolby Vision Profile    ", streamInfo.DolbyVisionProfile?.ToString());
        PrintRow("HDR10+ 动态元数据        ", streamInfo.HasHdr10PlusMetadata ? "是" : "否");

        // ── 打印音频流 ────────────────────────────────────────────────────
        if (streamInfo.AudioStreams.Count > 0)
        {
            Console.WriteLine();
            PrintSection($"音频流（共 {streamInfo.AudioStreams.Count} 条）", ConsoleColor.Cyan);
            for (int i = 0; i < streamInfo.AudioStreams.Count; i++)
            {
                var audio = streamInfo.AudioStreams[i];
                Console.WriteLine($"  [{i + 1}] 编解码器：{audio.Codec,-10} 声道：{audio.Channels}  语言：{audio.Language ?? "-"}");
                Console.WriteLine($"      Profile：{audio.Profile ?? "-"}  标题：{audio.Title ?? "-"}");
                Console.ForegroundColor = audio.IsAtmos ? ConsoleColor.Green : ConsoleColor.DarkGray;
                Console.WriteLine($"      Dolby Atmos：{(audio.IsAtmos ? "✔ 是" : "否")}");
                Console.ResetColor();
            }
        }

        // ── HDR 检测结果 ──────────────────────────────────────────────────
        var result = hdrService.Detect(streamInfo);

        Console.WriteLine();
        PrintSection("HDR 检测结果", ConsoleColor.Yellow);
        PrintRow("是否为 HDR 内容", result.IsHdr ? "✔ 是" : "✘ 否");

        if (result.Format.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  HDR 格式        ：");
            Console.WriteLine(result.Format.Value);
            Console.ResetColor();
        }
        else
        {
            PrintRow("  HDR 格式        ", "SDR（非 HDR）");
        }

        // ── 判定依据说明 ──────────────────────────────────────────────────
        Console.WriteLine();
        PrintSection("判定依据分析", ConsoleColor.DarkGray);
        Explain(streamInfo, result);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("按任意键退出……");
        Console.ResetColor();
        Console.ReadKey();
    }

    /// <summary>
    /// 打印检测判定的推理过程，帮助调试。
    /// </summary>
    private static void Explain(VideoStreamInfo info, HdrDetectionResult result)
    {
        // Dolby Vision
        if (info.HasDolbyVisionRpu || info.DolbyVisionProfile.HasValue)
        {
            Console.WriteLine("  → 检测到 Dolby Vision 标志（RPU 或 Profile），判定为杜比视界。");
            if (info.HasDolbyVisionRpu)
                Console.WriteLine("      HasDolbyVisionRpu = true（codec_tag 或 side_data 含 DOVI）");
            if (info.DolbyVisionProfile.HasValue)
                Console.WriteLine($"      DolbyVisionProfile = {info.DolbyVisionProfile}");
            return;
        }

        var transfer  = info.ColorTransfer?.ToLowerInvariant()  ?? string.Empty;
        var primaries = info.ColorPrimaries?.ToLowerInvariant() ?? string.Empty;
        var bitDepth  = info.BitDepth ?? 0;
        var isBt2020  = primaries.Contains("bt2020");

        // HLG
        if (transfer == "arib-std-b67" && isBt2020)
        {
            Console.WriteLine("  → color_transfer = arib-std-b67 且 color_primaries 含 bt2020，判定为 HLG。");
            return;
        }

        // HDR10 / HDR10+
        if (transfer == "smpte2084" && isBt2020 && bitDepth >= 10)
        {
            Console.WriteLine("  → color_transfer = smpte2084 且 color_primaries 含 bt2020 且 bitDepth >= 10，判定为 HDR10。");
            if (info.HasHdr10PlusMetadata)
                Console.WriteLine("      同时检测到 HDR10+ 动态元数据，升级为 HDR10+。");
            return;
        }

        // 未识别为 HDR
        Console.WriteLine("  → 未满足任何 HDR 条件，判定为 SDR。");
        Console.WriteLine($"      color_transfer   = \"{info.ColorTransfer ?? "(null)"}\"（期望：smpte2084 或 arib-std-b67）");
        Console.WriteLine($"      color_primaries  = \"{info.ColorPrimaries ?? "(null)"}\"（期望：含 bt2020）");
        Console.WriteLine($"      bitDepth         = {bitDepth}（期望：>= 10）");
        Console.WriteLine($"      HasDolbyVisionRpu = {info.HasDolbyVisionRpu}");
    }

    private static void PrintSection(string title, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"══ {title} ══");
        Console.ResetColor();
    }

    private static void PrintRow(string label, string? value)
    {
        Console.Write($"  {label}：");
        if (value is null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("(null)");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(value);
        }
    }
}
