using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// 基于 libmpv 的 IMpvPlayerService 实现骨架。
/// </summary>
/// <remarks>
/// 本实现尝试通过 P/Invoke 创建 mpv 实例并设置硬件解码、音量等基础选项；
/// 若运行环境未提供 mpv-2 动态库，将自动降级为 <see cref="MockMpvPlayerService"/>
/// 表现，所有外部 API 仍然可正常调用。SwapChainPanel 互操作渲染需要进一步在
/// MpvPlayerControl 中接入，本类负责管理 mpv 上下文与命令分发。
/// </remarks>
public class MpvPlayerService : IMpvPlayerService
{
    private readonly ILogger<MpvPlayerService> _logger;
    private readonly System.Threading.Timer _positionTimer;
    private IntPtr _mpvHandle = IntPtr.Zero;
    private IntPtr _renderContext = IntPtr.Zero;
    private bool _isLibAvailable;
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
        TryInitializeMpv();
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
            _volume = Math.Clamp(value, 0, 100);
            TrySetProperty("volume", _volume.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25, 4.0);
            TrySetProperty("speed", _speed.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? FileEnded;

    public void InitializeRenderer(IntPtr swapChainPanelHandle, int width, int height)
    {
        // SwapChainPanel 渲染需要构建 D3D11 设备并通过 mpv render API 输出帧。
        // 该集成需要原生互操作层，暂作为占位实现，避免在 mpv 缺失时阻塞构建。
        _logger.LogInformation("InitializeRenderer 调用：handle={Handle}, size={Width}x{Height}",
            swapChainPanelHandle, width, height);
    }

    public void Resize(int width, int height)
    {
        _logger.LogDebug("渲染区域调整：{Width}x{Height}", width, height);
    }

    public Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("文件路径不能为空", nameof(filePath));
        }

        UpdateState(PlaybackState.Loading);

