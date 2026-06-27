namespace SweetPlayer.Services.Settings;

/// <summary>
/// 用户设置服务接口。
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// 是否自动恢复播放进度（默认：true）。
    /// </summary>
    bool AutoResumePlayback { get; set; }

    /// <summary>
    /// 播放窗口默认展示模式（默认：Windowed）。
    /// </summary>
    PlaybackWindowMode DefaultPlaybackWindowMode { get; set; }

    /// <summary>
    /// 保存设置到本地存储。
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 从本地存储加载设置。
    /// </summary>
    Task LoadAsync();
}
