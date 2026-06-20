## Context

当前SweetPlayer的元数据刮削系统仅支持豆瓣作为数据源。`ScrapingService.ScrapeAsync`方法在豆瓣搜索失败时直接返回null，导致部分影片（特别是国外电影、新上映作品）无法获取元数据。

现有架构：
- `ScrapingService`：协调刮削流程（解析文件名 → 搜索豆瓣 → 存储元数据 → 缓存海报）
- `IDoubanClient`：豆瓣API客户端
- `IPosterCacheService`：海报下载和缓存
- `MovieMetadata`模型：存储元数据，当前仅包含`DoubanId`字段

约束条件：
- 必须保持豆瓣作为主要数据源的优先级
- TMDB元数据必须是中文的
- 需要遵守TMDB API速率限制（每秒40个请求）
- 现有刮削队列机制已实现速率控制，可复用

## Goals / Non-Goals

**Goals:**
- 当豆瓣无结果时，自动回退到TMDB进行二次搜索
- 获取中文TMDB元数据（标题、简介、类型、演职人员）
- 支持TMDB海报下载和缓存
- 在数据库中记录元数据来源（Douban/TMDB）
- 保持与现有刮削流程的一致性

**Non-Goals:**
- 不支持同时从两个数据源聚合元数据（只用一个源）
- 不实现TMDB优先模式（豆瓣始终是第一选择）
- 不支持用户手动选择数据源（自动fallback）
- 不支持TMDB的图片、演员详情等高级功能（仅基础元数据）

## Decisions

### 决策1：TMDB客户端架构 - 新建独立客户端类

**选择**：创建独立的`ITmdbClient`接口和`TmdbClient`实现，与`IDoubanClient`平行。

**理由**：
- 遵循现有架构模式，保持一致性
- 便于依赖注入和单元测试
- 两个客户端职责清晰，互不耦合

**备选方案**：
- 创建统一的`IMetadataProvider`抽象，Douban和TMDB都实现它 → 过度设计，当前只需fallback逻辑
- 直接在`ScrapingService`中调用TMDB HTTP API → 违反单一职责原则，难以测试

### 决策2：Fallback逻辑位置 - 在ScrapingService中实现

**选择**：在`ScrapingService.ScrapeAsync`方法中添加fallback逻辑：
```
1. 调用 DoubanClient.SearchAsync()
2. 如果结果为空，调用 TmdbClient.SearchAsync()
3. 根据数据源填充MovieMetadata
```

**理由**：
- `ScrapingService`是刮削流程的协调者，适合包含fallback决策逻辑
- 无需修改客户端接口，职责清晰
- 便于后续添加更多数据源

**备选方案**：
- 创建`MetadataSourceRouter`专门处理数据源选择 → 当前只有两个源，不需要额外抽象
- 在队列服务中实现fallback → 队列服务只负责调度，不应包含业务逻辑

### 决策3：数据模型扩展 - 添加TmdbId和DataSource字段

**选择**：在`MovieMetadata`模型中添加：
- `string? TmdbId`：可空，存储TMDB影片ID
- `MetadataSource DataSource`：枚举（Douban/TMDB），标识元数据来源

**理由**：
- 明确记录数据来源，便于后续更新或调试
- TmdbId用于未来可能的TMDB数据刷新功能
- 两个ID字段互斥（DoubanId和TmdbId只有一个非空）

**备选方案**：
- 只添加TmdbId，通过"哪个ID非空"判断来源 → 不够明确，增加代码复杂度
- 使用单一的`ExternalId`字段加前缀（如"tmdb:12345"） → 需要解析字符串，不如强类型清晰

### 决策4：TMDB中文数据获取 - 使用language=zh-CN参数

**选择**：所有TMDB API请求携带`language=zh-CN`查询参数。

**理由**：
- TMDB官方支持语言参数，返回本地化标题和简介
- 与用户需求一致（刮削到的资料需要是中文的）
- 如果中文数据缺失，TMDB会回退到原始语言

**备选方案**：
- 请求英文数据后调用翻译API → 增加复杂度和成本，TMDB本身支持中文
- 同时请求中英文合并 → 不必要，用户只需要中文

### 决策5：海报处理 - 复用PosterCacheService

**选择**：`PosterCacheService`接受TMDB海报URL，逻辑不变。

**理由**：
- 海报下载和缓存逻辑与数据源无关
- 无需修改现有服务
- TMDB海报URL格式：`https://image.tmdb.org/t/p/w500{poster_path}`

