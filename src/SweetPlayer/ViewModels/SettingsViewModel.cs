using CommunityToolkit.Mvvm.ComponentModel;
using SweetPlayer.Services.Settings;

namespace SweetPlayer.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsService _userSettings;

    public SettingsViewModel(IUserSettingsService userSettings)
    {
        _userSettings = userSettings;
        _autoResumePlayback = _userSettings.AutoResumePlayback;
        _selectedPlaybackWindowModeIndex = (int)_userSettings.DefaultPlaybackWindowMode;
    }

    [ObservableProperty]
    private bool _autoResumePlayback;

    partial void OnAutoResumePlaybackChanged(bool value)
    {
        _userSettings.AutoResumePlayback = value;
    }

    /// <summary>播放窗口默认模式索引（绑定到 ComboBox.SelectedIndex）。</summary>
    [ObservableProperty]
    private int _selectedPlaybackWindowModeIndex;

    partial void OnSelectedPlaybackWindowModeIndexChanged(int value)
    {
        _userSettings.DefaultPlaybackWindowMode = (PlaybackWindowMode)value;
    }
}
