using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SweetPlayer.Services.Playback;
using System.Runtime.InteropServices;
using WinRT;

namespace SweetPlayer.Controls;


/// <summary>
/// 基于 SwapChainPanel 的 libmpv 渲染控件。
/// </summary>
/// <remarks>
/// 控件本身不持有 mpv 实例的所有权——通过 <see cref="AttachPlayer"/> 将外部
/// 注入的 <see cref="IMpvPlayerService"/> 与底层 SwapChainPanel 关联，由其负责
/// 把视频帧渲染到 swap chain 上。
/// </remarks>
public sealed partial class MpvPlayerControl : UserControl
{
    private IMpvPlayerService? _player;
    private bool _initialized;

    public MpvPlayerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 关联外部 MPV 播放服务，控件会向其暴露 SwapChainPanel 句柄用于渲染。
    /// </summary>
    public void AttachPlayer(IMpvPlayerService player)
    {
        _player = player;
        TryInitializeRenderer();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TryInitializeRenderer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // SwapChainPanel 卸载后不再渲染；播放服务由 DI 容器管理生命周期，这里不释放。
        _initialized = false;
    }

    private void OnVideoPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_player is null) return;
        var w = (int)Math.Max(1, e.NewSize.Width);
        var h = (int)Math.Max(1, e.NewSize.Height);

        // 如果渲染器尚未初始化（首次布局时尺寸可能为 0），在此重试。
        // 这是 OnLoaded 阶段 ActualWidth/Height 为 0 导致跳过初始化的补偿逻辑。
        if (!_initialized)
        {
            TryInitializeRenderer();
            return;
        }

        _player.Resize(w, h);
    }

    private void TryInitializeRenderer()
    {
        if (_initialized) return;
        if (_player is null) return;
        if (VideoPanel is null) return;
        if (VideoPanel.ActualWidth <= 0 || VideoPanel.ActualHeight <= 0) return;

        try
        {
            // 取 SwapChainPanel 的本机指针交给底层渲染。
            // WinUI 3 SwapChainPanel 内部处理 DPI 缩放，SwapChain 使用逻辑像素尺寸即可。
            var nativePtr = GetNativePointer(VideoPanel);
            //System.Diagnostics.Debug.WriteLine($"[MpvPlayerControl] GetNativePointer={nativePtr}, PanelSize={VideoPanel.ActualWidth}x{VideoPanel.ActualHeight}");
            _player.InitializeRenderer(
                nativePtr,
                (int)VideoPanel.ActualWidth,
                (int)VideoPanel.ActualHeight);

            _initialized = true;
            System.Diagnostics.Debug.WriteLine("[MpvPlayerControl] 渲染器初始化成功");
        }
        catch (Exception ex)
        {
            // 渲染初始化失败时降级；播放控制仍可用。
            _initialized = false;
            System.Diagnostics.Debug.WriteLine($"[MpvPlayerControl] TryInitializeRenderer 异常: {ex}");
        }
    }

    private static IntPtr GetNativePointer(SwapChainPanel panel)
    {
        try
        {
            // 通过 WinRT.CastExtensions 获取底层 IUnknown 指针。
            var native = panel.As<ISwapChainPanelNative>();
            return Marshal.GetIUnknownForObject(native);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    private interface ISwapChainPanelNative
    {
        [PreserveSig]
        int SetSwapChain(IntPtr swapChain);
    }
}
