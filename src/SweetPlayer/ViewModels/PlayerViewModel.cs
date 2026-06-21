using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 字幕大小档位。
/// </summary>
public enum SubtitleSize
{
    Small,
    Medium,
    Large,
    XLarge,
}

/// <summary>
/// 字幕颜色档位。
/// </summary>
public enum SubtitleColor
{
    White,
    Yellow,
}

/// <summary>
/// 画面比例档位（对应 mpv video-aspect-override）。
/// </summary>
public sealed class AspectRatioOption
{
    public string DisplayName { get; init; } = string.Empty;
    public string MpvValue { get; init; } = "-1";
}

/// <summary>
/// 倍速档位。
/// </summary>
public sealed class SpeedOption
{
    public string Label { get; init; } = "1.0x";
    public double Value { get; init; } = 1.0;
}

/// <summary>
/// 单条字幕/音频轨道描述。
/// </summary>
public sealed partial class TrackItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public int TrackId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Language { get; init; }
    public string? Codec { get; init; }
}

/// <summary>
/// 章节条目。
/// </summary>
public sealed class ChapterItem
{
    public int Index { get; init; }
    public string Title { get; init; } = string.Empty;
    public TimeSpan StartTime { get; init; }

    public string DisplayTime =>
        StartTime.TotalHours >= 1
            ? StartTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : StartTime.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
}

/// <summary>
/// 播放页 ViewModel：管理 UI 状态、命令以及与 IPlaybackControlService 的桥接。
/// </summary>
public sealed partial class PlayerViewModel : ViewModelBase
{
    private readonly IPlaybackControlService _playback;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _suppressSeekFeedback;

    public PlayerViewModel(IPlaybackControlService playback)
    {
        _playback = playback;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        AspectRatios = new ObservableCollection<AspectRatioOption>
        {
            new() { DisplayName = "自动",      MpvValue = "-1" },
            new() { DisplayName = "填充屏幕",  MpvValue = "no" },
            new() { DisplayName = "适应屏幕",  MpvValue = "0" },
            new() { DisplayName = "16:9",     MpvValue = "16:9" },
            new() { DisplayName = "4:3",      MpvValue = "4:3" },
        };
        _selectedAspectRatio = AspectRatios[0];

        Speeds = new ObservableCollection<SpeedOption>
        {
            new() { Label = "0.5x",  Value = 0.5  },
            new() { Label = "0.75x", Value = 0.75 },
            new() { Label = "1.0x",  Value = 1.0  },
            new() { Label = "1.25x", Value = 1.25 },
            new() { Label = "1.5x",  Value = 1.5  },
            new() { Label = "2.0x",  Value = 2.0  },
        };

        SubtitleTracks = new ObservableCollection<TrackItem>();
        AudioTracks = new ObservableCollection<TrackItem>();
        Chapters = new ObservableCollection<ChapterItem>();

        // 默认条目，便于在没有真实轨道时也能呈现 UI 结构。
        SubtitleTracks.Add(new TrackItem { TrackId = 0, Title = "无", IsSelected = true });

        _playback.PositionChanged += OnPositionChanged;
        _playback.StateChanged += OnStateChanged;
    }

    // ---------- 顶部 / 中央 ----------

    [ObservableProperty]
    private string _currentTitle = "未播放";

    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>当前播放位置（用于 Slider 的双向绑定）。</summary>
    [ObservableProperty]
    private double _positionSeconds;

    /// <summary>总时长（秒）。</summary>
    [ObservableProperty]
    private double _durationSeconds;

    /// <summary>用户拖拽进度条期间不再回写。</summary>
    [ObservableProperty]
    private bool _isUserSeeking;

    public string CurrentTimeText => FormatTime(TimeSpan.FromSeconds(PositionSeconds));
    public string DurationText => FormatTime(TimeSpan.FromSeconds(DurationSeconds));

