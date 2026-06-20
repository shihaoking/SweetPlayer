using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;
using SweetPlayer.Helpers;
using SweetPlayer.Services;
using SweetPlayer.Services.Scraping;
using SweetPlayer.Views;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 主屏幕（媒体库）ViewModel：从数据库加载已刮削的影视条目，
/// 暴露海报墙数据源、搜索筛选、空库状态以及详情页导航命令。
/// </summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    /// <summary>每页增量加载的海报卡片数量。</summary>
    private const int PageSize = 50;

    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly INavigationService _navigation;
    private readonly IScrapingQueueService _scrapingQueue;
    private readonly List<MediaCardItem> _allItems = new();
    private List<MediaCardItem> _filteredItems = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isScraping;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public HomeViewModel(
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        INavigationService navigation,
        IScrapingQueueService scrapingQueue)
    {
        _dbFactory = dbFactory;
        _navigation = navigation;
        _scrapingQueue = scrapingQueue;
        MediaItems = new IncrementalLoadingCollection<MediaCardItem>(GetPage, PageSize);
    }

    /// <summary>
    /// 当前展示的海报卡片集合（搜索筛选后的视图）。
    /// 实现 <see cref="Microsoft.UI.Xaml.Data.ISupportIncrementalLoading"/>，
    /// 滚动接近末尾时由 <see cref="Microsoft.UI.Xaml.Controls.GridView"/> 自动加载下一页。
    /// </summary>
    public IncrementalLoadingCollection<MediaCardItem> MediaItems { get; }

    /// <summary>
    /// 分页数据提供器：从筛选后的快照中切片。
    /// </summary>
    private IReadOnlyList<MediaCardItem> GetPage(int skip, int take)
    {
        if (skip >= _filteredItems.Count) return Array.Empty<MediaCardItem>();
        var actualTake = Math.Min(take, _filteredItems.Count - skip);
        return _filteredItems.GetRange(skip, actualTake);
    }

    /// <summary>媒体库为空时显示引导视图。</summary>
    public bool IsLibraryEmpty => !IsLoading && _allItems.Count == 0;

    /// <summary>搜索后无结果且原始库非空时显示。</summary>
    public bool HasNoSearchResult => !IsLoading && _allItems.Count > 0 && MediaItems.Count == 0;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(HasNoSearchResult));
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// 从数据库加载所有已刮削的影视条目（按中文标题排序）。
    /// </summary>
    public async Task LoadLibraryAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(true);

            // 仅展示拥有本地视频文件的元数据（避免空条目）
            var metadataList = await db.MovieMetadata
                .Include(m => m.VideoFiles)
                .Where(m => m.VideoFiles.Any())
                .OrderBy(m => m.ChineseTitle)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(true);

            _allItems.Clear();
            foreach (var meta in metadataList)
            {
                _allItems.Add(BuildCardItem(meta));
            }

            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLibraryEmpty));
            OnPropertyChanged(nameof(HasNoSearchResult));
        }
    }

    private static MediaCardItem BuildCardItem(MovieMetadata meta)
    {
        // 电视剧统计本地实际识别到的季数（不依赖 TotalSeasons 字段，更贴近本地源）
        var localSeasonCount = 0;
        if (meta.ContentType == MediaContentType.TVSeries)
        {
            localSeasonCount = meta.VideoFiles
                .Where(v => v.Season.HasValue)
                .Select(v => v.Season!.Value)
                .Distinct()
                .Count();
        }

        return new MediaCardItem
        {
            MetadataId = meta.Id,
            Title = meta.ChineseTitle,
            PosterPath = meta.PosterLocalPath,
            BackdropPath = meta.BackdropLocalPath,
            MediaType = meta.ContentType,
            SeasonCount = localSeasonCount,
            HasHdr = meta.VideoFiles.Any(v => v.HasHDR),
            HasDolbyVision = meta.VideoFiles.Any(v => v.HasDolbyVision),
            HasDolbyAtmos = meta.VideoFiles.Any(v => v.HasDolbyAtmos),
            DoubanRating = meta.DoubanRating,
            Year = meta.Year,
        };
    }

    private void ApplyFilter()
    {
        var keyword = SearchQuery?.Trim();
        IEnumerable<MediaCardItem> filtered = _allItems;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = _allItems.Where(i =>
                i.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        // 搜索过滤后重新生成分页快照，并触发增量集合首页加载
        _filteredItems = filtered.ToList();
        MediaItems.Reset(_filteredItems.Count);

        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(HasNoSearchResult));
    }

    /// <summary>
    /// 点击海报卡片：根据类型导航到对应详情页（电影/剧集）。
    /// </summary>
    [RelayCommand]
    private void NavigateToDetail(MediaCardItem? item)
    {
        if (item is null) return;

        var pageType = item.MediaType == MediaContentType.TVSeries
            ? typeof(SeriesDetailPage)
            : typeof(MovieDetailPage);

        _navigation.NavigateTo(pageType, item.MetadataId);
    }

    /// <summary>
    /// 空库引导按钮：跳转到 Sources TAB。
    /// </summary>
    [RelayCommand]
    private void NavigateToSources()
    {
        _navigation.NavigateTo(typeof(SourcesPage));
    }

    /// <summary>
    /// 手动触发刮削所有未刮削的视频文件。
    /// </summary>
    [RelayCommand]
    private async Task StartScrapingAsync()
    {
        if (IsScraping) return;

        IsScraping = true;
        try
        {
            await _scrapingQueue.LoadUnscrapedFilesAsync().ConfigureAwait(true);
        }
        finally
        {
            IsScraping = false;
        }
    }
}
