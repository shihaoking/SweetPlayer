using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = (HomeViewModel)App.Services.GetService(typeof(HomeViewModel))!;
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadLibraryAsync();
    }

    private void LibraryGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MediaCardItem item)
        {
            ViewModel.NavigateToDetailCommand.Execute(item);
        }
    }
}
