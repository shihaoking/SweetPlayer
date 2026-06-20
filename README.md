# SweetPlayer

A Windows video player inspired by Infuse, featuring intelligent media library management, metadata scraping, and HDR/Dolby Vision support.

## Features

- **Media Source Management** — Add local folders and WebDAV sources with recursive multi-level scanning
- **Metadata Scraping** — Automatic movie/TV info and poster retrieval via Douban (primary) and TMDB (fallback) APIs
- **Poster Wall UI** — Infuse-style dark theme with poster grid, HDR/Dolby badges
- **HDR/Dolby Detection** — Auto-detect HDR10, Dolby Vision, Dolby Atmos; auto-enable Windows HDR
- **Video Playback** — LibMPV-powered playback supporting all major formats with hardware acceleration
- **Subtitle Management** — Auto-load local subtitles, online search via Shooter API

## Configuration

### TMDB API Key (Optional)

SweetPlayer uses Douban as the primary metadata source. If you want to enable TMDB as a fallback for movies not found on Douban:

1. Get a free API key from [TMDB](https://www.themoviedb.org/settings/api)
2. Add it to `appsettings.json`:

```json
{
  "Tmdb": {
    "ApiKey": "your_api_key_here"
  }
}
```

Without a TMDB API key, the app will only use Douban for metadata scraping.

## Tech Stack

- **Framework**: WinUI 3 + .NET 8
- **Playback Engine**: LibMPV
- **Architecture**: MVVM (CommunityToolkit.Mvvm)
- **Database**: SQLite + Entity Framework Core
- **Metadata**: Douban (primary) + TMDB (fallback)
- **Subtitles**: Shooter (射手网) API

## Getting Started

> Coming soon — project is in planning phase.

## License

MIT
