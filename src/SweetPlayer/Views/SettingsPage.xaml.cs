using Microsoft.UI.Xaml.Controls;
using SweetPlayer.ViewModels;

namespace SweetPlayer.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = (SettingsViewModel)App.Services.GetService(typeof(SettingsViewModel))!;
        DataContext = ViewModel;
    }
}
