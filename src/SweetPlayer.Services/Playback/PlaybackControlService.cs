using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.Detection;
using SweetPlayer.Services.Settings;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// 高层播放控制服务接口，供 ViewModel/UI 直接调用。
/// </summary>
public interface IPlaybackControlService
{
    /// <summary>当前正在播放的视频文件。</summary>
    VideoFile? CurrentVideo { get; }

    /// <summary>是否正在播放。</summary>
    bool IsPlaying { get; }

    /// <summary>当前播放位置。</summary>
    TimeSpan Position { get; }

    /// <summary>当前媒体总时长。</summary>
    TimeSpan Duration { get; }

    /// <summary>底层 MPV 服务（供需要直接访问渲染上下文的 UI 控件使用）。</summary>
    IMpvPlayerService MpvPlayer { get; }

    /// <summary>开始播放指定视频。</summary>
    Task PlayVideoAsync(VideoFile videoFile);

    /// <summary>暂停。</summary>
    void Pause();

    /// <summary>恢复播放。</summary>
    void Resume();

    /// <summary>切换播放/暂停。</summary>
    void TogglePlayPause();

    /// <summary>前进。</summary>
    void SeekForward(int seconds = 10);

    /// <summary>后退。</summary>
    void SeekBackward(int seconds = 10);

    /// <summary>设置音量（0-100）。</summary>
    void SetVolume(double volume);

    /// <summary>设置播放速度。</summary>
    void SetSpeed(double speed);

    /// <summary>停止并释放当前播放。</summary>
    Task StopAsync();

    /// <summary>停止并释放当前播放（同步入口）。</summary>
    void Stop();

    /// <summary>位置变化（秒级）。</summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>状态变化。</summary>
    event EventHandler<PlaybackState>? StateChanged;
}

/// <summary>
/// 默认播放控制服务实现：
/// 1. 包装 <see cref="IMpvPlayerService"/> 提供常用操作；
/// 2. 集成 <see cref="IPlaybackProgressService"/> 实现每 5 秒进度持久化与停止时持久化；
/// 3. 集成 <see cref="IWindowsHdrService"/>，HDR/杜比视界视频自动切换系统 HDR。
/// </summary>
public class PlaybackControlService : IPlaybackControlService, IDisposable
{
    private readonly IMpvPlayerService _mpv;
    private readonly IPlaybackProgressService _progress;
    private readonly IWindowsHdrService _hdr;
    private readonly ILogger<PlaybackControlService> _logger;
    private readonly IUserSettingsService _userSettings;
    private readonly System.Threading.Timer _saveProgressTimer;
    private VideoFile? _currentVideo;
    private bool _hdrAutoEnabled;
    private bool _disposed;

