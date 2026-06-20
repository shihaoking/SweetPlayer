using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.Core.Models;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

public sealed partial class FileSourceBrowserPage : Page
{
    public FileSourceBrowserViewModel ViewModel { get; }

    public FileSourceBrowserPage()
    {
        InitializeComponent();
        ViewModel = (FileSourceBrowserViewModel)App.Services.GetService(typeof(FileSourceBrowserViewModel))!;
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is MediaSource source)
        {
            await ViewModel.InitializeAsync(source);
        }
    }

    private void Entry_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is BrowseEntry entry)
        {
            if (entry.IsDirectory)
            {
                ViewModel.NavigateToSubdirectoryCommand.Execute(entry);
            }
            // 文件点击暂不处理（未来可以用于播放）
        }
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is BreadcrumbItem item)
        {
            ViewModel.NavigateToBreadcrumbCommand.Execute(item);
        }
    }
}
