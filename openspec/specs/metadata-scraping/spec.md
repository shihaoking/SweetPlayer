## ADDED Requirements

### 需求：TMDB备用数据源集成
系统应集成TMDB（The Movie Database）作为备用元数据源。当豆瓣搜索失败或无匹配结果时，系统必须自动尝试从TMDB获取元数据。

#### 场景：豆瓣无结果时回退到TMDB
- **WHEN** 系统在豆瓣搜索影片"Dune (2021)"无结果
- **THEN** 系统自动使用相同搜索关键词查询TMDB API
- **AND** 如果TMDB返回匹配结果，使用TMDB元数据填充MovieMetadata

#### 场景：豆瓣有结果时不查询TMDB
- **WHEN** 系统在豆瓣成功匹配影片"流浪地球 (2019)"
- **THEN** 系统不再查询TMDB
- **AND** 直接使用豆瓣元数据

#### 场景：豆瓣和TMDB都无结果
- **WHEN** 系统在豆瓣和TMDB均无法匹配某个影片
- **THEN** 系统标记该视频文件为"未匹配"状态
- **AND** 记录刮削失败日志，包含已尝试的数据源

### 需求：TMDB返回中文元数据
系统在调用TMDB API时必须指定中文语言参数，确保返回的标题、简介、类型等文本内容为中文。

#### 场景：TMDB API请求使用中文语言
- **WHEN** 系统查询TMDB搜索或详情接口
- **THEN** 请求参数必须包含`language=zh-CN`
- **AND** 返回的元数据字段（title、overview等）应为中文

#### 场景：TMDB中文数据缺失时的回退
- **WHEN** TMDB中文语言请求返回空标题或简介
- **THEN** 系统使用TMDB原始标题（original_title）作为备用
- **AND** 在UI中标注该元数据来自TMDB且为原始语言

### 需求：TMDB元数据存储
系统必须在MovieMetadata表中记录元数据的来源，并存储TMDB的唯一标识符以便后续更新。

#### 场景：存储TMDB来源的元数据
- **WHEN** 系统成功从TMDB获取影片元数据
- **THEN** 在MovieMetadata记录中设置TmdbId字段为TMDB影片ID
- **AND** 设置DataSource字段为"TMDB"
- **AND** DoubanId字段保持为null

#### 场景：存储豆瓣来源的元数据
- **WHEN** 系统成功从豆瓣获取影片元数据
- **THEN** 在MovieMetadata记录中设置DoubanId字段
- **AND** 设置DataSource字段为"Douban"
- **AND** TmdbId字段保持为null

### 需求：TMDB海报图片缓存
系统应支持从TMDB下载海报图片并本地缓存，处理方式与豆瓣海报一致。

#### 场景：TMDB元数据包含海报URL
- **WHEN** TMDB返回的影片详情包含poster_path字段
- **THEN** 系统拼接完整海报URL（使用TMDB配置的base_url和尺寸）
- **AND** 下载海报到本地缓存目录
- **AND** 在MovieMetadata中记录PosterLocalPath

#### 场景：TMDB无海报时使用占位图
- **WHEN** TMDB返回的影片详情poster_path为null
- **THEN** 系统不下载海报
- **AND** UI显示默认占位海报

### 需求：TMDB API速率限制
系统必须遵守TMDB API的速率限制政策，避免请求过于频繁导致API密钥被封禁。

#### 场景：TMDB请求遵守速率限制
- **WHEN** 系统连续刮削多个文件需要查询TMDB
- **THEN** 每次TMDB API请求之间应延迟至少1秒
- **AND** 使用与豆瓣相同的刮削队列机制控制并发

#### 场景：TMDB返回429错误时重试
- **WHEN** TMDB API返回429 Too Many Requests错误
- **THEN** 系统记录警告日志
- **AND** 等待5秒后重试一次
- **AND** 如果再次失败则标记为刮削失败

## MODIFIED Requirements

### 需求：从豆瓣自动抓取元数据
系统应优先使用解析的文件名信息在豆瓣搜索匹配的电影/电视剧元数据。如果豆瓣搜索失败或无结果，系统必须自动回退到TMDB数据源进行二次搜索。搜索策略：
1. 如果文件名中包含 tmdb/imdb ID，使用 ID 直接查询
2. 否则，使用提取的标题 + 年份（如有）在豆瓣搜索
3. 中文标题直接搜索；英文标题同时搜索原文和翻译结果
4. 优先选择年份与文件名中提取年份一致的结果
5. **如果豆瓣无结果，使用相同搜索关键词查询TMDB（language=zh-CN）**
6. **如果TMDB有匹配结果，使用TMDB元数据并标记数据源**

#### 场景：通过标题和年份在豆瓣匹配电影
- **WHEN** 解析器提取 title="Interstellar"，year=2014
- **THEN** 系统在豆瓣搜索"星际穿越 2014"，找到匹配条目，并存储中文标题、年份、评分、类型、导演、演员和简介

#### 场景：在豆瓣匹配电视剧
- **WHEN** 解析器提取 title="Breaking Bad"，type=tv
- **THEN** 系统在豆瓣搜索"绝命毒师"，找到剧集条目，并存储剧集级元数据及每季信息

#### 场景：豆瓣返回模糊结果
- **WHEN** 豆瓣搜索返回多个结果
- **THEN** 系统根据以下标准选择最佳匹配：年份匹配 > 标题相似度 > 热度/评分，并标记置信度

#### 场景：豆瓣无结果时自动尝试TMDB
- **WHEN** 解析器提取 title="The Matrix"，year=1999
- **THEN** 系统首先在豆瓣搜索"黑客帝国 1999"
- **AND** 如果豆瓣返回空结果列表
- **THEN** 系统自动使用"The Matrix"和year=1999查询TMDB API（language=zh-CN）
- **AND** 如果TMDB返回匹配结果，使用TMDB元数据填充MovieMetadata并标记DataSource为"TMDB"