**备选方案**：
- 为TMDB创建独立的海报服务 → 重复代码，没有必要

### 决策6：速率限制 - 复用现有刮削队列

**选择**：TMDB请求在同一个`ScrapingQueueService`队列中执行，共享延迟配置（2-5秒）。

**理由**：
- TMDB速率限制（40请求/秒）远高于当前队列速度，无需额外限制
- 简化实现，无需管理两个独立的速率控制器

**备选方案**：
- 为TMDB创建独立的速率限制器 → 增加复杂度，当前队列速度已足够保守

### 决策7：TMDB API密钥管理 - 使用配置文件

**选择**：在`appsettings.json`中添加：
```json
{
  "Tmdb": {
    "ApiKey": "",
    "BaseUrl": "https://api.themoviedb.org/3",
    "ImageBaseUrl": "https://image.tmdb.org/t/p/w500"
  }
}
```

**理由**：
- 与现有配置模式一致
- 便于部署时配置
- 可通过环境变量覆盖

**备选方案**：
- 硬编码BaseUrl → 不灵活，无法应对TMDB API变更
- 使用用户级配置 → 不合适，API Key是应用级别的

## Risks / Trade-offs

### 风险1：TMDB中文数据不完整
**风险**：部分影片TMDB可能没有中文标题或简介，返回英文。  
**缓解**：
- 接受TMDB返回的原始语言数据作为备选
- 在UI中标注"[原始语言]"提示用户
- 用户可通过手动刮削功能选择豆瓣结果

### 风险2：TMDB API密钥泄露或配额用尽
**风险**：API Key泄露导致滥用，或免费配额（每天数千次请求）耗尽。  
**缓解**：
- 在配置文件示例中提示用户申请个人API Key
- 记录TMDB请求失败日志，便于监控
- 现有速率限制降低请求频率

### 风险3：两个数据源匹配不一致
**风险**：同一部影片在豆瓣和TMDB的元数据可能不一致（如年份、标题）。  
**影响**：用户看到的元数据取决于哪个源先匹配成功，可能产生困惑。  
**缓解**：
- 在MovieMetadata中存储DataSource字段，UI中可显示来源标识
- 允许用户手动重新刮削，选择正确的结果

### 风险4：TMDB搜索结果匹配准确率
**风险**：TMDB搜索可能返回错误匹配（特别是中文查询时）。  
**缓解**：
- 复用现有的`SelectBest`逻辑，优先匹配年份
- 用户可通过手动刮削功能纠正错误匹配

### Trade-off：不聚合多源数据
**选择**：只使用一个数据源（豆瓣优先，TMDB备用），不合并两个源的数据。  
**理由**：
- 简化实现，避免字段冲突和优先级问题
- 单一数据源保证元数据一致性
- 未来如需聚合，可基于DataSource字段扩展

### Trade-off：TMDB不支持电视剧分季数据
**限制**：当前设计仅支持基础元数据，不包含TMDB的TV季/集详细信息。  
**影响**：使用TMDB刮削的电视剧可能缺少季度聚合数据。  
**理由**：
- 聚焦解决电影刮削覆盖率问题
- 电视剧刮削通常在豆瓣成功率较高
- 未来可扩展TMDB TV API支持

## Migration Plan

### 数据库迁移
1. 创建EF Core migration添加字段：
   - `MovieMetadata.TmdbId` (string?, nullable)
   - `MovieMetadata.DataSource` (int, default=0 for Douban)
2. 现有数据默认DataSource为Douban，TmdbId为null
3. 无需回填现有数据（向后兼容）

### 部署步骤
1. 更新数据库Schema（运行migration）
2. 在配置文件中添加TMDB API Key（由用户自行申请）
3. 部署新版本代码
4. 验证：手动触发刮削，观察日志中的TMDB fallback逻辑

### 回滚策略
- 如果TMDB集成出现问题，移除TMDB API Key配置即可禁用
- `TmdbClient.SearchAsync`内部可添加开关检测，无Key时直接返回空结果
- 数据库字段向后兼容，回滚代码不影响现有数据

### 验证方法
- 测试豆瓣有结果的场景：确认不调用TMDB
- 测试豆瓣无结果的场景：确认调用TMDB并存储正确的DataSource
- 测试TMDB海报下载：确认海报正常缓存
- 检查日志：确认fallback逻辑按预期执行

## Open Questions

无待定问题。设计已明确。
