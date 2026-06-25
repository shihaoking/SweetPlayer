using System.Globalization;
using Microsoft.Extensions.Logging;
using Richasy.MpvKernel;
using Richasy.MpvKernel.Core;
using Richasy.MpvKernel.Core.Enums;
using Richasy.MpvKernel.Core.Models;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// 基于 mpv-kernel 的 IMpvPlayerService 实现。
/// 使用 wid（Window ID）模式让 mpv 直接 GPU 零拷贝渲染。
/// </summary>
public class MpvPlayerService : IMpvPlayerService
{
    private readonly ILogger<MpvPlayerService> _logger;
    private readonly System.Threading.Timer _positionTimer;

    private MpvClient? _client;
    private IntPtr _windowHandle;
    private bool _disposed;

    private TimeSpan _duration;
    private TimeSpan _position;
    private double _volume = 100;
    private double _speed = 1.0;
    private PlaybackState _state = PlaybackState.Idle;

    public MpvPlayerService(ILogger<MpvPlayerService> logger)
    {
        _logger = logger;
        _positionTimer = new System.Threading.Timer(OnPositionTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsPlaying => _state == PlaybackState.Playing;
    public bool IsPaused => _state == PlaybackState.Paused;
    public TimeSpan Position => _position;
    public TimeSpan Duration => _duration;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 200);
            if (_client is not null)
            {
                _ = _client.SetVolumeAsync(Math.Clamp(_volume, 0, 100));
            }
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25, 4.0);
            if (_client is not null)
            {
                _ = _client.SetSpeedAsync(_speed);
            }
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? FileEnded;

    public async Task InitializeAsync(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        if (_client is not null)
        {
            _logger.LogInformation("MpvClient 已初始化，跳过重复初始化");
            return;
        }

        _logger.LogInformation("初始化 MpvClient (wid={Handle})...", windowHandle);

        _client = await MpvClient.CreateAsync(logger: _logger);
        await _client.UseIdleAsync(true);
        await _client.UseKeepOpenAsync(true);
        await _client.SetVideoOutputAsync(VideoOutputType.Gpu);
        await _client.SetGpuContextAsync(GpuContextType.D3D11);
        await _client.SetHardwareDecodeAsync(HardwareDecodeType.Nvdec);

        // 订阅事件
        _client.DataNotify += OnClientDataNotify;
        _client.ReachFileEnd += OnClientFileEnd;
        _client.ReachFileLoaded += OnClientFileLoaded;

        // 设置日志级别
        await _client.SetLogLevelAsync(MpvLogLevel.Info);

        _logger.LogInformation("MpvClient 初始化完成");
    }

    public async Task SetDecodeOptionsAsync(DecodeMode mode)
    {
        if (_client is null) return;

        var hwdec = mode switch
        {
            DecodeMode.Software => "no",
            DecodeMode.Hardware => "auto",
            _ => "auto",
        };

        await Task.Run(() => MpvNative.SetOptionString(_client.Handle, "hwdec", hwdec));
        _logger.LogInformation("解码模式设置为：{Mode}", mode);
    }

    public async Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (_client is null)
        {
            _logger.LogWarning("MpvClient 未初始化，跳过加载");
            return;
        }

        UpdateState(PlaybackState.Loading);

