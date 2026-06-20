using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SweetPlayer.Views;

public sealed partial class SourcesPage : Page
{
    public SourcesViewModel ViewModel { get; }
    private readonly INavigationService _navigationService;

    public SourcesPage()
    {
        InitializeComponent();
        ViewModel = (SourcesViewModel)App.Services.GetService(typeof(SourcesViewModel))!;
        _navigationService = (INavigationService)App.Services.GetService(typeof(INavigationService))!;
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadSourcesAsync();
    }

    private async void AddSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "添加文件源",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None,
            XamlRoot = this.XamlRoot,
        };

        var localButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 14, 16, 14),
            Content = BuildOptionContent("\uE8B7", "本地文件夹", "扫描指定本地路径下的视频文件"),
        };
        localButton.Click += async (_, __) =>
        {
            dialog.Hide();
            await ShowLocalSourceDialogAsync();
        };

        var webdavButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 14, 16, 14),
            Content = BuildOptionContent("\uE968", "WebDAV", "通过 WebDAV 协议接入云端媒体库"),
        };
        webdavButton.Click += async (_, __) =>
        {
            dialog.Hide();
            await ShowWebDavSourceDialogAsync();
        };

        dialog.Content = new StackPanel
        {
            Spacing = 8,
            Width = 360,
            Children =
            {
                localButton,
                webdavButton,
            },
        };

        await dialog.ShowAsync();
    }

    private static StackPanel BuildOptionContent(string glyph, string title, string subtitle)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 22 },
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock { Text = subtitle, FontSize = 11, Opacity = 0.6 },
                    },
                },
            },
        };
    }

    private async Task ShowLocalSourceDialogAsync()
    {
        // 使用 FolderPicker 选择本地路径（WinUI 3 桌面应用需绑定主窗 HWND）
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var mainWindow = App.MainWindow ?? throw new InvalidOperationException("MainWindow 未初始化");
        var hwnd = WindowNative.GetWindowHandle(mainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        var nameBox = new TextBox { PlaceholderText = "源名称", Text = folder.Name };
        var dialog = new ContentDialog
        {
            Title = "命名本地源",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = folder.Path, Opacity = 0.65, FontSize = 12 },
                    nameBox,
                },
            },
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        ViewModel.AddLocalSourceCommand.Execute(new LocalSourceInput(folder.Path, nameBox.Text));
    }

    private async Task ShowWebDavSourceDialogAsync()
    {
        var nameBox = new TextBox { PlaceholderText = "源名称（如：家庭 NAS）" };
        var urlBox = new TextBox
        {
            PlaceholderText = "http://192.168.1.100/webdav 或 https://nas.example.com/dav",
            Header = "WebDAV 地址（支持自动检测常见路径）",
        };
        var userBox = new TextBox { PlaceholderText = "用户名（可选）" };
        var pwdBox = new PasswordBox { PlaceholderText = "密码（可选）" };

        var dialog = new ContentDialog
        {
            Title = "添加 WebDAV 源",
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Width = 380,
                Children = { nameBox, urlBox, userBox, pwdBox },
            },
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        ViewModel.AddWebDavSourceCommand.Execute(
            new WebDavSourceInput(urlBox.Text, userBox.Text, pwdBox.Password, nameBox.Text));
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is MediaSource source)
        {
            ViewModel.ScanSourceCommand.Execute(source);
        }
    }

    private void ScanButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // 阻止事件冒泡到父级 Grid 的 Tapped 事件
        e.Handled = true;
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not MediaSource source) return;

        var confirm = new ContentDialog
        {
            Title = "删除文件源",
            Content = $"确定删除「{source.Name}」吗？该源下的视频记录将一并清除。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        var result = await confirm.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.RemoveSourceCommand.Execute(source);
        }
    }

    private void RemoveButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // 阻止事件冒泡到父级 Grid 的 Tapped 事件
        e.Handled = true;
    }

    private void SourceCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is MediaSource source)
        {
            _navigationService.NavigateTo(typeof(FileSourceBrowserPage), source);
        }
    }
}