        if (_isLibAvailable && _mpvHandle != IntPtr.Zero)
        {
            try
            {
                MpvInterop.mpv_command_string(_mpvHandle, $"loadfile \"{filePath.Replace("\"", "\\\"")}\"");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "loadfile 命令失败");
                UpdateState(PlaybackState.Error);
                return Task.CompletedTask;
            }
        }
        else
        {
            // 库不可用，模拟基础时长。
            _duration = TimeSpan.FromMinutes(90);
            _position = TimeSpan.Zero;
        }

        UpdateState(PlaybackState.Playing);
        _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return Task.CompletedTask;
    }

    public void Play()
    {
        TrySetProperty("pause", "no");
        UpdateState(PlaybackState.Playing);
        _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Pause()
    {
        TrySetProperty("pause", "yes");
        UpdateState(PlaybackState.Paused);
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Seek(TimeSpan position)
    {
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        TryCommand($"seek {_position.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} absolute");
        PositionChanged?.Invoke(this, _position);
    }

    public void SeekRelative(double seconds)
    {
        TryCommand($"seek {seconds.ToString("0.###", CultureInfo.InvariantCulture)} relative");
        var next = _position + TimeSpan.FromSeconds(seconds);
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (_duration > TimeSpan.Zero && next > _duration) next = _duration;
        _position = next;
        PositionChanged?.Invoke(this, _position);
    }

    public void Stop()
    {
        TryCommand("stop");
        _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _position = TimeSpan.Zero;
        UpdateState(PlaybackState.Stopped);
    }

    public void SetSubtitleTrack(int trackId)
    {
        var value = trackId <= 0 ? "no" : trackId.ToString(CultureInfo.InvariantCulture);
        TrySetProperty("sid", value);
    }

    public void LoadExternalSubtitle(string subtitlePath)
    {
        if (string.IsNullOrWhiteSpace(subtitlePath)) return;
        TryCommand($"sub-add \"{subtitlePath.Replace("\"", "\\\"")}\"");
    }

    public void SetAudioTrack(int trackId)
    {
        var value = trackId <= 0 ? "no" : trackId.ToString(CultureInfo.InvariantCulture);
        TrySetProperty("aid", value);
    }

    public void SetAudioBoost(double multiplier)
    {
        var clamped = Math.Clamp(multiplier, 1.0, 2.0);
        // mpv volume-max 控制最大可达音量，volume 设为期望放大值。
        TrySetProperty("volume-max", (clamped * 100).ToString("0.##", CultureInfo.InvariantCulture));
    }

    public void SetAspectRatio(string ratio)
    {
        TrySetProperty("video-aspect-override", ratio);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _positionTimer.Dispose();
        }
        catch { /* ignore */ }

        if (_renderContext != IntPtr.Zero)
        {
            try { MpvInterop.mpv_render_context_free(_renderContext); }
            catch { /* ignore */ }
            _renderContext = IntPtr.Zero;
        }

        if (_mpvHandle != IntPtr.Zero)
        {
            try { MpvInterop.mpv_terminate_destroy(_mpvHandle); }
            catch { /* ignore */ }
            _mpvHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    // ---------- 内部 ----------

    private void TryInitializeMpv()
    {
        try
        {
            _mpvHandle = MpvInterop.mpv_create();
            if (_mpvHandle == IntPtr.Zero)
            {
                _logger.LogWarning("mpv_create 返回空指针，将使用降级模式");
                return;
            }

            MpvInterop.mpv_set_option_string(_mpvHandle, "hwdec", "d3d11va");
            MpvInterop.mpv_set_option_string(_mpvHandle, "vo", "gpu-next");
            MpvInterop.mpv_set_option_string(_mpvHandle, "keep-open", "yes");
            MpvInterop.mpv_set_option_string(_mpvHandle, "audio-fallback-to-null", "yes");

            var ret = MpvInterop.mpv_initialize(_mpvHandle);
            if (ret != MpvInterop.MPV_ERROR_SUCCESS)
            {
                _logger.LogWarning("mpv_initialize 失败：{Error}", ret);
                MpvInterop.mpv_terminate_destroy(_mpvHandle);
                _mpvHandle = IntPtr.Zero;
                return;
            }

            _isLibAvailable = true;
            _logger.LogInformation("libmpv 初始化成功，硬件解码：d3d11va");
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "未找到 libmpv 动态库（mpv-2.dll），降级到模拟播放");
            _isLibAvailable = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化 libmpv 时发生异常");
            _isLibAvailable = false;
        }
    }

    private void TrySetProperty(string name, string value)
    {
        if (!_isLibAvailable || _mpvHandle == IntPtr.Zero) return;
        try
        {
            MpvInterop.mpv_set_property_string(_mpvHandle, name, value);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "mpv_set_property_string {Name} 异常", name);
        }
    }

    private void TryCommand(string command)
    {
        if (!_isLibAvailable || _mpvHandle == IntPtr.Zero) return;
        try
        {
            MpvInterop.mpv_command_string(_mpvHandle, command);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "mpv_command_string '{Command}' 异常", command);
        }
    }

    private string? TryGetProperty(string name)
    {
        if (!_isLibAvailable || _mpvHandle == IntPtr.Zero) return null;
        try
        {
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, name);
            if (ptr == IntPtr.Zero) return null;
            var s = Marshal.PtrToStringUTF8(ptr);
            MpvInterop.mpv_free(ptr);
            return s;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateState(PlaybackState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnPositionTick(object? state)
    {
        try
        {
            if (_isLibAvailable)
            {
                var posStr = TryGetProperty("time-pos");
                var durStr = TryGetProperty("duration");
                if (double.TryParse(posStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var pos))
                {
                    _position = TimeSpan.FromSeconds(pos);
                }
                if (double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                {
                    _duration = TimeSpan.FromSeconds(dur);
                }
            }
            else if (IsPlaying)
            {
                _position += TimeSpan.FromSeconds(1);
                if (_duration > TimeSpan.Zero && _position >= _duration)
                {
                    _position = _duration;
                    _positionTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    UpdateState(PlaybackState.Ended);
                    FileEnded?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            PositionChanged?.Invoke(this, _position);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PositionTimer 异常");
        }
    }
}
