using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SweetPlayer.Services;
using SweetPlayer.Views;

namespace SweetPlayer;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;

    public MainWindow()
    {
        InitializeComponent();

        Title = "SweetPlayer";

        _navigationService = App.Services.GetService(typeof(INavigationService)) as INavigationService
            ?? new NavigationService();
        _navigationService.Frame = ContentFrame;

        // 监听导航事件，播放页面时隐藏侧边栏
        ContentFrame.Navigated += OnFrameNavigated;

        // 默认选中首页
        RootNavigationView.SelectedItem = RootNavigationView.MenuItems[0];
    }

    private void OnFrameNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // 播放页面隐藏侧边栏，其他页面显示
        var isPlayerPage = e.Content is PlayerPage;
        RootNavigationView.IsPaneVisible = !isPlayerPage;
        RootNavigationView.IsSettingsVisible = !isPlayerPage;
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _navigationService.NavigateTo(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "home":
                    _navigationService.NavigateTo(typeof(HomePage));
                    break;
                case "sources":
                    _navigationService.NavigateTo(typeof(SourcesPage));
                    break;
            }
        }
    }
}
