using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Data;
using SweetPlayer.Core.Models;
using SweetPlayer.Services;
using SweetPlayer.Services.Playback;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 单季按钮项（季选择器使用）。
/// </summary>
public sealed partial class SeasonItem : ObservableObject
{
    public int SeasonNumber { get; init; }

    public string DisplayText => $"第{SeasonNumber}季";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 单集列表项。
/// </summary>
public sealed class EpisodeItem
{
    public VideoFile File { get; init; } = null!;

    public int Season { get; init; }

    public int Episode { get; init; }

    public string DisplayNumber => $"E{Episode:00}";

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(File.EpisodeTitle) ? $"第 {Episode} 集" : File.EpisodeTitle!;

    public string ThumbnailHint => $"S{Season:00}E{Episode:00}";

    public bool HasHdr => File.HasHDR;

    public bool HasDolbyVision => File.HasDolbyVision;

    public bool HasDolbyAtmos => File.HasDolbyAtmos;
}

/// <summary>
/// 剧集详情页 ViewModel。
/// </summary>
public sealed partial class SeriesDetailViewModel : ViewModelBase
{
    private readonly IDbContextFactory<SweetPlayerDbContext> _dbFactory;
    private readonly INavigationService _navigation;
    private readonly IPlaybackControlService _playback;

    private readonly Dictionary<int, List<EpisodeItem>> _episodesBySeason = new();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _backdropPath;

    [ObservableProperty]
    private string? _posterPath;

    [ObservableProperty]
    private string? _yearText;

    [ObservableProperty]
    private string? _genres;

    [ObservableProperty]
    private string? _ratingText;

    [ObservableProperty]
    private string? _synopsis;

    [ObservableProperty]
    private SeasonItem? _selectedSeason;

    public SeriesDetailViewModel(
        IDbContextFactory<SweetPlayerDbContext> dbFactory,
        INavigationService navigation,
        IPlaybackControlService playback)
    {
        _dbFactory = dbFactory;
        _navigation = navigation;
        _playback = playback;
        Seasons = new ObservableCollection<SeasonItem>();
        Episodes = new ObservableCollection<EpisodeItem>();
    }

    /// <summary>本地源中实际存在的季列表。</summary>
    public ObservableCollection<SeasonItem> Seasons { get; }

    /// <summary>当前选中季的所有集数。</summary>
    public ObservableCollection<EpisodeItem> Episodes { get; }

    public async Task LoadAsync(int metadataId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(true);
        var meta = await db.MovieMetadata
            .Include(m => m.VideoFiles)
            .FirstOrDefaultAsync(m => m.Id == metadataId, cancellationToken)
            .ConfigureAwait(true);
        if (meta is null) return;

        Title = meta.ChineseTitle;
        BackdropPath = meta.BackdropLocalPath ?? meta.PosterLocalPath;
        PosterPath = meta.PosterLocalPath;
        YearText = meta.Year?.ToString();
        Genres = meta.Genres;
        RatingText = meta.DoubanRating.HasValue ? $"豆瓣 {meta.DoubanRating:F1}" : null;
        Synopsis = meta.Synopsis;

        // 按季分组
        _episodesBySeason.Clear();
        var grouped = meta.VideoFiles
            .Where(v => v.MediaType == MediaType.TVEpisode && v.Season.HasValue && v.Episode.HasValue)
            .GroupBy(v => v.Season!.Value)
            .OrderBy(g => g.Key);

        Seasons.Clear();
        foreach (var grp in grouped)
        {
            var seasonNumber = grp.Key;
            var episodes = grp.OrderBy(v => v.Episode!.Value)
                .Select(v => new EpisodeItem
                {
                    File = v,
                    Season = seasonNumber,
                    Episode = v.Episode!.Value,
                })
                .ToList();
            _episodesBySeason[seasonNumber] = episodes;
            Seasons.Add(new SeasonItem { SeasonNumber = seasonNumber });
        }

        // 默认选中第一季
        var first = Seasons.FirstOrDefault();
        if (first is not null)
        {
            SelectSeason(first);
        }
    }

    partial void OnSelectedSeasonChanged(SeasonItem? value)
    {
        // 同步选中态
        foreach (var s in Seasons)
        {
            s.IsSelected = ReferenceEquals(s, value);
        }

        Episodes.Clear();
        if (value is null) return;
        if (_episodesBySeason.TryGetValue(value.SeasonNumber, out var list))
        {
            foreach (var ep in list)
            {
                Episodes.Add(ep);
            }
        }
    }

    [RelayCommand]
    private void SelectSeason(SeasonItem? season)
    {
        if (season is null) return;
        SelectedSeason = season;
    }

    [RelayCommand]
    private async Task PlayEpisodeAsync(EpisodeItem? episode)
    {
        if (episode?.File is null) return;
        await _playback.PlayVideoAsync(episode.File);
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigation.GoBack();
    }
}
