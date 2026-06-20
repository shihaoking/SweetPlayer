using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

public sealed partial class MovieDetailPage : Page
{
    public MovieDetailViewModel ViewModel { get; }

    public MovieDetailPage()
    {
        InitializeComponent();
        ViewModel = (MovieDetailViewModel)App.Services.GetService(typeof(MovieDetailViewModel))!;
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
}
