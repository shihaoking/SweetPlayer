using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.Services.Playback;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

/// <summary>
/// 播放器桥接页：接收导航参数后打开独立 PlayerWindow，
/// 窗口关闭后自动导航返回。页面本身显示为全黑。
/// </summary>
public sealed partial class PlayerPage : Page
{
    private readonly IPlaybackControlService _playback;
    private readonly INavigationService _navigation;
    private PlayerWindow? _playerWindow;
    private PlayerViewModel? _viewModel;

    public PlayerPage()
    {
        var sp = App.Services;
        _playback = sp.GetRequiredService<IPlaybackControlService>();
        _navigation = sp.GetRequiredService<INavigationService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is VideoFile vf)
        {
            // 从 DI 获取 PlayerWindow（Transient，每次播放创建新实例）
            _playerWindow = App.Services.GetRequiredService<PlayerWindow>();
            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();

            // 初始化 ViewModel
            _viewModel.Initialize(vf);
            _viewModel.LoadChaptersFromMpv();

            // 显示独立播放窗口
            await _playerWindow.ShowAsync();

            // 设置 UI 覆盖层
            var overlay = new PlayerWindowOverlay(_playerWindow, _viewModel);
            _playerWindow.SetOverlayContent(overlay);

            // 监听窗口关闭
            _playerWindow.Closed += OnPlayerWindowClosed;

            // 开始播放（等待完成，确保文件加载成功）
            await _playback.PlayVideoAsync(vf);
        }
    }

    private void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        if (_playerWindow is not null)
        {
            _playerWindow.Closed -= OnPlayerWindowClosed;
        }

        _playback.Stop();

        DispatcherQueue.TryEnqueue(() =>
        {
            // 重新激活主窗口（AppWindow 关闭后不会自动归还焦点）
            var mainWindow = App.MainWindow ?? MainWindowAccessor.Current;
            mainWindow?.Activate();

            if (_navigation.CanGoBack)
            {
                _navigation.GoBack();
            }
            else
            {
                _navigation.NavigateTo(typeof(HomePage));
            }
        });
    }
}
