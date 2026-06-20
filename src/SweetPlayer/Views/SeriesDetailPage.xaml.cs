using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

public sealed partial class SeriesDetailPage : Page
{
    public SeriesDetailViewModel ViewModel { get; }

    public SeriesDetailPage()
    {
        InitializeComponent();
        ViewModel = (SeriesDetailViewModel)App.Services.GetService(typeof(SeriesDetailViewModel))!;
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int metadataId)
        {
            await ViewModel.LoadAsync(metadataId);
        }
    }

    private void EpisodeCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is EpisodeItem item)
        {
            ViewModel.PlayEpisodeCommand.Execute(item);
        }
    }
}
