namespace SweetPlayer.Core.Models;

public enum MediaType
{
    Movie,
    TVEpisode,
    Unknown
}

public enum HdrFormat
{
    HDR10,
    HDR10Plus,
    HLG,
    DolbyVision
}

public class VideoFile
{
    public int Id { get; set; }

    public int MediaSourceId { get; set; }

    public MediaSource MediaSource { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string? Container { get; set; }

    public MediaType MediaType { get; set; }

    public int? MovieMetadataId { get; set; }

    public MovieMetadata? MovieMetadata { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public string? EpisodeTitle { get; set; }

    public string? EditionTag { get; set; }

    public bool HasHDR { get; set; }

    public HdrFormat? HdrFormat { get; set; }

    public bool HasDolbyVision { get; set; }

    public bool HasDolbyAtmos { get; set; }

    public DateTime DiscoveredAt { get; set; }
}