        try
        {
            var options = new MpvPlayOptions
            {
                WindowHandle = _windowHandle != IntPtr.Zero ? _windowHandle : null,
            };

            _logger.LogInformation("加载文件：{File}", filePath);
            await _client.PlayAsync(filePath, options);
            _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadfile 命令失败");
            UpdateState(PlaybackState.Error);
        }
    }

    public void Play()
    {
        if (_client is null) return;
        _ = _client.ResumeAsync();
        UpdateState(PlaybackState.Playing);
        _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Pause()
    {
        if (_client is null) return;
        _ = _client.PauseAsync();
        UpdateState(PlaybackState.Paused);
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause(); else Play();
    }

    public void Seek(TimeSpan position)
    {
        if (_client is null) return;
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        System.Diagnostics.Debug.WriteLine($"[MpvPlayerService] Seek called: position={_position.TotalSeconds}s");
        _ = _client.SetCurrentPositionAsync(_position.TotalSeconds);
        PositionChanged?.Invoke(this, _position);
    }

    public void SeekRelative(double seconds)
    {
        if (_client is null) return;
        var next = _position + TimeSpan.FromSeconds(seconds);
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (_duration > TimeSpan.Zero && next > _duration) next = _duration;
        _position = next;
        _ = _client.SetCurrentPositionAsync(_position.TotalSeconds);
        PositionChanged?.Invoke(this, _position);
    }

    public void Stop()
    {
        _logger.LogInformation("MpvPlayerService.Stop() 调用，当前位置：{Position}s", _position.TotalSeconds);
        if (_client is not null)
        {
            _ = _client.StopAsync();
        }
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _position = TimeSpan.Zero;
        UpdateState(PlaybackState.Stopped);
    }

    public void SetSubtitleTrack(int trackId)
    {
        if (_client is null) return;
        var value = trackId <= 0 ? "no" : trackId.ToString(CultureInfo.InvariantCulture);
        MpvNative.SetPropertyString(_client.Handle, "sid", value);
    }

    public void LoadExternalSubtitle(string subtitlePath)
    {
        if (_client is null || string.IsNullOrWhiteSpace(subtitlePath)) return;
        MpvNative.SetCommandString(_client.Handle, $"sub-add \"{subtitlePath.Replace("\"", "\\\"")}\"");
    }

    public void SetAudioTrack(int trackId)
    {
        if (_client is null) return;
        var value = trackId <= 0 ? "no" : trackId.ToString(CultureInfo.InvariantCulture);
        MpvNative.SetPropertyString(_client.Handle, "aid", value);
    }

    public void SetAudioBoost(double multiplier)
    {
        if (_client is null) return;
        var clamped = Math.Clamp(multiplier, 1.0, 2.0);
        MpvNative.SetPropertyString(_client.Handle, "volume-max",
            (clamped * 100).ToString("0.##", CultureInfo.InvariantCulture));
    }

    public void SetAspectRatio(string ratio)
    {
        if (_client is null) return;
        MpvNative.SetPropertyString(_client.Handle, "video-aspect-override", ratio);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try { _positionTimer.Dispose(); } catch { }

        if (_client is not null)
        {
            _client.DataNotify -= OnClientDataNotify;
            _client.ReachFileEnd -= OnClientFileEnd;
            _client.ReachFileLoaded -= OnClientFileLoaded;
            await _client.DisposeAsync();
            _client = null;
        }

        GC.SuppressFinalize(this);
    }

    // ======================== 事件处理 ========================

    private void OnClientDataNotify(object? sender, MpvClientNotifyEventArgs e)
    {
        switch (e.Id)
        {
            case MpvClientEventId.PositionChanged:
                if (e.Data is double pos)
                {
                    _position = TimeSpan.FromSeconds(pos);
                    PositionChanged?.Invoke(this, _position);
                }
                break;
            case MpvClientEventId.DurationChanged:
                if (e.Data is double dur)
                {
                    _duration = TimeSpan.FromSeconds(dur);
                }
                break;
            case MpvClientEventId.StateChanged:
                if (e.Data is MpvPlayerState mpvState)
                {
                    var mappedState = MapState(mpvState);
                    UpdateState(mappedState);
                }
                break;
            case MpvClientEventId.VolumeChanged:
                if (e.Data is double vol)
                {
                    _volume = vol;
                }
                break;
            case MpvClientEventId.SpeedChanged:
                if (e.Data is double spd)
                {
                    _speed = spd;
                }
                break;
        }
    }

    private void OnClientFileEnd(object? sender, EventArgs e)
    {
        _logger.LogInformation("收到 FileEnd 事件");
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        UpdateState(PlaybackState.Ended);
        FileEnded?.Invoke(this, EventArgs.Empty);
    }

    private void OnClientFileLoaded(object? sender, EventArgs e)
    {
        _logger.LogInformation("收到 FileLoaded 事件");
        UpdateState(PlaybackState.Playing);
    }

    // ======================== 内部方法 ========================

    private static PlaybackState MapState(MpvPlayerState mpvState) => mpvState switch
    {
        MpvPlayerState.Playing => PlaybackState.Playing,
        MpvPlayerState.Paused => PlaybackState.Paused,
        MpvPlayerState.Buffering => PlaybackState.Playing,
        MpvPlayerState.Seeking => PlaybackState.Playing,
        MpvPlayerState.End => PlaybackState.Ended,
        MpvPlayerState.Idle => PlaybackState.Idle,
        _ => PlaybackState.Idle,
    };

    private void UpdateState(PlaybackState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnPositionTick(object? state)
    {
        if (_client is null || !IsPlaying) return;

        try
        {
            var posTask = _client.GetCurrentPositionAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (posTask.IsSuccess)
            {
                var newPos = TimeSpan.FromSeconds(posTask.Value);
                if (Math.Abs((newPos - _position).TotalSeconds) > 0.5)
                {
                    _position = newPos;
                    PositionChanged?.Invoke(this, _position);
                }
            }

            var durTask = _client.GetDurationAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            if (durTask.IsSuccess)
            {
                _duration = TimeSpan.FromSeconds(durTask.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PositionTimer 后备查询异常");
        }
    }
}
