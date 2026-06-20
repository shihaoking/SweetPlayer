using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.Services.Subtitles;

/// <summary>
/// <see cref="ISubtitleSettingsService"/> 的默认实现。
/// </summary>
/// <remarks>
/// 设置文件路径：%LocalAppData%/SweetPlayer/subtitle_settings.json。
/// mpv 属性映射：
/// <list type="bullet">
/// <item>sub-font-size：依据 <see cref="SubtitleSize"/> 取 36/48/60/72</item>
/// <item>sub-color：白色 #FFFFFFFF，黄色 #FFFFFF00（mpv 颜色格式 AARRGGBB）</item>
/// <item>sub-pos：底部 100，顶部 0</item>
/// <item>sub-delay：直接写入秒数</item>
/// </list>
/// </remarks>
public class SubtitleSettingsService : ISubtitleSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object _sync = new();
    private readonly string _settingsFilePath;
    private readonly ILogger<SubtitleSettingsService>? _logger;
    private SubtitleSettings? _cached;

    public SubtitleSettingsService(ILogger<SubtitleSettingsService>? logger = null)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SweetPlayer");
        _settingsFilePath = Path.Combine(dir, "subtitle_settings.json");
    }

    /// <inheritdoc />
    public SubtitleSettings GetSettings()
    {
        lock (_sync)
        {
            if (_cached is not null)
            {
                return Clone(_cached);
            }

            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<SubtitleSettings>(json, JsonOptions);
                    if (loaded is not null)
                    {
                        _cached = Sanitize(loaded);
                        return Clone(_cached);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "读取字幕设置失败：{Path}", _settingsFilePath);
            }

            _cached = new SubtitleSettings();
            return Clone(_cached);
        }
    }

    /// <inheritdoc />
    public void ApplySettings(IMpvPlayerService player, SubtitleSettings settings)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var sanitized = Sanitize(settings);
        TrySetMpvProperty(player, "sub-font-size", FontSizeFor(sanitized.Size).ToString(CultureInfo.InvariantCulture));
        TrySetMpvProperty(player, "sub-color", ColorFor(sanitized.Color));
        TrySetMpvProperty(player, "sub-pos", PositionFor(sanitized.Position).ToString(CultureInfo.InvariantCulture));
        TrySetMpvProperty(player, "sub-delay", sanitized.DelaySeconds.ToString("0.###", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(SubtitleSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        var sanitized = Sanitize(settings);

        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(sanitized, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);

        lock (_sync)
        {
            _cached = sanitized;
        }
    }

    // ---------------- helpers ----------------

    private static SubtitleSettings Sanitize(SubtitleSettings input)
    {
        return new SubtitleSettings
        {
            Size = Enum.IsDefined(typeof(SubtitleSize), input.Size) ? input.Size : SubtitleSize.Medium,
            Color = Enum.IsDefined(typeof(SubtitleColor), input.Color) ? input.Color : SubtitleColor.White,
            Position = Enum.IsDefined(typeof(SubtitlePosition), input.Position) ? input.Position : SubtitlePosition.Bottom,
            DelaySeconds = Math.Clamp(input.DelaySeconds, -5.0, 5.0),
        };
    }

    private static SubtitleSettings Clone(SubtitleSettings src) => new()
    {
        Size = src.Size,
        Color = src.Color,
        Position = src.Position,
        DelaySeconds = src.DelaySeconds,
    };

    private static int FontSizeFor(SubtitleSize size) => size switch
    {
        SubtitleSize.Small => 36,
        SubtitleSize.Medium => 48,
        SubtitleSize.Large => 60,
        SubtitleSize.ExtraLarge => 72,
        _ => 48,
    };

    private static string ColorFor(SubtitleColor color) => color switch
    {
        SubtitleColor.Yellow => "#FFFFFF00",
        _ => "#FFFFFFFF",
    };

    private static int PositionFor(SubtitlePosition position) => position switch
    {
        SubtitlePosition.Top => 0,
        _ => 100,
    };

    /// <summary>
    /// 通过反射尝试调用 MpvPlayerService 内部 TrySetProperty；失败时回退为静默忽略。
    /// 这样保持 IMpvPlayerService 接口最小化的同时，仍能下发自定义 mpv 属性。
    /// </summary>
    private void TrySetMpvProperty(IMpvPlayerService player, string name, string value)
    {
        try
        {
            var method = player.GetType().GetMethod(
                "TrySetProperty",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string), typeof(string) },
                modifiers: null);
            method?.Invoke(player, new object[] { name, value });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "下发 mpv 属性 {Name} 失败", name);
        }
    }
}
