## Why

当前系统仅依赖豆瓣作为唯一的元数据源，导致部分影片（特别是国外电影、冷门作品或新上映影片）无法获取到元数据，影响用户体验。添加TMDB作为备用数据源可以显著提高元数据覆盖率，确保更多影片能被正确识别和展示。

## What Changes

- 集成TMDB API客户端，支持通过标题、年份搜索影片并获取详细元数据
- 在刮削服务中实现fallback逻辑：当豆瓣搜索失败或无结果时，自动尝试TMDB
- TMDB API调用时指定中文语言参数（`language=zh-CN`），确保返回中文标题、简介等元数据
- 在MovieMetadata模型中添加TmdbId字段，用于存储TMDB数据源标识
- 支持从TMDB下载和缓存海报图片
- 保持豆瓣作为主要数据源的优先级不变

## Capabilities

### New Capabilities

无新增独立能力模块。

### Modified Capabilities

- `metadata-scraping`: 扩展刮削逻辑，添加TMDB作为备用数据源。当豆瓣无法匹配影片时，自动回退到TMDB进行二次搜索，并获取中文元数据。

## Impact

### 代码影响

- **SweetPlayer.Services/Scraping/ScrapingService.cs**: 修改刮削主流程，添加TMDB fallback逻辑
- **SweetPlayer.Core/Models/MovieMetadata.cs**: 添加`TmdbId`和`DataSource`字段
- **新增文件**:
  - `SweetPlayer.Services/Scraping/ITmdbClient.cs`: TMDB API客户端接口
  - `SweetPlayer.Services/Scraping/TmdbClient.cs`: TMDB API客户端实现
  - `SweetPlayer.Services/Scraping/TmdbModels.cs`: TMDB API响应模型

### 依赖项

- 需要TMDB API密钥（需在配置文件或环境变量中配置）
- 可能需要添加HTTP客户端相关NuGet包（如果现有HttpClient不足）

### 配置

- 需在应用配置中添加TMDB API Key配置项
- 可选：添加是否启用TMDB fallback的开关配置

### 数据库

- 需要migration添加`MovieMetadata.TmdbId`字段（可空字符串）
- 需要migration添加`MovieMetadata.DataSource`枚举字段（Douban/TMDB）
