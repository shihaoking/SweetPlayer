namespace SweetPlayer.Core.Models;

public class PlaybackProgress
{
    public int Id { get; set; }

    public int VideoFileId { get; set; }

    public VideoFile VideoFile { get; set; } = null!;

    public TimeSpan Position { get; set; }

    public TimeSpan Duration { get; set; }

    public DateTime LastPlayedAt { get; set; }

    public bool IsCompleted { get; set; }
}
