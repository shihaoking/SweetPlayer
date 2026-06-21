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

📖 **[完整快速开始指南](docs/QUICK_START.md)**

### Prerequisites

1. **Windows 10 version 1809 (build 17763) or later**
2. **.NET 8.0 SDK**
3. **LibMPV dynamic library** — Required for video playback

   The app will fall back to mock playback mode if `libmpv-2.dll` is not found. To enable real video playback:

   📖 **[See LibMPV Setup Guide](docs/LIBMPV_SETUP.md)** for detailed instructions

   Quick setup:

   ```powershell
   # Run automated deployment script
   .\scripts\setup-libmpv.ps1
   ```

   Or manually:
   - Download libmpv from [shinchiro/mpv-winbuild-cmake releases](https://github.com/shinchiro/mpv-winbuild-cmake/releases)
   - Extract `libmpv-2.dll` and place it in `src/SweetPlayer/Native/{architecture}/`

### Build and Run

```bash
# Clone the repository
git clone https://github.com/yourusername/SweetPlayer.git
cd SweetPlayer

# Deploy libmpv (automated)
.\scripts\setup-libmpv.ps1

# Build the solution
dotnet build

# Run the application
dotnet run --project src/SweetPlayer
```

See [Quick Start Guide](docs/QUICK_START.md) for detailed instructions and troubleshooting.

## License

MIT
