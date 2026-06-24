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
    }

    [ObservableProperty]
    private bool _autoResumePlayback;

    partial void OnAutoResumePlaybackChanged(bool value)
    {
        _userSettings.AutoResumePlayback = value;
    }
}
