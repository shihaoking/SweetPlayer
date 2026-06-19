# SweetPlayer

A Windows video player inspired by Infuse, featuring intelligent media library management, metadata scraping, and HDR/Dolby Vision support.

## Features

- **Media Source Management** — Add local folders and WebDAV sources with recursive multi-level scanning
- **Metadata Scraping** — Automatic movie/TV info and poster retrieval via TMDb API (Chinese preferred)
- **Poster Wall UI** — Infuse-style dark theme with poster grid, HDR/Dolby badges
- **HDR/Dolby Detection** — Auto-detect HDR10, Dolby Vision, Dolby Atmos; auto-enable Windows HDR
- **Video Playback** — LibMPV-powered playback supporting all major formats with hardware acceleration
- **Subtitle Management** — Auto-load local subtitles, online search via Shooter API

## Tech Stack

- **Framework**: WPF + .NET 8
- **Playback Engine**: LibMPV
- **Architecture**: MVVM (CommunityToolkit.Mvvm)
- **Database**: SQLite + Entity Framework Core
- **Metadata**: TMDb API
- **Subtitles**: Shooter (射手网) API

## Getting Started

> Coming soon — project is in planning phase.

## License

MIT