    public PlaybackControlService(
        IMpvPlayerService mpv,
        IPlaybackProgressService progress,
        IWindowsHdrService hdr,
        IUserSettingsService userSettings,
        ILogger<PlaybackControlService> logger)
    {
        _mpv = mpv;
        _progress = progress;
        _hdr = hdr;
        _userSettings = userSettings;
        _logger = logger;

        _mpv.PositionChanged += OnMpvPositionChanged;
        _mpv.StateChanged += OnMpvStateChanged;
        _mpv.FileEnded += OnMpvFileEnded;

        _saveProgressTimer = new System.Threading.Timer(OnSaveProgressTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public VideoFile? CurrentVideo => _currentVideo;
    public bool IsPlaying => _mpv.IsPlaying;
    public TimeSpan Position => _mpv.Position;
    public TimeSpan Duration => _mpv.Duration;
    public IMpvPlayerService MpvPlayer => _mpv;

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;

    public async Task PlayVideoAsync(VideoFile videoFile)
    {
        ArgumentNullException.ThrowIfNull(videoFile);

        // 已有播放则先持久化进度。
        if (_currentVideo is not null)
        {
            await SaveCurrentProgressAsync();
        }

        _currentVideo = videoFile;

        // HDR/杜比视界自动启用系统 HDR
        try
        {
            var needsHdr = videoFile.HasHDR || videoFile.HasDolbyVision;
            if (needsHdr && _hdr.IsHdrSupported() && !_hdr.IsHdrEnabled())
            {
                await _hdr.EnableHdrAsync();
                _hdrAutoEnabled = true;
                _logger.LogInformation("已为 HDR 视频自动启用系统 HDR：{File}", videoFile.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "自动启用系统 HDR 失败");
        }

        // 加载文件（wid 模式无需等待渲染器就绪）
        await _mpv.LoadFileAsync(videoFile.FullPath);

        // 等待文件加载完成并且 mpv 准备好播放后再恢复进度
        // 需要等待足够的时间让 mpv 完成内部初始化（包括音频解码器）
        await Task.Delay(800);

        // 恢复上次播放进度（根据用户设置决定是否恢复）
        try
        {
            if (_userSettings.AutoResumePlayback)
            {
                var saved = await _progress.GetProgressAsync(videoFile.Id);
                if (saved is not null && !saved.IsCompleted && saved.Position > TimeSpan.FromSeconds(3))
                {
                    _logger.LogInformation("从数据库加载进度：VideoId={VideoId}, Position={Position}s", videoFile.Id, saved.Position.TotalSeconds);
                    _mpv.Seek(saved.Position);
                    _logger.LogInformation("恢复播放进度：{Position}", saved.Position);
                    
                    // 恢复进度后，确保 UI 能够正常更新（重置 seek 状态）
                    await Task.Delay(100);
                }
                else
                {
                    _logger.LogInformation("新视频无保存的进度或进度太短（<3秒），从头开始播放：VideoId={VideoId}", videoFile.Id);
                }
            }
            else
            {
                _logger.LogInformation("用户设置禁用自动恢复进度，从头开始播放：VideoId={VideoId}", videoFile.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "恢复播放进度失败");
        }

        _saveProgressTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Pause() => _mpv.Pause();

    public void Resume() => _mpv.Play();

    public void TogglePlayPause() => _mpv.TogglePlayPause();

    public void SeekForward(int seconds = 10) => _mpv.SeekRelative(seconds);

    public void SeekBackward(int seconds = 10) => _mpv.SeekRelative(-seconds);

    public void SetVolume(double volume) => _mpv.Volume = volume;

    public void SetSpeed(double speed) => _mpv.Speed = speed;

    public void Stop()
    {
        _ = StopAsync();
    }

    public async Task StopAsync()
    {
        _saveProgressTimer.Change(Timeout.Infinite, Timeout.Infinite);

        try
        {
            await SaveCurrentProgressAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存最终进度失败");
        }

        _mpv.Stop();
        
        _currentVideo = null;

        // 恢复 HDR 状态
        if (_hdrAutoEnabled)
        {
            try
            {
                await _hdr.DisableHdrAsync();
                _logger.LogInformation("已恢复系统 HDR 原始状态");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "恢复系统 HDR 状态失败");
            }
            finally
            {
                _hdrAutoEnabled = false;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mpv.PositionChanged -= OnMpvPositionChanged;
        _mpv.StateChanged -= OnMpvStateChanged;
        _mpv.FileEnded -= OnMpvFileEnded;

        try { _saveProgressTimer.Dispose(); } catch { }

        GC.SuppressFinalize(this);
    }

    private async Task SaveCurrentProgressAsync()
    {
        if (_currentVideo is null) return;
        var pos = _mpv.Position;
        var dur = _mpv.Duration;
        if (dur <= TimeSpan.Zero) return;

        _logger.LogInformation("保存进度：VideoId={VideoId}, Position={Position}s", _currentVideo.Id, pos.TotalSeconds);
        await _progress.SaveProgressAsync(_currentVideo.Id, pos, dur);
    }

    private void OnSaveProgressTick(object? state)
    {
        _ = SaveCurrentProgressAsync();
    }

    private void OnMpvPositionChanged(object? sender, TimeSpan position)
    {
        PositionChanged?.Invoke(this, position);
    }

    private void OnMpvStateChanged(object? sender, PlaybackState state)
    {
        StateChanged?.Invoke(this, state);
    }

    private async void OnMpvFileEnded(object? sender, EventArgs e)
    {
        if (_currentVideo is not null)
        {
            try
            {
                await _progress.MarkCompletedAsync(_currentVideo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "标记完成失败");
            }
        }

        await StopAsync();
    }
}
