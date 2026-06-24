namespace SweetPlayer.Services.Playback;

using Microsoft.Extensions.Logging;

/// <summary>
/// 辅助类：在非 unsafe 上下文中等待任务完成（带超时）。
/// MpvPlayerService 类为 unsafe，不能在其中使用 await，故抽出到这里。
/// </summary>
internal static class RendererReadyWaiter
{
    public static async Task WaitAsync(Task<bool> readyTask, TimeSpan timeout, ILogger logger)
    {
        var completed = await Task.WhenAny(readyTask, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != readyTask)
        {
            logger.LogWarning("等待渲染器就绪超时 ({Timeout}ms)", timeout.TotalMilliseconds);
        }
    }
}

/// <summary>
/// 播放器状态枚举。
/// </summary>
public enum PlaybackState
{
    /// <summary>未加载任何媒体。</summary>
    Idle,
    /// <summary>正在加载文件。</summary>
    Loading,
    /// <summary>正在播放。</summary>
    Playing,
    /// <summary>已暂停。</summary>
    Paused,
    /// <summary>已停止。</summary>
    Stopped,
    /// <summary>播放结束。</summary>
    Ended,
    /// <summary>发生错误。</summary>
    Error,
}

/// <summary>
/// 底层 MPV 播放服务接口。
/// </summary>
/// <remarks>
/// 直接对应 libmpv 的能力封装；上层业务逻辑请使用 <see cref="IPlaybackControlService"/>。
/// </remarks>
public interface IMpvPlayerService : IDisposable
{
    /// <summary>是否正在播放。</summary>
    bool IsPlaying { get; }

    /// <summary>是否已暂停。</summary>
    bool IsPaused { get; }

    /// <summary>当前播放位置。</summary>
    TimeSpan Position { get; }

    /// <summary>当前媒体总时长。</summary>
    TimeSpan Duration { get; }

    /// <summary>音量（0-100）。</summary>
    double Volume { get; set; }

    /// <summary>播放速度（0.5-2.0）。</summary>
    double Speed { get; set; }

    /// <summary>使用 SwapChainPanel 句柄初始化渲染上下文（WinUI 调用）。</summary>
    /// <param name="swapChainPanelHandle">SwapChainPanel 的本机指针。</param>
    /// <param name="width">渲染区域宽度（像素）。</param>
    /// <param name="height">渲染区域高度（像素）。</param>
    void InitializeRenderer(IntPtr swapChainPanelHandle, int width, int height);

    /// <summary>渲染上下文是否已创建完成。</summary>
    bool IsRendererReady { get; }

    /// <summary>等待渲染上下文创建完成（在调用 LoadFileAsync 前使用，避免 mpv vo 初始化时报 'No render context set'）。</summary>
    Task WaitForRendererReadyAsync(TimeSpan timeout);

    /// <summary>渲染目标尺寸变化时调用。</summary>
    void Resize(int width, int height);

    /// <summary>异步加载视频文件并准备播放。</summary>
    Task LoadFileAsync(string filePath);

    /// <summary>开始/恢复播放。</summary>
    void Play();

    /// <summary>暂停播放。</summary>
    void Pause();

    /// <summary>切换播放/暂停。</summary>
    void TogglePlayPause();

    /// <summary>跳转到指定位置。</summary>
    void Seek(TimeSpan position);

    /// <summary>相对当前位置跳转（正数前进，负数后退，单位：秒）。</summary>
    void SeekRelative(double seconds);

    /// <summary>停止播放并卸载文件。</summary>
    void Stop();

    /// <summary>释放渲染上下文（SwapChain、D3D11 设备等），但保留 mpv 实例。用于退出播放页时清理资源。</summary>
    void DisposeRenderer();

    /// <summary>切换字幕轨道（0 表示关闭）。</summary>
    void SetSubtitleTrack(int trackId);

    /// <summary>加载外挂字幕文件。</summary>
    void LoadExternalSubtitle(string subtitlePath);

    /// <summary>切换音频轨道。</summary>
    void SetAudioTrack(int trackId);

    /// <summary>设置音频增强倍数（1.0 - 2.0）。</summary>
    void SetAudioBoost(double multiplier);

    /// <summary>设置画面比例（"auto"、"16:9"、"4:3"、"-1" = 拉伸填充）。</summary>
    void SetAspectRatio(string ratio);

    /// <summary>播放位置变化（每秒触发）。</summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>播放状态变化。</summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>文件播放结束事件。</summary>
    event EventHandler? FileEnded;
}
