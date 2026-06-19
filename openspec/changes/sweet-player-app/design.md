## Context

SweetPlayer 是一个从零开始构建的 Windows 桌面视频播放器，目标用户是拥有大量本地/NAS 视频资源的影视爱好者。当前 Windows 平台上缺少一款兼具美观 UI（类 Infuse）、智能媒体管理和高级播放能力（HDR/杜比）的一体化播放器。

项目需要在 Windows 10/11 上运行，支持触控和键鼠操作，需具备高性能视频解码能力和现代化 UI 框架。

## Goals / Non-Goals

**Goals:**
- 提供类 Infuse 的沉浸式海报墙 UI 体验
- 支持本地和 WebDAV 文件源的统一管理
- 自动化媒体信息刮削和海报获取
- 智能 HDR/杜比检测与系统 HDR 自动切换
- 完善的字幕生态（本地 + 在线）
- 主流视频格式全覆盖播放
- 整体UI设计使用WinUI 3，控件也是使用WinUI 3的组件库

**Non-Goals:**
- 不做流媒体服务（Netflix/Disney+ 等）集成
- 不做视频转码/编辑功能
- 不做移动端或跨平台版本（首期仅 Windows）
- 不做 DLNA/Chromecast 投屏（可后续扩展）
- 不做用户账号系统和云同步

## Decisions

### 1. 桌面框架：WinUI 3 + .NET 8

**选择**: WinUI 3（Windows App SDK）基于 .NET 8

**理由**:
- WinUI 3 是微软官方推荐的现代 Windows 桌面开发框架
- 内置 Fluent Design 设计系统，提供统一的暗色主题和控件风格
- .NET 8 LTS 版本，性能优秀
- 原生支持 Windows 10/11 的现代控件和布局系统
- 相比 Electron：原生性能更好，内存占用更低

**替代方案**:
- WPF：成熟但设计语言较旧，缺乏现代 Fluent 控件
- Avalonia UI：跨平台但 Windows 专有功能（HDR API）集成复杂
- Electron + mpv：跨平台但资源消耗大

### 2. 视频播放引擎：LibMPV

**选择**: LibMPV（mpv 的库版本）

**理由**:
- 格式支持最广泛（几乎所有主流格式开箱即用）
- 原生支持 HDR 直通、杜比视界 Profile 5/7/8
- 字幕渲染质量高（ASS/SSA 特效完美支持）
- 硬件解码支持完善（D3D11VA、DXVA2）
- 活跃维护，性能经过大规模验证

**替代方案**:
- LibVLC：格式支持好但 HDR 直通和杜比视界支持较弱
- FFmpeg 自行封装：工作量巨大，维护成本高
- Windows Media Foundation：格式支持有限

### 3. 元数据刮削源：豆瓣

**选择**: 豆瓣（Douban）作为主要刮削源

**理由**:
- 中文影视信息最全面，评分体系成熟
- 自然提供中文标题、简介、演职人员等信息
- 海报图片质量高
- 国内用户最熟悉的影视数据库

**替代方案**:
- TMDb：免费 API 但中文数据覆盖不如豆瓣
- OMDB：数据量不如 TMDb，中文支持弱

**风险**:
- 豆瓣无官方开放 API，需要通过网页解析或第三方接口获取数据
- 需要实现反爬虫对策（限速、缓存、请求延迟）
- 后续可扩展 TMDb 作为备用源

### 4. 在线字幕源：射手网 API

**选择**: 射手网（shooter.cn）API

**理由**:
- 用户明确需求
- 中文字幕资源丰富
- 支持文件哈希匹配和文件名搜索

### 5. 数据存储：SQLite

**选择**: SQLite + Entity Framework Core

**理由**:
- 无需额外数据库服务，单文件部署
- 性能满足本地媒体库管理需求
- EF Core 提供优雅的 ORM 体验

### 6. UI 架构：MVVM + CommunityToolkit + WinUI 默认风格

**选择**: MVVM 架构 + CommunityToolkit.Mvvm，UI 使用 WinUI 3 自带的 Fluent Design 暗色主题和原生控件风格

**理由**:
- WinUI 3 原生控件已具备统一的 Fluent Design 风格
- 暗色主题开箱即用，无需自定义颜色方案
- CommunityToolkit 减少样板代码
- 便于单元测试和后续维护

## Risks / Trade-offs

- **[MPV 集成复杂度]** → 使用 LibMPVSharp 封装库降低集成难度；预留 fallback 到 LibVLC 的接口抽象
- **[豆瓣数据获取稳定性]** → 豆瓣无官方 API，需通过网页解析或第三方接口，实现健壮的缓存和重试机制，后续可扩展 TMDb 作为备用源
- **[射手网 API 稳定性]** → 射手网服务可能不稳定，设计容错机制，后续可扩展其他字幕源
- **[Windows HDR API 兼容性]** → 不同显卡驱动行为可能不一致，需要在 NVIDIA/AMD/Intel 上分别测试
- **[杜比视界检测]** → 需要解析视频容器元数据（HEVC SEI/配置记录），可能存在边缘格式识别不全
- **[WebDAV 性能]** → 大文件列表可能慢，实现异步加载和分页扫描
- **[首次启动体验]** → 媒体库为空时需要引导用户添加文件源，避免空白页面

## Open Questions

- 是否需要支持 TV 模式（10-foot UI）用于 HTPC 场景？
- 是否需要支持外挂音轨切换？
- 海报墙是否需要支持按类型/年份/评分等分类筛选？
