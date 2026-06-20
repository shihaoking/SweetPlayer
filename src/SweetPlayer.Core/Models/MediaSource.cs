namespace SweetPlayer.Core.Models;

public enum MediaSourceType
{
    Local,
    WebDAV
}

public enum ScanStatus
{
    Idle,
    Scanning,
    Completed,
    Error
}

public class MediaSource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public MediaSourceType Type { get; set; }

    public string Path { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastScanAt { get; set; }

    public int FileCount { get; set; }

    public ScanStatus ScanStatus { get; set; }

    public ICollection<VideoFile> VideoFiles { get; set; } = new List<VideoFile>();
}
