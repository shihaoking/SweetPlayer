## 新增需求

### 需求：视频文件名解析引擎
系统应实现一个文件名解析引擎，从视频文件名中提取结构化元数据。解析器必须按以下优先级顺序支持以下模式：

**电影识别规则（优先级从高到低）：**
1. 带数据库 ID：文件名中包含 `{tmdb-数字}` 或 `{imdb-tt数字}` 标识，直接使用该 ID 查询元数据，跳过搜索步骤
2. 标准格式：文件名符合 `名称 (YYYY).ext`、`名称-YYYY.ext` 或 `名称.YYYY.ext` 格式，提取影片名称和年份作为搜索关键词
3. 简单格式：文件名不符合以上格式时，去除扩展名和常见后缀标记（分辨率、编码等）后，将剩余文本作为搜索关键词

**电视剧识别规则（支持 10 种模式，优先级从高到低）：**
1. `show-name_s01.e02.ext`（季集分离，句号分隔）
2. `show-name_s1e2.ext`（季集连写）
3. `show-name_1x02.ext`（x 分隔符格式）
4. `show-name_se1.ep2.ext`（se/ep 前缀）
5. `show-name-season1.episode2.ext`（全拼写）
6. `show name/any folder/S01E02.ext`（文件夹名为剧名，文件名仅含季集号）
7. `show name/any folder/01.02 EpisodeName.ext`（季.集 + 集名）
8. `show name/season 1/02 EpisodeName.ext`（文件夹含季，文件名含集号）
9. `show name/season 1/1-02 EpisodeName.ext`（季-集 格式）
10. `show name/season 1/episode 02 - EpisodeName.ext`（全拼写 episode）

**分隔符处理：** 句号(.)、空格( )、下划线(_)、破折号(-) 视为等价分隔符，解析时统一处理。

**常见后缀标记过滤：** 解析时应移除以下信息然后再搜索：分辨率（2160p/1080p/720p/4K）、编码（HEVC/H265/H264/AV1/x264/x265）、来源（BluRay/BDRip/WEB-DL/WEBRip/HDRip）、音频（DTS/AAC/FLAC/Atmos）、制作组名等。

#### 场景：使用标准命名的电影文件
- **WHEN** 发现名为 "Interstellar (2014).mkv" 的文件
- **THEN** 解析器提取 title="Interstellar"，year=2014，type=movie

#### 场景：带数据库 ID 的电影文件
- **WHEN** 发现名为 "星际穿越 {tmdb-157336}.mkv" 的文件
- **THEN** 解析器提取 tmdb_id=157336，type=movie，并跳过基于搜索的匹配

#### 场景：S01E02 格式的电视剧
- **WHEN** 发现名为 "Breaking.Bad.S01E02.720p.BluRay.mkv" 的文件
- **THEN** 解析器提取 title="Breaking Bad"，season=1，episode=2，type=tv

#### 场景：基于文件夹结构的电视剧
- **WHEN** 发现路径为 "Breaking Bad/Season 1/02 Pilot.mkv" 的文件
- **THEN** 解析器从父文件夹提取 title="Breaking Bad"，season=1，episode=2，episode_title="Pilot"，type=tv

#### 场景：高集数动漫
- **WHEN** 发现名为 "Boruto Naruto Next Generations.S01E210.mp4" 的文件
- **THEN** 解析器提取 title="Boruto Naruto Next Generations"，season=1，episode=210，type=tv

#### 场景：带发布信息噪声的文件
- **WHEN** 发现名为 "Dune.2021.2160p.UHD.BluRay.x265.HDR.DTS-HD.mkv" 的文件
- **THEN** 解析器去除分辨率/编码/来源标签后提取 title="Dune"，year=2021，type=movie

#### 场景：中文文件名
- **WHEN** 发现名为 "流浪地球 (2019).mkv" 的文件
- **THEN** 解析器提取 title="流浪地球"，year=2019，type=movie