    partial void OnPositionSecondsChanged(double value) => OnPropertyChanged(nameof(CurrentTimeText));
    partial void OnDurationSecondsChanged(double value) => OnPropertyChanged(nameof(DurationText));

    // ---------- 控制可见性 ----------

    [ObservableProperty]
    private bool _showControls = true;

    [ObservableProperty]
    private bool _isSettingsPaneOpen;

    [ObservableProperty]
    private int _settingsTabIndex;

    // ---------- 倍速 ----------

    public ObservableCollection<SpeedOption> Speeds { get; }

    [ObservableProperty]
    private double _speed = 1.0;

    public string SpeedLabel => Speed == 1.0
        ? "1.0x"
        : Speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";

    public bool IsSpeedNonDefault => Math.Abs(Speed - 1.0) > 0.001;

    partial void OnSpeedChanged(double value)
    {
        _playback.SetSpeed(value);
        OnPropertyChanged(nameof(SpeedLabel));
        OnPropertyChanged(nameof(IsSpeedNonDefault));
    }

    // ---------- 音量 ----------

    /// <summary>
    /// 音量值 0-200。0-100 为正常范围，100-200 为音频增强范围。
    /// </summary>
    [ObservableProperty]
    private double _volume = 100;

    [ObservableProperty]
    private bool _isMuted;

    private double _volumeBeforeMute = 100;

    public bool IsVolumeBoosted => Volume > 100.5;

    partial void OnVolumeChanged(double value)
    {
        ApplyVolume();
        OnPropertyChanged(nameof(IsVolumeBoosted));
    }

    private void ApplyVolume()
    {
        var v = IsMuted ? 0 : Volume;
        if (v > 100)
        {
            _playback.MpvPlayer.SetAudioBoost(v / 100.0);
            _playback.SetVolume(v);
        }
        else
        {
            _playback.MpvPlayer.SetAudioBoost(1.0);
            _playback.SetVolume(v);
        }
    }

    // ---------- 画面比例 ----------

    public ObservableCollection<AspectRatioOption> AspectRatios { get; }

    [ObservableProperty]
    private AspectRatioOption _selectedAspectRatio;

    partial void OnSelectedAspectRatioChanged(AspectRatioOption value)
    {
        if (value is null) return;
        _playback.MpvPlayer.SetAspectRatio(value.MpvValue);
    }

    // ---------- 字幕 ----------

    public ObservableCollection<TrackItem> SubtitleTracks { get; }
    public ObservableCollection<TrackItem> AudioTracks { get; }

    [ObservableProperty]
    private double _subtitleDelay;

    [ObservableProperty]
    private SubtitleSize _subtitleSize = SubtitleSize.Medium;

    [ObservableProperty]
    private SubtitleColor _subtitleColor = SubtitleColor.White;

    public IReadOnlyList<string> SubtitleSizeNames { get; } = new[] { "小", "中", "大", "特大" };
    public IReadOnlyList<string> SubtitleColorNames { get; } = new[] { "白色", "黄色" };

    [ObservableProperty]
    private int _subtitleSizeIndex = 1;

    [ObservableProperty]
    private int _subtitleColorIndex = 0;

    partial void OnSubtitleSizeIndexChanged(int value) =>
        SubtitleSize = (SubtitleSize)Math.Clamp(value, 0, 3);

    partial void OnSubtitleColorIndexChanged(int value) =>
        SubtitleColor = (SubtitleColor)Math.Clamp(value, 0, 1);

    // ---------- 章节 ----------

    public ObservableCollection<ChapterItem> Chapters { get; }

    public bool HasChapters => Chapters.Count > 0;

    public bool NoChapters => Chapters.Count == 0;

    /// <summary>从 mpv 加载章节列表。无 mpv 时静默忽略。</summary>
    public void LoadChaptersFromMpv()
    {
        // 真实集成：通过 mpv property "chapter-list" 获取数组结构。
        // 当前实现保持为占位以避免 P/Invoke 复杂度。UI 层若 Chapters 为空则隐藏 Tab 列表。
        OnPropertyChanged(nameof(HasChapters));
        OnPropertyChanged(nameof(NoChapters));
    }

