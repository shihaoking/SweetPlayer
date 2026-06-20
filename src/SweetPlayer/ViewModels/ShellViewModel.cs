using CommunityToolkit.Mvvm.ComponentModel;

namespace SweetPlayer.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "SweetPlayer";
}
