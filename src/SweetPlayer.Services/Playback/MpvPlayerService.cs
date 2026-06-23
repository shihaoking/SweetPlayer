using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using LibMpv.Client;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Mpv = LibMpv.Client.LibMpv;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// 基于 LibMpv.Client 的 IMpvPlayerService 实现。
/// 使用软件渲染 (sw) 模式将帧绘制到 SwapChainPanel。
/// </summary>
public unsafe class MpvPlayerService : IMpvPlayerService
{
    private readonly ILogger<MpvPlayerService> _logger;
    private readonly System.Threading.Timer _positionTimer;
    private readonly object _resizeLock = new object();

    private MpvHandle* _mpvHandle;
    private MpvRenderContext* _renderContext;
    private Device? _d3d11Device;
    private SwapChain1? _swapChain;
    private Texture2D? _stagingTexture;
    private byte[]? _frameBuffer;
    private ManualResetEventSlim? _renderUpdateEvent;
    private Thread? _renderThread;
    private Task? _eventLoopTask;
    private CancellationTokenSource? _renderCancellation;
    private CancellationTokenSource? _eventLoopCancellation;
    private MpvRenderContextSetUpdateCallbackCallback? _updateCallbackDelegate;
    private long _renderedFrameCount;
    private long _callbackCount;
    private SynchronizationContext? _uiSyncContext;
    private IntPtr _swapChainPanelHandle;
    private int _initWidth;
    private int _initHeight;

    private int _pendingWidth;
    private int _pendingHeight;
    private bool _hasPendingResize;
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
            Mpv.MpvSetPropertyString(_mpvHandle, "volume", _volume.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.25, 4.0);
            Mpv.MpvSetPropertyString(_mpvHandle, "speed", _speed.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? FileEnded;

    public void InitializeRenderer(IntPtr swapChainPanelHandle, int width, int height)
    {
        _logger.LogInformation("InitializeRenderer: handle={Handle}, size={Width}x{Height}",
            swapChainPanelHandle, width, height);

        // 捕获 UI 线程同步上下文，用于后续 SetSwapChain 和 Present 分派
        _uiSyncContext = SynchronizationContext.Current;
        _logger.LogInformation("UI SynchronizationContext: {Ctx}", _uiSyncContext is not null ? _uiSyncContext.GetType().Name : "null");

        if (!_isLibAvailable || _mpvHandle == null)
        {
            _logger.LogWarning("libmpv 不可用，跳过渲染器初始化");
            return;
        }

        // 存储参数，由专用渲染线程执行 D3D11 设备创建
        _swapChainPanelHandle = swapChainPanelHandle;
        _initWidth = width;
        _initHeight = height;

        // 启动专用渲染线程（D3D11 ImmediateContext 绑定到创建线程）
        _renderCancellation = new CancellationTokenSource();
        _renderThread = new Thread(RenderThreadEntry)
        {
            Name = "MpvRenderThread",
            IsBackground = true
        };
        _renderThread.Start();
    }

    /// <summary>
    /// 专用渲染线程入口：在同一个线程上创建 D3D11 设备/SwapChain/mpv渲染上下文并运行渲染循环。
    /// D3D11 ImmediateContext 只能从创建设备的线程调用，否则会返回 E_INVALIDARG。
    /// </summary>
    private void RenderThreadEntry()
    {
        try
        {
            _logger.LogInformation("渲染线程启动，创建 D3D11 设备...");

            // 尝试使用 Debug 层设备以获取详细的 DirectX 错误信息
            try
            {
                _d3d11Device = new Device(DriverType.Hardware,
                    DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport | DeviceCreationFlags.Debug);
                _logger.LogInformation("D3D11 设备创建成功 (Debug模式, ThreadId={Tid})", Environment.CurrentManagedThreadId);
            }
            catch (Exception debugEx)
            {
                _logger.LogWarning(debugEx, "D3D11 Debug 设备创建失败，回退到普通设备");
                _d3d11Device = new Device(DriverType.Hardware,
                    DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);
                _logger.LogInformation("D3D11 设备创建成功 (普通模式, ThreadId={Tid})", Environment.CurrentManagedThreadId);
            }

            var swapChainDesc = new SwapChainDescription1
            {
                Width = _initWidth,
                Height = _initHeight,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            using (var dxgiDevice = _d3d11Device.QueryInterface<SharpDX.DXGI.Device>())
            using (var adapter = dxgiDevice.Adapter)
            using (var factory = adapter.GetParent<Factory2>())
            {
                _swapChain = new SwapChain1(factory, _d3d11Device, ref swapChainDesc);
                _logger.LogInformation("SwapChain 创建成功");
            }

            // SwapChain 关联到 SwapChainPanel 必须在 UI 线程
            if (_swapChainPanelHandle != IntPtr.Zero && _uiSyncContext is not null)
            {
                var assocDone = new ManualResetEventSlim(false);
                _uiSyncContext.Post(_ =>
                {
                    try
                    {
                        var nativePanel = Marshal.GetObjectForIUnknown(_swapChainPanelHandle) as ISwapChainPanelNative;
                        _logger.LogInformation("ISwapChainPanelNative 解析: {Result}",
                            nativePanel is not null ? "成功" : "失败(null)");
                        if (nativePanel != null)
                        {
                            var hr = nativePanel.SetSwapChain(_swapChain.NativePointer);
                            _logger.LogInformation("SetSwapChain HRESULT={HR}", hr);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SetSwapChain 分派失败");
                    }
                    finally
                    {
                        assocDone.Set();
                    }
                }, null);
                assocDone.Wait(5000);
                assocDone.Dispose();
            }

            // 创建 mpv 渲染上下文并进入渲染循环
            CreateMpvRenderContext();
            RenderLoop(_renderCancellation!.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "渲染线程发生异常");
            _swapChain?.Dispose();
            _swapChain = null;
            _d3d11Device?.Dispose();
            _d3d11Device = null;
        }
    }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        lock (_resizeLock)
        {
            _pendingWidth = width;
            _pendingHeight = height;
            _hasPendingResize = true;
        }
    }

    public Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        UpdateState(PlaybackState.Loading);

        if (_isLibAvailable && _mpvHandle != null)
        {
            try
            {
                Mpv.MpvCommandString(_mpvHandle, $"loadfile \"{filePath.Replace("\"", "\\\"")}\"");
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
            _duration = TimeSpan.FromMinutes(90);
            _position = TimeSpan.Zero;
        }

        UpdateState(PlaybackState.Playing);
        _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return Task.CompletedTask;
    }

