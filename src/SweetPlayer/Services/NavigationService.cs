using Microsoft.UI.Xaml.Controls;

namespace SweetPlayer.Services;

/// <summary>
/// 基于 WinUI Frame 的简单页面导航服务。
/// </summary>
public interface INavigationService
{
    Frame? Frame { get; set; }

    bool CanGoBack { get; }

    bool NavigateTo(Type pageType, object? parameter = null);

    bool GoBack();
}

public sealed class NavigationService : INavigationService
{
    public Frame? Frame { get; set; }

    public bool CanGoBack => Frame?.CanGoBack ?? false;

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        if (Frame is null)
        {
            return false;
        }

        if (Frame.Content?.GetType() == pageType)
        {
            return false;
        }

        return Frame.Navigate(pageType, parameter);
    }

    public bool GoBack()
    {
        if (Frame is { CanGoBack: true })
        {
            Frame.GoBack();
            return true;
        }

        return false;
    }
}
