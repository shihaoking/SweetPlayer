namespace SweetPlayer.Core.Models;

public enum MediaContentType
{
    Movie,
    TVSeries
}

public enum MatchConfidence
{
    High,
    Medium,
    Low,
    Manual
}

public enum MetadataSource
{
    Douban = 0,
    TMDB = 1
}

public class MovieMetadata
{
    public int Id { get; set; }

    public string ChineseTitle { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public int? Year { get; set; }

    public double? DoubanRating { get; set; }

    public string? Genres { get; set; }

    public string? Director { get; set; }

    public string? Cast { get; set; }

    public string? Synopsis { get; set; }

    public string? PosterLocalPath { get; set; }

    public string? BackdropLocalPath { get; set; }

    public string? DoubanId { get; set; }

    public string? TmdbId { get; set; }

    public MetadataSource DataSource { get; set; } = MetadataSource.Douban;

    public MediaContentType ContentType { get; set; }

    public int? TotalSeasons { get; set; }

    public string? SeasonsEpisodeCount { get; set; }

    public DateTime ScrapedAt { get; set; }

    public MatchConfidence Confidence { get; set; }

    public ICollection<VideoFile> VideoFiles { get; set; } = new List<VideoFile>();
}
