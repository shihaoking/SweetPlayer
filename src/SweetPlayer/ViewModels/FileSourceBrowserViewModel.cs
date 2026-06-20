using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.Services.Browse;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 文件源浏览页 ViewModel：展示文件源目录内容，支持导航和面包屑
/// </summary>
public sealed partial class FileSourceBrowserViewModel : ViewModelBase
{
    private readonly IDirectoryBrowseService _browseService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<FileSourceBrowserViewModel> _logger;

    private MediaSource? _currentSource;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public FileSourceBrowserViewModel(
        IDirectoryBrowseService browseService,
        INavigationService navigationService,
        ILogger<FileSourceBrowserViewModel> logger)
    {
        _browseService = browseService;
        _navigationService = navigationService;
        _logger = logger;

        Entries = new ObservableCollection<BrowseEntry>();
        BreadcrumbItems = new ObservableCollection<BreadcrumbItem>();
    }

    /// <summary>
    /// 当前目录的内容列表
    /// </summary>
    public ObservableCollection<BrowseEntry> Entries { get; }

    /// <summary>
    /// 面包屑导航项
    /// </summary>
    public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; }

    /// <summary>
    /// 初始化浏览器，加载文件源根目录
    /// </summary>
    public async Task InitializeAsync(MediaSource source, CancellationToken cancellationToken = default)
    {
        _currentSource = source ?? throw new ArgumentNullException(nameof(source));
        CurrentPath = string.Empty;

        UpdateBreadcrumbs();
        await LoadDirectoryAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// 加载当前路径的目录内容
    /// </summary>
    public async Task LoadDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSource is null)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var entries = await _browseService.ListDirectoryAsync(_currentSource, CurrentPath, cancellationToken).ConfigureAwait(true);

            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载目录失败：{Path}", CurrentPath);
            ErrorMessage = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 进入子目录
    /// </summary>
    [RelayCommand]
    private async Task NavigateToSubdirectoryAsync(BrowseEntry? entry)
    {
        if (entry is null || !entry.IsDirectory)
        {
            return;
        }

        CurrentPath = entry.RelativePath;
        UpdateBreadcrumbs();
        await LoadDirectoryAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// 点击面包屑导航到指定层级
    /// </summary>
    [RelayCommand]
    private async Task NavigateToBreadcrumbAsync(BreadcrumbItem? item)
    {
        if (item is null)
        {
            return;
        }

        CurrentPath = item.Path;
        UpdateBreadcrumbs();
        await LoadDirectoryAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// 返回文件源列表页
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        _navigationService.GoBack();
    }

    /// <summary>
    /// 重试加载
    /// </summary>
    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadDirectoryAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// 更新面包屑导航
    /// </summary>
    private void UpdateBreadcrumbs()
    {
        BreadcrumbItems.Clear();

        // 根目录
        BreadcrumbItems.Add(new BreadcrumbItem
        {
            Label = _currentSource?.Name ?? "根目录",
            Path = string.Empty
        });

        // 子目录
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            var segments = CurrentPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var accumulatedPath = string.Empty;

            foreach (var segment in segments)
            {
                accumulatedPath = string.IsNullOrEmpty(accumulatedPath)
                    ? segment
                    : Path.Combine(accumulatedPath, segment);

                BreadcrumbItems.Add(new BreadcrumbItem
                {
                    Label = segment,
                    Path = accumulatedPath
                });
            }
        }
    }
}

/// <summary>
/// 面包屑导航项
/// </summary>
public class BreadcrumbItem
{
    public string Label { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
