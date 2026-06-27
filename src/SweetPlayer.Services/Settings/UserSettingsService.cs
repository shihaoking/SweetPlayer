using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SweetPlayer.Services.Settings;

/// <summary>
/// 用户设置服务实现，使用本地 JSON 文件存储。
/// </summary>
public class UserSettingsService : IUserSettingsService
{
    private readonly ILogger<UserSettingsService> _logger;
    private readonly string _settingsFilePath;
    private UserSettingsData _data = new();

    public UserSettingsService(ILogger<UserSettingsService> logger)
    {
        _logger = logger;
        
        // 设置文件保存在用户文档目录
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SweetPlayer");
        Directory.CreateDirectory(appDataPath);
        _settingsFilePath = Path.Combine(appDataPath, "user_settings.json");
    }

    public bool AutoResumePlayback
    {
        get => _data.AutoResumePlayback;
        set
        {
            if (_data.AutoResumePlayback != value)
            {
                _data.AutoResumePlayback = value;
                _ = SaveAsync();
            }
        }
    }

    public PlaybackWindowMode DefaultPlaybackWindowMode
    {
        get => _data.DefaultPlaybackWindowMode;
        set
        {
            if (_data.DefaultPlaybackWindowMode != value)
            {
                _data.DefaultPlaybackWindowMode = value;
                _ = SaveAsync();
            }
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("用户设置已保存：{Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存用户设置失败");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                _data = JsonSerializer.Deserialize<UserSettingsData>(json) ?? new UserSettingsData();
                _logger.LogInformation("用户设置已加载：{Path}", _settingsFilePath);
            }
            else
            {
                _data = new UserSettingsData();
                await SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载用户设置失败，使用默认设置");
            _data = new UserSettingsData();
        }
    }

    /// <summary>
    /// 设置数据结构（用于 JSON 序列化）。
    /// </summary>
    private class UserSettingsData
    {
        public bool AutoResumePlayback { get; set; } = true;
        public PlaybackWindowMode DefaultPlaybackWindowMode { get; set; } = PlaybackWindowMode.Windowed;
    }
}
