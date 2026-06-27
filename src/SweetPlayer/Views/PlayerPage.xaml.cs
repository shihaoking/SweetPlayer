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
    private readonly INavigationService _navigation;
    private IPlaybackControlService? _playback;
    private PlayerWindow? _playerWindow;
    private PlayerViewModel? _viewModel;

    public PlayerPage()
    {
        _navigation = App.Services.GetRequiredService<INavigationService>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is VideoFile vf)
        {
            var sp = App.Services;

            // 创建单个 mpv 实例，由 PlayerWindow 和 PlaybackControlService 共享
            var mpv = sp.GetRequiredService<IMpvPlayerService>();
            _playerWindow = ActivatorUtilities.CreateInstance<PlayerWindow>(sp, mpv);
            _playback = ActivatorUtilities.CreateInstance<PlaybackControlService>(sp, mpv);
            _viewModel = ActivatorUtilities.CreateInstance<PlayerViewModel>(sp, (IPlaybackControlService)_playback);

            // 初始化 ViewModel
            _viewModel.Initialize(vf);
            _viewModel.LoadChaptersFromMpv();

            // 显示独立播放窗口
            await _playerWindow.ShowAsync();

            // 设置 UI 覆盖层
            var overlay = new PlayerWindowOverlay(_playerWindow, _viewModel, _playback);
            _playerWindow.SetOverlayContent(overlay);

            // 监听窗口关闭
            _playerWindow.Closed += OnPlayerWindowClosed;

            // 开始播放（等待完成，确保文件加载成功）
            await _playback.PlayVideoAsync(vf);
        }
    }

    private async void OnPlayerWindowClosed(object? sender, EventArgs e)
    {
        if (_playerWindow is not null)
        {
            _playerWindow.Closed -= OnPlayerWindowClosed;
        }

        // 保存播放进度并清理 HDR 状态（mpv 实例已被 PlayerWindow.DisposeAsync 销毁）
        if (_playback is not null)
        {
            await _playback.StopAsync();
        }

        DispatcherQueue.TryEnqueue(() =>
        {
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