### 需求：从豆瓣自动抓取元数据
系统应使用解析的文件名信息在豆瓣搜索匹配的电影/电视剧元数据。搜索策略：
1. 如果文件名中包含 tmdb/imdb ID，使用 ID 直接查询
2. 否则，使用提取的标题 + 年份（如有）在豆瓣搜索
3. 中文标题直接搜索；英文标题同时搜索原文和翻译结果
4. 优先选择年份与文件名中提取年份一致的结果

#### 场景：通过标题和年份在豆瓣匹配电影
- **WHEN** 解析器提取 title="Interstellar"，year=2014
- **THEN** 系统在豆瓣搜索"星际穿越 2014"，找到匹配条目，并存储中文标题、年份、评分、类型、导演、演员和简介

#### 场景：在豆瓣匹配电视剧
- **WHEN** 解析器提取 title="Breaking Bad"，type=tv
- **THEN** 系统在豆瓣搜索"绝命毒师"，找到剧集条目，并存储剧集级元数据及每季信息

#### 场景：豆瓣返回模糊结果
- **WHEN** 豆瓣搜索返回多个结果
- **THEN** 系统根据以下标准选择最佳匹配：年份匹配 > 标题相似度 > 热度/评分，并标记置信度

### 需求：海报图片获取
系统应为匹配的视频从豆瓣下载并缓存电影/电视剧海报图片。

#### 场景：元数据匹配且含海报
- **WHEN** 视频成功匹配到含海报图片的豆瓣条目
- **THEN** 系统下载海报图片并在本地缓存以供显示

#### 场景：无可用海报
- **WHEN** 匹配的豆瓣条目无海报图片
- **THEN** 系统显示默认占位海报

### 需求：中文元数据优先
系统应使用豆瓣作为主要元数据源，其自然提供中文标题、描述和评分作为默认语言。

#### 场景：电影有完整豆瓣元数据
- **WHEN** 电影在豆瓣上匹配成功
- **THEN** 系统存储并显示中文标题、豆瓣评分、类型、导演、演员和剧情简介

#### 场景：电影在豆瓣上未找到
- **WHEN** 电影无法在豆瓣上匹配
- **THEN** 系统将文件标记为"未匹配"并仅显示原始文件名

### 需求：手动元数据修正
系统应允许用户手动搜索和重新匹配任何视频文件的元数据。

#### 场景：用户修正错误匹配
- **WHEN** 用户选择一个元数据不正确的视频并触发"重新匹配"
- **THEN** 系统展示豆瓣搜索结果并允许用户选择正确匹配

### 需求：抓取速率限制
系统应对豆瓣请求实现速率限制以避免被封禁，请求间延迟可配置。

#### 场景：批量扫描触发大量抓取请求
- **WHEN** 单次扫描发现 100 个新视频文件
- **THEN** 系统将抓取请求排队，并采用适当的速率限制处理，以避免触发豆瓣反爬措施

### 需求：多版本和版本识别
系统应检测文件名中的版本/版本标签，并将同一电影的多个版本分组在一起。

支持的版本模式：
- 标准标签：Director's Cut、Extended Cut、Theatrical Cut、IMAX、Unrated 等
- 自定义标签：`{edition-自定义文本}` 格式
- 多部分：cdX、discX、partX 模式

#### 场景：电影有多个版本
- **WHEN** 发现文件 "Get Out (2017) {edition-Regular Ending}.mp4" 和 "Get Out (2017) {edition-Alternate Ending}.mp4"
- **THEN** 系统将它们归组到同一电影条目下，并显示版本标签

### 需求：电视剧季度聚合
系统应将电视剧所有季度和集数聚合到媒体库中的单一剧集条目下，跟踪检测到的总季数和每季集数。

#### 场景：发现多个季度
- **WHEN** 在源文件夹中发现 Breaking Bad S01E01 至 S05E16 的文件
- **THEN** 系统创建一个剧集条目"Breaking Bad"，显示 5 季及每季正确集数
