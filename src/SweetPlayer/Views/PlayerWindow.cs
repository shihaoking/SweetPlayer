using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using SweetPlayer.Services.Playback;
using WinRT.Interop;

namespace SweetPlayer.Views;

/// <summary>
/// 独立播放窗口：使用 AppWindow + DesktopWindowXamlSource 托管播放器 UI 覆盖层。
/// mpv 通过 wid 模式直接在此窗口内 GPU 零拷贝渲染。
/// </summary>
public sealed class PlayerWindow : IAsyncDisposable
{
    private readonly IMpvPlayerService _mpv;
    private readonly DispatcherQueue _dispatcherQueue;

    private AppWindow? _appWindow;
    private DesktopWindowXamlSource? _xamlSource;
    private bool _isFullScreen;
    private bool _disposed;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_FRAME = 0x0400;
    private const uint RDW_UPDATENOW = 0x0100;

    /// <summary>窗口句柄（HWND），供 mpv wid 模式使用。</summary>
    public IntPtr Handle { get; private set; }

    /// <summary>XAML 根元素。</summary>
    public XamlRoot? XamlRoot => _xamlSource?.Content?.XamlRoot;

    /// <summary>窗口关闭事件。</summary>
    public event EventHandler? Closed;

    /// <summary>覆盖层控件（由外部设置）。</summary>
    public UIElement? OverlayContent { get; private set; }

    public PlayerWindow(IMpvPlayerService mpv)
    {
        _mpv = mpv;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// 创建并显示播放窗口，初始化 mpv。
    /// </summary>
    public async Task ShowAsync()
    {
        _appWindow = AppWindow.Create();
        _appWindow.Title = "SweetPlayer";
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
        _appWindow.Destroying += OnWindowDestroying;
        _appWindow.Changed += OnWindowChanged;

        // 获取 HWND
        Handle = Win32Interop.GetWindowFromWindowId(_appWindow.Id);

        // 初始化 mpv（传入 HWND）
        await _mpv.InitializeAsync(Handle);

        // 创建 XAML 源
        _xamlSource = new DesktopWindowXamlSource();
        _xamlSource.Initialize(_appWindow.Id);

        // 设置初始内容为空 Grid（黑色背景）
        var rootGrid = new Microsoft.UI.Xaml.Controls.Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black),
        };
        _xamlSource.Content = rootGrid;

        // 调整 XAML 源位置
        UpdateXamlSourcePosition();

        _appWindow.Show();
    }

    /// <summary>
    /// 设置覆盖层 UI 内容。
    /// </summary>
    public void SetOverlayContent(UIElement content)
    {
        OverlayContent = content;
        if (_xamlSource is not null)
        {
            var rootGrid = new Microsoft.UI.Xaml.Controls.Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            rootGrid.Children.Add(content);
            _xamlSource.Content = rootGrid;
            UpdateXamlSourcePosition();
        }
    }

    /// <summary>
    /// 切换全屏模式。
    /// </summary>
    /// <returns>当前是否全屏。</returns>
    public bool ToggleFullScreen()
    {
        if (_appWindow is null) return false;

        if (_isFullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            _isFullScreen = false;
        }
        else
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            _isFullScreen = true;
        }

        return _isFullScreen;
    }

    /// <summary>是否当前全屏。</summary>
    public bool IsFullScreen => _isFullScreen;

    /// <summary>
    /// 关闭窗口。
    /// </summary>
    public void Close()
    {
        if (_appWindow is not null && !_disposed)
        {
            // 先清理 XAML 岛（必须在窗口销毁前完成，否则会破坏主窗口的非客户区）
            _xamlSource?.Dispose();
            _xamlSource = null;

            _appWindow.Destroy();
        }
    }

    /// <summary>
    /// 设置窗口焦点。
    /// </summary>
    public void Focus()
    {
        if (_xamlSource?.Content is UIElement element)
        {
            element.Focus(FocusState.Programmatic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_appWindow is not null)
        {
            _appWindow.Destroying -= OnWindowDestroying;
            _appWindow.Changed -= OnWindowChanged;
        }

        _xamlSource?.Dispose();
        _xamlSource = null;

        // 注意：不销毁 MpvClient（由 DI 管理 Singleton）
    }

    private void UpdateXamlSourcePosition()
    {
        if (_appWindow is null || _xamlSource is null) return;

        var size = _appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen
            ? _appWindow.Size
            : _appWindow.ClientSize;

        _xamlSource.SiteBridge.MoveAndResize(new Windows.Graphics.RectInt32(
            0, 0, size.Width, size.Height));
    }

    private async void OnWindowDestroying(AppWindow sender, object args)
    {
        // 确保 XAML 岛已清理（如果 Close() 未提前清理）
        if (_xamlSource is not null)
        {
            _xamlSource.Dispose();
            _xamlSource = null;
        }

        Closed?.Invoke(this, EventArgs.Empty);

        // 通过 Win32 强制恢复主窗口的标题栏和非客户区
        var mainWindow = App.MainWindow ?? MainWindowAccessor.Current;
        if (mainWindow is not null)
        {
            var mainHwnd = Win32Interop.GetWindowFromWindowId(mainWindow.AppWindow.Id);
            EnableWindow(mainHwnd, true);
            SetForegroundWindow(mainHwnd);
            RedrawWindow(mainHwnd, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME | RDW_UPDATENOW);
            mainWindow.Activate();
        }

        await DisposeAsync();
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidVisibilityChange || args.DidPresenterChange)
        {
            UpdateXamlSourcePosition();
        }
    }
}