    [RelayCommand]
    private void JumpToChapter(ChapterItem? chapter)
    {
        if (chapter is null) return;
        _playback.MpvPlayer.Seek(chapter.StartTime);
        ShowOsd($"章节: {chapter.Title}", "\uE8B5");
    }

    [RelayCommand]
    private void NextChapter()
    {
        if (Chapters.Count == 0) return;
        var pos = TimeSpan.FromSeconds(PositionSeconds);
        var next = Chapters.FirstOrDefault(c => c.StartTime > pos + TimeSpan.FromSeconds(1));
        if (next is not null) JumpToChapterCommand.Execute(next);
    }

    [RelayCommand]
    private void PrevChapter()
    {
        if (Chapters.Count == 0) return;
        var pos = TimeSpan.FromSeconds(PositionSeconds);
        var prev = Chapters.LastOrDefault(c => c.StartTime < pos - TimeSpan.FromSeconds(2));
        if (prev is not null) JumpToChapterCommand.Execute(prev);
    }

    // ---------- Up Next ----------

    [ObservableProperty]
    private bool _upNextVisible;

    [ObservableProperty]
    private string _nextEpisodeTitle = string.Empty;

    [ObservableProperty]
    private double _upNextCountdown = 10;

    /// <summary>用户层注入的"播放下一集"动作。</summary>
    public Func<Task>? PlayNextEpisodeHandler { get; set; }

    /// <summary>当无下一集时调用的"返回详情页"动作。</summary>
    public Action? ReturnToDetailHandler { get; set; }

    /// <summary>剧集信息：用于在最后一集判断结束态。</summary>
    public bool HasNextEpisode { get; set; }

    /// <summary>由播放页定时调用，检查是否需要弹出 Up Next 卡片。</summary>
    public void CheckUpNext()
    {
        if (!HasNextEpisode || string.IsNullOrEmpty(NextEpisodeTitle))
        {
            UpNextVisible = false;
            return;
        }
        if (DurationSeconds <= 0) return;
        var remaining = DurationSeconds - PositionSeconds;
        if (remaining <= 30 && remaining > 0)
        {
            if (!UpNextVisible)
            {
                UpNextVisible = true;
                UpNextCountdown = Math.Min(10, remaining);
            }
        }
        else
        {
            UpNextVisible = false;
        }
    }

    [RelayCommand]
    private async Task PlayNextEpisodeAsync()
    {
        UpNextVisible = false;
        if (PlayNextEpisodeHandler is not null)
        {
            await PlayNextEpisodeHandler.Invoke();
        }
    }

    [RelayCommand]
    private void DismissUpNext()
    {
        UpNextVisible = false;
        HasNextEpisode = false;
    }

    // ---------- 控制命令 ----------

    [RelayCommand]
    private void TogglePlayPause()
    {
        _playback.TogglePlayPause();
        IsPlaying = _playback.IsPlaying;
    }

    [RelayCommand]
    private void SeekForward()
    {
        _playback.SeekForward(10);
        ShowOsd("+10s", "\uEB9D");
    }

    [RelayCommand]
    private void SeekBackward()
    {
        _playback.SeekBackward(10);
        ShowOsd("-10s", "\uEB9E");
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (IsMuted)
        {
            IsMuted = false;
            Volume = _volumeBeforeMute;
        }
        else
        {
            _volumeBeforeMute = Volume;
            IsMuted = true;
            ApplyVolume();
        }
        ShowOsd(IsMuted ? "静音" : $"{(int)Volume}%", IsMuted ? "\uE74F" : "\uE767");
    }