    public void Play()
    {
        Mpv.MpvSetPropertyString(_mpvHandle, "pause", "no");
        UpdateState(PlaybackState.Playing);
        _positionTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public void Pause()
    {
        Mpv.MpvSetPropertyString(_mpvHandle, "pause", "yes");
        UpdateState(PlaybackState.Paused);
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause(); else Play();
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
        Mpv.MpvSetPropertyString(_mpvHandle, "sid", value);
    }

    public void LoadExternalSubtitle(string subtitlePath)
    {
        if (string.IsNullOrWhiteSpace(subtitlePath)) return;
        TryCommand($"sub-add \"{subtitlePath.Replace("\"", "\\\"")}\"");
    }

    public void SetAudioTrack(int trackId)
    {
        var value = trackId <= 0 ? "no" : trackId.ToString(CultureInfo.InvariantCulture);
        Mpv.MpvSetPropertyString(_mpvHandle, "aid", value);
    }

    public void SetAudioBoost(double multiplier)
    {
        var clamped = Math.Clamp(multiplier, 1.0, 2.0);
        Mpv.MpvSetPropertyString(_mpvHandle, "volume-max", (clamped * 100).ToString("0.##", CultureInfo.InvariantCulture));
    }

    public void SetAspectRatio(string ratio)
    {
        Mpv.MpvSetPropertyString(_mpvHandle, "video-aspect-override", ratio);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _positionTimer.Dispose(); } catch { }

        // 停止事件循环（必须在 render loop 之前，且在 mpv_terminate_destroy 之前）
        _eventLoopCancellation?.Cancel();
        try { _eventLoopTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        _eventLoopCancellation?.Dispose();

        // 停止渲染线程
        _renderCancellation?.Cancel();
        try { _renderThread?.Join(TimeSpan.FromSeconds(5)); } catch { }

        _renderCancellation?.Dispose();
        _renderUpdateEvent?.Dispose();

        if (_renderContext != null)
        {
            try { Mpv.MpvRenderContextFree(_renderContext); } catch { }
            _renderContext = null;
        }

        if (_swapChain != null) { try { _swapChain.Dispose(); } catch { } _swapChain = null; }
        if (_stagingTexture != null) { try { _stagingTexture.Dispose(); } catch { } _stagingTexture = null; }
        if (_d3d11Device != null) { try { _d3d11Device.Dispose(); } catch { } _d3d11Device = null; }

        if (_mpvHandle != null)
        {
            try { Mpv.MpvTerminateDestroy(_mpvHandle); } catch { }
            _mpvHandle = null;
        }

        GC.SuppressFinalize(this);
    }

    // ======================== 内部方法 ========================

    private void CreateMpvRenderContext()
    {
        try
        {
            _logger.LogInformation("尝试创建软件渲染 (sw) 上下文...");

            // 使用 LibMpv.Client 的 MpvRenderParam 结构体（布局已由库保证正确）
            var apiType = Marshal.StringToHGlobalAnsi("sw");
            try
            {
                var parameters = new MpvRenderParam[]
                {
                    new MpvRenderParam { Type = MpvRenderParamType.MpvRenderParamApiType, Data = (void*)apiType },
                    new MpvRenderParam { Type = MpvRenderParamType.MpvRenderParamInvalid, Data = null }
                };

                fixed (MpvRenderParam* paramsPtr = parameters)
                {
                    MpvRenderContext* localCtx = null;
                    var ret = Mpv.MpvRenderContextCreate(&localCtx, _mpvHandle, paramsPtr);
                    if (ret != 0)
                    {
                        var errStr = Mpv.MpvErrorString(ret);
                        _logger.LogError("mpv_render_context_create 失败: {Error} (code={Code})", errStr, ret);
                        throw new InvalidOperationException($"创建 mpv 渲染上下文失败: {errStr} ({ret})");
                    }
                    _renderContext = localCtx;
                }

                _logger.LogInformation("mpv_render_context 创建成功（软件渲染模式）");
            }
            finally
            {
                Marshal.FreeHGlobal(apiType);
            }

            // 注册渲染更新回调
            _renderUpdateEvent = new ManualResetEventSlim(false);
            _updateCallbackDelegate = OnMpvRenderUpdate;
            Mpv.MpvRenderContextSetUpdateCallback(_renderContext, _updateCallbackDelegate, null);
            _logger.LogInformation("渲染更新回调已注册");

            // 渲染循环由 RenderThreadEntry 在当前线程启动
            _logger.LogInformation("mpv 渲染上下文准备就绪，即将进入渲染循环");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 mpv 渲染上下文时发生异常");
            _renderContext = null;
            throw;
        }
    }

    private void OnMpvRenderUpdate(void* ctx)
    {
        Interlocked.Increment(ref _callbackCount);
        _renderUpdateEvent?.Set();
    }

    /// <summary>
    /// 后台事件循环：持续调用 mpv_wait_event 处理 mpv 内部事件。
    /// 参考 LibMpv-OpenGL 示例的 MpvSimpleEventLoop，这是驱动 mpv 解码和渲染回调的必要条件。
    /// </summary>
    private void EventLoop(CancellationToken cancellationToken)
    {
        _logger.LogInformation("事件循环开始 (ThreadId={Tid})", Environment.CurrentManagedThreadId);
        long eventCount = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_mpvHandle == null) break;

                var eventPtr = Mpv.MpvWaitEvent(_mpvHandle, 0.5);
                if (eventPtr == null) continue;

                var mpvEvent = *eventPtr;
                var eventId = (int)mpvEvent.EventId;
                if (eventId == 0) continue; // MPV_EVENT_NONE

                eventCount++;
                if (eventCount <= 20 || eventCount % 100 == 0)
                    _logger.LogDebug("mpv 事件 #{Count}: id={EventId}", eventCount, eventId);

                switch (eventId)
                {
                    case 1: // MPV_EVENT_SHUTDOWN
                        _logger.LogInformation("收到 MPV_EVENT_SHUTDOWN");
                        return;
                    case 6: // MPV_EVENT_START_FILE
                        _logger.LogInformation("收到 MPV_EVENT_START_FILE");
                        break;
                    case 7: // MPV_EVENT_END_FILE
                        _logger.LogInformation("收到 MPV_EVENT_END_FILE");
                        break;
                    case 8: // MPV_EVENT_FILE_LOADED
                        _logger.LogInformation("收到 MPV_EVENT_FILE_LOADED");
                        break;
                    case 21: // MPV_EVENT_PLAYBACK_RESTART
                        _logger.LogInformation("收到 MPV_EVENT_PLAYBACK_RESTART");
                        break;
                    case 23: // MPV_EVENT_VIDEO_RECONFIG
                        _logger.LogInformation("收到 MPV_EVENT_VIDEO_RECONFIG");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("事件循环被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件循环发生异常");
        }
        finally
        {
            _logger.LogInformation("事件循环结束");
        }
    }

    private void RenderLoop(CancellationToken cancellationToken)
    {
        _logger.LogInformation("渲染循环开始, renderContext={Ctx}, swapChain={Sc}, device={Dev}",
            _renderContext is not null ? "OK" : "null",
            _swapChain is not null ? "OK" : "null",
            _d3d11Device is not null ? "OK" : "null");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // 等待更新回调或超时（短超时确保持续驱动 mpv 帧流水线）
                _renderUpdateEvent?.Wait(16, cancellationToken);
                _renderUpdateEvent?.Reset();

                if (_renderContext == null || _swapChain == null || _d3d11Device == null)
                    continue;

                // 处理 resize
                lock (_resizeLock)
                {
                    if (_hasPendingResize)
                    {
                        try
                        {
                            _swapChain.ResizeBuffers(0, _pendingWidth, _pendingHeight, Format.Unknown, SwapChainFlags.None);
                            // 重建 staging 纹理
                            _stagingTexture?.Dispose();
                            _stagingTexture = new Texture2D(_d3d11Device, new Texture2DDescription
                            {
                                Width = _pendingWidth,
                                Height = _pendingHeight,
                                MipLevels = 1,
                                ArraySize = 1,
                                Format = Format.B8G8R8A8_UNorm,
                                SampleDescription = new SampleDescription(1, 0),
                                Usage = ResourceUsage.Staging,
                                CpuAccessFlags = CpuAccessFlags.Write,
                                BindFlags = BindFlags.None,
                                OptionFlags = ResourceOptionFlags.None
                            });
                            _hasPendingResize = false;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "调整 SwapChain 缓冲区大小失败");
                            _hasPendingResize = false;
                        }
                    }
                }

                try
                {
                    var ctx = _d3d11Device.ImmediateContext;

                    // 获取后缓冲区尺寸
                    var backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
                    var bbDesc = backBuffer.Description;
                    var w = bbDesc.Width;
                    var h = bbDesc.Height;
                    backBuffer.Dispose();

                    // 分配 CPU 帧缓冲区（完全避开 MapSubresource）
                    var stride = (uint)(w * 4);
                    var frameSize = (int)(stride * h);
                    if (_frameBuffer == null || _frameBuffer.Length != frameSize)
                    {
                        _frameBuffer = new byte[frameSize];
                        _logger.LogInformation("分配帧缓冲区: {W}x{H}, stride={Stride}, size={Size} bytes", w, h, stride, frameSize);
                    }

                    // 让 mpv 渲染到 CPU 缓冲区
                    var size = new[] { w, h };
                    var strideArr = new[] { stride };
                    var formatBytes = System.Text.Encoding.UTF8.GetBytes("bgr0\0");

                    fixed (byte* framePtr = _frameBuffer)
                    fixed (int* sizePtr = size)
                    fixed (uint* stridePtr = strideArr)
                    fixed (byte* formatPtr = formatBytes)
                    {
                        var renderParams = new MpvRenderParam[]
                        {
                            new() { Type = MpvRenderParamType.MpvRenderParamSwSize, Data = sizePtr },
                            new() { Type = MpvRenderParamType.MpvRenderParamSwFormat, Data = formatPtr },
                            new() { Type = MpvRenderParamType.MpvRenderParamSwStride, Data = stridePtr },
                            new() { Type = MpvRenderParamType.MpvRenderParamSwPointer, Data = framePtr },
                            new() { Type = MpvRenderParamType.MpvRenderParamInvalid, Data = null }
                        };
                        fixed (MpvRenderParam* rpPtr = renderParams)
                        {
                            var ret = Mpv.MpvRenderContextRender(_renderContext, rpPtr);
                            if (ret < 0)
                            {
                                _logger.LogWarning("mpv_render_context_render 失败: {Error} (code={Code})",
                                    Mpv.MpvErrorString(ret), ret);
                                continue;
                            }
                        }
                    }

                    // 用 UpdateSubresource 将帧数据上传到后缓冲区
                    backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
                    try
                    {
                        fixed (byte* framePtr = _frameBuffer)
                        {
                            ctx.UpdateSubresource(backBuffer, 0, null, (IntPtr)framePtr, (int)stride, 0);
                        }
                    }
                    finally
                    {
                        backBuffer.Dispose();
                    }

                    // WinUI 3 SwapChainPanel 的 Present 必须在 UI 线程调用
                    if (_uiSyncContext is not null)
                    {
                        var presentEvent = new ManualResetEventSlim(false);
                        _uiSyncContext.Post(_ =>
                        {
                            try
                            {
                                _swapChain.Present(1, PresentFlags.None);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "UI 线程 Present 失败");
                            }
                            finally
                            {
                                presentEvent.Set();
                            }
                        }, null);
                        presentEvent.Wait(200);
                        presentEvent.Dispose();
                    }
                    else
                    {
                        _swapChain.Present(1, PresentFlags.None);
                    }

                    var fc = Interlocked.Increment(ref _renderedFrameCount);
                    if (fc <= 3 || fc % 300 == 0)
                    {
                        _logger.LogInformation("渲染帧 #{Count}, 累计回调={Callbacks}", fc, Interlocked.Read(ref _callbackCount));
                    }
                }
                catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved ||
                                                   ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
                {
                    _logger.LogError(ex, "D3D11 设备丢失或重置");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "渲染循环中发生异常");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("渲染循环被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "渲染循环发生未预期异常");
        }
        finally
        {
            _logger.LogInformation("渲染循环结束");
        }
    }

    private void TryInitializeMpv()
    {
        try
        {
            // 初始化 LibMpv.Client 加载器
            Mpv.UseLibMpv(2);

            _mpvHandle = Mpv.MpvCreate();
            if (_mpvHandle == null)
            {
                _logger.LogWarning("MpvCreate 返回 null，降级模式");
                return;
            }

            Mpv.MpvSetOptionString(_mpvHandle, "hwdec", "auto");
            var voRet = Mpv.MpvSetOptionString(_mpvHandle, "vo", "libmpv");
            _logger.LogInformation("设置 vo=libmpv 返回值：{Ret}", voRet);
            Mpv.MpvSetOptionString(_mpvHandle, "keep-open", "yes");
            Mpv.MpvSetOptionString(_mpvHandle, "audio-fallback-to-null", "yes");

            var ret = Mpv.MpvInitialize(_mpvHandle);
            if (ret != 0)
            {
                _logger.LogWarning("MpvInitialize 失败: {Error} (code={Code})", Mpv.MpvErrorString(ret), ret);
                Mpv.MpvTerminateDestroy(_mpvHandle);
                _mpvHandle = null;
                return;
            }

            _isLibAvailable = true;
            _logger.LogInformation("libmpv 初始化成功（通过 LibMpv.Client）");

            // 启动事件循环（参考 LibMpv-OpenGL 示例：必须在后台线程持续调用 mpv_wait_event）
            _eventLoopCancellation = new CancellationTokenSource();
            _eventLoopTask = Task.Run(() => EventLoop(_eventLoopCancellation.Token), _eventLoopCancellation.Token);
            _logger.LogInformation("mpv 事件循环已启动");
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "未找到 libmpv 动态库，降级到模拟播放");
            _isLibAvailable = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化 libmpv 时发生异常");
            _isLibAvailable = false;
        }
    }

    private void TryCommand(string command)
    {
        if (!_isLibAvailable || _mpvHandle == null) return;
        try { Mpv.MpvCommandString(_mpvHandle, command); }
        catch (Exception ex) { _logger.LogDebug(ex, "mpv command '{Command}' 异常", command); }
    }

    private string? TryGetProperty(string name)
    {
        if (!_isLibAvailable || _mpvHandle == null) return null;
        try
        {
            var ptr = Mpv.MpvGetPropertyString(_mpvHandle, name);
            if (ptr == null) return null;
            var s = Marshal.PtrToStringUTF8((IntPtr)ptr);
            Mpv.MpvFree(ptr);
            return s;
        }
        catch { return null; }
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
                    _position = TimeSpan.FromSeconds(pos);
                if (double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                    _duration = TimeSpan.FromSeconds(dur);
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
