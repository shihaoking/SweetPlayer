## Why

Windows 平台缺乏一款类似 macOS/iOS 上 Infuse 的优雅视频播放器——能够自动管理本地和网络视频库、刮削影片元数据、以海报墙形式展示媒体库，并智能处理 HDR/杜比视界/杜比音效以及字幕加载。SweetPlayer 旨在填补这一空白，为 Windows 用户提供一站式高品质影音体验。

## What Changes

- 新建完整的 Windows 桌面播放器应用
- 支持主流视频格式播放（MKV、MP4、AVI、MOV、HEVC、AV1 等）
- 支持添加本地文件夹和 WebDAV 远程文件源
- 多层级文件夹递归扫描，自动发现视频文件
- 根据文件名在线搜索影片信息和海报（元数据刮削，数据源：豆瓣）
- 主屏幕以海报墙形式展示媒体库（中文影片名 + 海报）
- 自动识别 HDR、杜比视界、杜比音效视频文件并标记
- 播放 HDR 视频时自动启用 Windows 系统 HDR 开关
- 自动加载同目录下同名字幕文件（ASS/SRT 等格式）
- 播放界面支持手动添加本地字幕和在线字幕（射手网）
- 播放界面设计参考 Infuse 风格（简洁、沉浸式）

## Capabilities

### New Capabilities

- `media-source-management`: 本地文件夹和 WebDAV 文件源的添加、管理、多层级递归扫描
- `metadata-scraping`: 基于 Infuse 命名规范的文件名解析引擎，豆瓣元数据搜索（标题、年份、评分、简介）和海报图片获取
- `media-library-ui`: 三 TAB 导航结构（主屏幕海报墙、文件源管理、设置/关于）、剧集展示、Infuse 风格 UI
- `video-playback`: 主流视频格式播放、播放控制、进度记忆
- `hdr-dolby-detection`: 自动识别 HDR/杜比视界/杜比音效，播放时自动启用系统 HDR
- `subtitle-management`: 自动加载同名字幕、手动添加本地字幕、射手网在线字幕搜索与加载

### Modified Capabilities

（无，本项目为全新创建）

## Impact

- **技术栈**: WinUI 3 + .NET 8 桌面开发框架和视频渲染引擎（LibMPV）
- **外部依赖**: 豆瓣（元数据刮削）、射手网 API（在线字幕）、WebDAV 客户端库
- **系统交互**: Windows HDR API（`SetDisplayMode`/`DwmSetWindowAttribute`）、文件系统监控
- **网络**: WebDAV 协议支持、HTTP API 调用（刮削/字幕）