    [RelayCommand]
    private void SelectSpeed(SpeedOption? option)
    {
        if (option is null) return;
        Speed = option.Value;
        ShowOsd(option.Label, "\uEC57");
    }

    [RelayCommand]
    private void ToggleSettingsPane()
    {
        IsSettingsPaneOpen = !IsSettingsPaneOpen;
    }

    [RelayCommand]
    private void OpenSettingsTab(int? tabIndex)
    {
        SettingsTabIndex = Math.Clamp(tabIndex ?? 0, 0, 3);
        IsSettingsPaneOpen = true;
    }

    [RelayCommand]
    private void SelectSubtitle(TrackItem? item)
    {
        if (item is null) return;
        foreach (var t in SubtitleTracks) t.IsSelected = false;
        item.IsSelected = true;
        _playback.MpvPlayer.SetSubtitleTrack(item.TrackId);
        ShowOsd(item.TrackId == 0 ? "字幕: 关闭" : $"字幕: {item.Title}", "\uED1E");
    }

    [RelayCommand]
    private void SelectAudio(TrackItem? item)
    {
        if (item is null) return;
        foreach (var t in AudioTracks) t.IsSelected = false;
        item.IsSelected = true;
        _playback.MpvPlayer.SetAudioTrack(item.TrackId);
        ShowOsd($"音轨: {item.Title}", "\uE8D6");
    }

    // ---------- Seek 桥接 ----------

    /// <summary>用户结束 Slider 拖拽，提交跳转。</summary>
    public void CommitSeekFromSlider()
    {
        IsUserSeeking = false;
        var target = TimeSpan.FromSeconds(PositionSeconds);
        _suppressSeekFeedback = true;
        _playback.MpvPlayer.Seek(target);
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (IsUserSeeking) return;
            PositionSeconds = position.TotalSeconds;
            var dur = _playback.Duration.TotalSeconds;
            if (dur > 0 && Math.Abs(DurationSeconds - dur) > 0.5)
            {
                DurationSeconds = dur;
            }
            _suppressSeekFeedback = false;
            CheckUpNext();
        });
    }

    private void OnStateChanged(object? sender, PlaybackState state)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
        });
    }

    // ---------- OSD ----------

    /// <summary>请求显示 OSD 通知；由 PlayerPage 通过该委托代理到 UI 控件。</summary>
    public Action<string, string>? ShowOsdHandler { get; set; }

    public void ShowOsd(string message, string glyph = "\uE767")
    {
        ShowOsdHandler?.Invoke(message, glyph);
    }

    // ---------- 工具 ----------

    /// <summary>初始化 ViewModel：填充标题、长度并复位状态。</summary>
    public void Initialize(VideoFile? video, bool hasNextEpisode = false, string? nextTitle = null)
    {
        if (video is not null)
        {
            CurrentTitle = BuildTitle(video);
        }
        HasNextEpisode = hasNextEpisode;
        NextEpisodeTitle = nextTitle ?? string.Empty;
        DurationSeconds = _playback.Duration.TotalSeconds;
        PositionSeconds = _playback.Position.TotalSeconds;
        IsPlaying = _playback.IsPlaying;
    }

    public void DetachEvents()
    {
        _playback.PositionChanged -= OnPositionChanged;
        _playback.StateChanged -= OnStateChanged;
    }

    private static string BuildTitle(VideoFile v)
    {
        if (v.MediaType == MediaType.TVEpisode && v.Season is int s && v.Episode is int e)
        {
            var prefix = v.MovieMetadata?.ChineseTitle ?? Path.GetFileNameWithoutExtension(v.FileName);
            var ep = $"S{s:00}E{e:00}";
            return string.IsNullOrEmpty(v.EpisodeTitle)
                ? $"{prefix} - {ep}"
                : $"{prefix} - {ep} {v.EpisodeTitle}";
        }
        return v.MovieMetadata?.ChineseTitle ?? Path.GetFileNameWithoutExtension(v.FileName);
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : ts.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }
}
