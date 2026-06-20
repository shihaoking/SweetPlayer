## 1. 数据模型扩展

- [x] 1.1 在MovieMetadata模型中添加TmdbId字段（string? 可空）
- [x] 1.2 在MovieMetadata模型中添加DataSource枚举字段（MetadataSource枚举：Douban=0, TMDB=1）
- [x] 1.3 创建EF Core数据库迁移文件
- [x] 1.4 更新数据库Schema（运行migration）

## 2. TMDB客户端实现

- [x] 2.1 创建TmdbModels.cs定义TMDB API响应模型（TmdbSearchResult、TmdbMovieDetail等）
- [x] 2.2 创建ITmdbClient接口，定义SearchAsync和GetDetailAsync方法
- [x] 2.3 实现TmdbClient类，包含HTTP客户端配置和API调用逻辑
- [x] 2.4 在SearchAsync中添加language=zh-CN参数
- [x] 2.5 在GetDetailAsync中添加language=zh-CN参数
- [x] 2.6 实现TMDB海报URL拼接逻辑（base_url + size + poster_path）
- [x] 2.7 添加TMDB API错误处理和日志记录

## 3. 配置管理

- [x] 3.1 在appsettings.json中添加Tmdb配置节（ApiKey、BaseUrl、ImageBaseUrl）
- [x] 3.2 创建TmdbOptions配置类
- [x] 3.3 在依赖注入容器中注册TmdbOptions和ITmdbClient

## 4. 刮削服务Fallback逻辑

- [x] 4.1 在ScrapingService.ScrapeAsync中修改豆瓣搜索逻辑，判断结果是否为空
- [x] 4.2 当豆瓣无结果时，调用TmdbClient.SearchAsync进行二次搜索
- [x] 4.3 实现SelectBest方法的TMDB版本（选择最佳匹配结果）
- [x] 4.4 修改UpsertMetadataAsync方法，根据数据源填充TmdbId或DoubanId
- [x] 4.5 修改UpsertMetadataAsync方法，设置DataSource字段
- [x] 4.6 确保TMDB海报URL能正确传递给PosterCacheService
- [x] 4.7 添加TMDB fallback逻辑的日志记录（搜索失败、fallback触发、TMDB匹配成功等）

## 5. 手动刮削支持

- [ ] 5.1 在ManualSearchAsync方法中添加可选的数据源参数（默认豆瓣）
- [ ] 5.2 在ApplyManualMatchAsync方法中支持TMDB ID匹配
- [x] 5.3 确保手动匹配时正确设置DataSource字段

## 6. 测试与验证

- [ ] 6.1 测试豆瓣有结果的场景（确认不调用TMDB）
- [ ] 6.2 测试豆瓣无结果的场景（确认调用TMDB并存储正确数据）
- [ ] 6.3 测试TMDB返回中文元数据（验证language参数生效）
- [ ] 6.4 测试TMDB海报下载和缓存
- [ ] 6.5 测试豆瓣和TMDB都无结果的场景（确认正确标记为未匹配）
- [ ] 6.6 检查日志输出，确认fallback逻辑执行顺序正确
- [ ] 6.7 验证数据库中TmdbId和DataSource字段正确存储

## 7. 文档更新

- [x] 7.1 在README或配置文档中说明如何获取TMDB API Key
- [x] 7.2 在appsettings.json示例中添加Tmdb配置注释
- [x] 7.3 更新用户文档，说明元数据来源标识功能
