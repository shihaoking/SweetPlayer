using Microsoft.EntityFrameworkCore;
using SweetPlayer.Core.Models;

namespace SweetPlayer.Core.Data;

public class SweetPlayerDbContext : DbContext
{
    public SweetPlayerDbContext(DbContextOptions<SweetPlayerDbContext> options)
        : base(options)
    {
    }

    public DbSet<MediaSource> MediaSources => Set<MediaSource>();

    public DbSet<VideoFile> VideoFiles => Set<VideoFile>();

    public DbSet<MovieMetadata> MovieMetadata => Set<MovieMetadata>();

    public DbSet<PlaybackProgress> PlaybackProgress => Set<PlaybackProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MediaSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Path).IsRequired();
            entity.HasIndex(e => e.Path);
            entity.HasMany(e => e.VideoFiles)
                .WithOne(v => v.MediaSource)
                .HasForeignKey(v => v.MediaSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VideoFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired();
            entity.Property(e => e.FullPath).IsRequired();
            entity.HasIndex(e => e.FullPath).IsUnique();
            entity.HasIndex(e => e.MediaSourceId);
            entity.HasIndex(e => e.MovieMetadataId);
            entity.HasOne(v => v.MovieMetadata)
                .WithMany(m => m.VideoFiles)
                .HasForeignKey(v => v.MovieMetadataId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MovieMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChineseTitle).IsRequired();
            entity.HasIndex(e => e.DoubanId);
            entity.HasIndex(e => new { e.ChineseTitle, e.Year });
        });

        modelBuilder.Entity<PlaybackProgress>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VideoFileId).IsUnique();
            entity.HasOne(p => p.VideoFile)
                .WithMany()
                .HasForeignKey(p => p.VideoFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
