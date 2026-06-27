using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using SweetPlayer.Services.Playback;
using Windows.Graphics;
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
        Log("PlayerWindow 实例已创建");
    }

    /// <summary>
    /// 创建并显示播放窗口，初始化 mpv。
    /// </summary>
    public async Task ShowAsync()
    {
        Log("ShowAsync 开始");

        _appWindow = AppWindow.Create();
        _appWindow.AssociateWithDispatcherQueue(_dispatcherQueue);
        _appWindow.Title = "SweetPlayer";

        // 恢复标题栏，并将 XAML 内容拓展到标题栏区域
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: true);
        }
        _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // 标题栏按钮颜色：透明背景 + 白色图标，融入深色视频背景
        _appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        _appWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
        _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
        _appWindow.TitleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
        _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(80, 255, 255, 255);
        _appWindow.TitleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
        _appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(150, 255, 255, 255);

        // 初始尺寸：视频内容区域 1920×1130，加上标题栏高度（动态获取）
        var titleBarHeight = (int)_appWindow.TitleBar.Height;
        if (titleBarHeight <= 0) titleBarHeight = 48; // 备用默认值
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1130 + titleBarHeight));
        Log($"ShowAsync: 标题栏高度={titleBarHeight}, 窗口大小=1920x{1130 + titleBarHeight}");
        _appWindow.Destroying += OnWindowDestroying;
        _appWindow.Changed += OnWindowChanged;
        Log($"AppWindow 已创建并关联 DispatcherQueue, Id={_appWindow.Id.Value}");

        // 获取 HWND
        Handle = Win32Interop.GetWindowFromWindowId(_appWindow.Id);
        Log($"HWND 已获取: 0x{Handle:X}");

        // 初始化 mpv（传入 HWND）
        Log("正在初始化 mpv...");
        await _mpv.InitializeAsync(Handle);
        Log("mpv 初始化完成");

        // 创建 XAML 源
        _xamlSource = new DesktopWindowXamlSource();
        _xamlSource.Initialize(_appWindow.Id);
        Log("DesktopWindowXamlSource 已创建并初始化");

        // 设置初始内容为空 Grid（黑色背景）
        var rootGrid = new Microsoft.UI.Xaml.Controls.Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black),
        };
        _xamlSource.Content = rootGrid;

        // 调整 XAML 源位置
        UpdateXamlSourcePosition();

        _appWindow.Show();
        Log("ShowAsync 完成, 窗口已显示");
    }

    /// <summary>
    /// 设置覆盖层 UI 内容。
    /// </summary>
    public void SetOverlayContent(UIElement content)
    {
        Log($"SetOverlayContent: content={content.GetType().Name}");
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
            Log("SetOverlayContent 完成, XAML 内容已设置");
        }
        else
        {
            Log("SetOverlayContent 警告: _xamlSource 为 null, 跳过设置");
        }
    }

    /// <summary>
    /// 切换全屏模式。
    /// </summary>
    /// <returns>当前是否全屏。</returns>
    public bool ToggleFullScreen()
    {
        if (_appWindow is null)
        {
            Log("ToggleFullScreen: _appWindow 为 null, 返回 false");
            return false;
        }

        if (_isFullScreen)
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            _isFullScreen = false;
            Log("ToggleFullScreen: 退出全屏 → Default");
        }
        else
        {
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            _isFullScreen = true;
            Log("ToggleFullScreen: 进入全屏 → FullScreen");
        }

        return _isFullScreen;
    }

    /// <summary>是否当前全屏。</summary>
    public bool IsFullScreen => _isFullScreen;

    /// <summary>
    /// 更新标题栏可拖拽区域（排除系统按钮区域）。
    /// </summary>
    public void UpdateTitleBarDragRects()
    {
        if (_appWindow is null) return;
        var tb = _appWindow.TitleBar;
        if (!tb.ExtendsContentIntoTitleBar) return;
        var height = (int)tb.Height;
        if (height <= 0) return;
        var rightInset = (int)tb.RightInset;
        var leftInset = (int)tb.LeftInset;
        var width = _appWindow.Size.Width;
        tb.SetDragRectangles([new RectInt32(leftInset, 0, width - rightInset - leftInset, height)]);
        Log($"UpdateTitleBarDragRects: leftInset={leftInset}, rightInset={rightInset}, height={height}, width={width}");
    }

    /// <summary>
    /// 关闭窗口。
    /// </summary>
    public void Close()
    {
        Log($"Close 调用: _appWindow={(_appWindow is not null)}, _disposed={_disposed}");
        _appWindow?.Destroy();
        Log("Close: _appWindow.Destroy() 已调用");
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
        Log($"DisposeAsync 调用: _disposed={_disposed}");
        if (_disposed) return;
        _disposed = true;

        if (_appWindow is not null)
        {
            _appWindow.Destroying -= OnWindowDestroying;
            _appWindow.Changed -= OnWindowChanged;
            _appWindow.Destroy();
            Log("DisposeAsync: AppWindow 已销毁");
        }

        _xamlSource?.Dispose();
        Log("DisposeAsync: XamlSource 已 Dispose");

        // 销毁 mpv 实例（与 MpvKernel 行为一致：窗口关闭即释放播放引擎）
        await _mpv.DisposeAsync();
        Log("DisposeAsync: MpvPlayerService 已 Dispose");
    }

    private void UpdateXamlSourcePosition()
    {
        if (_appWindow is null || _xamlSource is null) return;

        var isFullScreen = _appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        var size = isFullScreen
            ? _appWindow.Size
            : _appWindow.ClientSize;

        _xamlSource.SiteBridge.MoveAndResize(new Windows.Graphics.RectInt32(
            0, 0, size.Width, size.Height));
        Log($"UpdateXamlSourcePosition: size={size.Width}x{size.Height}, fullScreen={isFullScreen}");
    }

    private async void OnWindowDestroying(AppWindow sender, object args)
    {
        Log("OnWindowDestroying 触发");
        Closed?.Invoke(this, EventArgs.Empty);
        Log("OnWindowDestroying: Closed 事件已触发");
        await DisposeAsync();
        Log("OnWindowDestroying 结束");
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidVisibilityChange || args.DidPresenterChange)
        {
            Log($"OnWindowChanged: DidSize={args.DidSizeChange}, DidVisibility={args.DidVisibilityChange}, DidPresenter={args.DidPresenterChange}");
            UpdateXamlSourcePosition();
            UpdateTitleBarDragRects();
        }
    }

    /// <summary>输出诊断日志到调试输出窗口。</summary>
    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[PlayerWindow] {message}");
    }
}
