using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.Views;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 电影详情页 ViewModel：根据 MovieMetadata.Id 加载详情，
/// 暴露海报、背景图、基本信息、剧情简介与版本列表，并提供播放命令。
/// </summary>
public sealed partial class MovieDetailViewModel : ViewModelBase
{
    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _originalTitle;

    [ObservableProperty]
    private string? _posterPath;

    [ObservableProperty]
    private string? _backdropPath;

    [ObservableProperty]
    private string? _yearText;

    [ObservableProperty]
    private string? _genres;

    [ObservableProperty]
    private string? _director;

    [ObservableProperty]
    private string? _cast;

    [ObservableProperty]
    private string? _ratingText;

    [ObservableProperty]
    private string? _synopsis;

    [ObservableProperty]
    private VideoFile? _selectedVersion;

    public MovieDetailViewModel(
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        INavigationService navigation)
    {
        _dbFactory = dbFactory;
        _navigation = navigation;
        Versions = new ObservableCollection<VideoFile>();
    }

    /// <summary>同一部电影的多版本视频文件列表（如 Director's Cut、{edition-xxx}）。</summary>
    public ObservableCollection<VideoFile> Versions { get; }

    /// <summary>是否包含多个版本（用于 UI 显示版本选择列表）。</summary>
    public bool HasMultipleVersions => Versions.Count > 1;

    /// <summary>是否有可播放视频。</summary>
    public bool HasPlayable => Versions.Count > 0;

    public async Task LoadAsync(int metadataId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(true);
        var meta = await db.MovieMetadata
            .Include(m => m.VideoFiles)
            .FirstOrDefaultAsync(m => m.Id == metadataId, cancellationToken)
            .ConfigureAwait(true);
        if (meta is null) return;

        Title = meta.ChineseTitle;
        OriginalTitle = meta.OriginalTitle;
        PosterPath = meta.PosterLocalPath;
        BackdropPath = meta.BackdropLocalPath ?? meta.PosterLocalPath;
        YearText = meta.Year?.ToString();
        Genres = meta.Genres;
        Director = string.IsNullOrWhiteSpace(meta.Director) ? null : $"导演：{meta.Director}";
        Cast = string.IsNullOrWhiteSpace(meta.Cast) ? null : $"主演：{meta.Cast}";
        RatingText = meta.DoubanRating.HasValue ? $"豆瓣 {meta.DoubanRating:F1}" : null;
        Synopsis = meta.Synopsis;

        Versions.Clear();
        foreach (var file in meta.VideoFiles
                     .Where(v => v.MediaType != MediaType.TVEpisode)
                     .OrderBy(v => v.EditionTag ?? string.Empty)
                     .ThenBy(v => v.FileName))
        {
            Versions.Add(file);
        }

        SelectedVersion = Versions.FirstOrDefault();
        OnPropertyChanged(nameof(HasMultipleVersions));
        OnPropertyChanged(nameof(HasPlayable));
    }

    [RelayCommand]
    private void Play()
    {
        var target = SelectedVersion ?? Versions.FirstOrDefault();
        if (target is null) return;

        // 仅导航到播放页面，PlayerPage.OnNavigatedTo 负责创建 mpv 实例并开始播放
        _navigation.NavigateTo(typeof(PlayerPage), target);
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigation.GoBack();
    }
}
