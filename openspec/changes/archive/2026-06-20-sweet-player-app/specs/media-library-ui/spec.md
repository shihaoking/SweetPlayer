## 新增需求

### 需求：三标签页导航结构
系统应使用 WinUI 3 的 NavigationView 控件，采用左侧紧凑模式，包含恰好三个导航项：Home（主屏幕）、Sources（文件源）、Settings（设置）。NavigationView 使用 WinUI 默认暗色主题样式，内置图标支持（SymbolIcon）。标签页切换使用 WinUI 标准的 DrillInNavigationTransitionInfo 页面过渡动画。

#### 场景：应用启动
- **WHEN** 应用启动时
- **THEN** NavigationView 默认选中 Home 项，主内容 Frame 显示首页

#### 场景：用户切换标签页
- **WHEN** 用户点击任意导航项（Home、Sources、Settings）
- **THEN** 系统使用 WinUI 默认页面过渡动画将内容 Frame 导航到对应页面

### 需求：主屏幕海报墙
系统应使用 WinUI 的 GridView 控件显示主屏幕，采用模块化布局：

1. **Hero 区域**（顶部）：一张精选/最近添加电影的大幅背景图（16:9 比例），使用 LinearGradientBrush 渐变淡出至页面背景。电影标题叠加在上方，使用 WinUI TitleLarge 文字样式。

2. **继续观看行**（如适用）：一个水平 ListView 显示带播放进度的视频，每个项目底部使用 WinUI ProgressBar。

3. **最近添加行**：一个水平 ListView，显示新索引内容的海报卡片。

4. **全部电影网格**：使用 WinUI GridView 配合 ItemsWrapGrid 面板。电影海报使用 2:3 宽高比。每个项目模板使用标准 WinUI 卡片样式，默认 CornerRadius 和阴影。中文电影标题显示在下方，使用 Body 文字样式，TextTrimming="CharacterEllipsis"。

5. **电视剧**显示在同一网格中，以第一季海报作为封面。右下角使用 WinUI InfoBadge 或小型 TextBlock 覆盖层显示检测到的季数（如"嬣3季"）。

#### 场景：媒体库有已匹配的电影
- **WHEN** 用户查看 Home 标签页且媒体库中有已索引和匹配的电影
- **THEN** 屏幕显示包含精选电影背景的 Hero 区域，随后是水平行和主 GridView。每张海报卡片使用 WinUI 默认卡片样式，2:3 比例图片，下方显示中文标题

#### 场景：媒体库有已匹配的电视剧
- **WHEN** 视频源包含被匹配为电视剧（多季）的文件
- **THEN** 主屏幕使用第一季海报作为封面显示该剧，右下角显示季数徽章

#### 场景：用户悬停在海报卡片上
- **WHEN** 用户将鼠标光标移至海报卡片上方
- **THEN** 卡片显示 WinUI 默认悬停状态（微妙的阴影变化和背景高亮）

#### 场景：用户点击电影海报
- **WHEN** 用户点击电影海报卡片
- **THEN** 系统以 DrillIn 过渡动画导航到电影详情页，顶部为全宽背景图（带渐变淡出），左侧为海报缩略图，中文标题（TitleLarge 样式）、年份、类型、简介文本（Body 样式），以及一个 WinUI 主题色"播放"按钮

#### 场景：用户点击电视剧海报
- **WHEN** 用户点击电视剧海报卡片
- **THEN** 系统导航到剧集详情页，显示：背景图、剧集中文标题，以及仅包含本地文件源中实际找到季度的水平季度标签/按钮列表。每个季度按钮显示"第N季"。默认选中第一季。

#### 场景：用户在剧集详情中选择季度
- **WHEN** 用户在剧集详情页点击特定季度按钮（如"第2季"）
- **THEN** 系统在季度选择器下方显示一个水平滚动 ListView，展示本地源中该季的所有集数。每集项目显示集号、集标题（如有元数据）和缩略图或剧集卡片。

#### 场景：用户点击剧集播放
- **WHEN** 用户在水平剧集列表中点击特定剧集项目
- **THEN** 系统导航到全屏视频播放页面并开始播放选中的剧集文件

#### 场景：媒体库为空
- **WHEN** 用户查看 Home 标签页且未配置媒体源或无匹配结果
- **THEN** 屏幕显示居中消息"添加文件源以开始"和一个 WinUI 主题色按钮"添加文件源"，点击后导航到文件源页面

### 需求：海报卡片上的 HDR/Dolby 徽章
系统应使用 WinUI 的 InfoBadge 或自定义轻量覆盖层在海报卡片右上角显示可视化徽章。每种徽章类型使用不同的背景色：HDR = 金色/琥珀色，Dolby Vision = 绿色，Dolby Atmos = 蓝色（使用 WinUI 主题资源）。

#### 场景：视频支持 HDR10
- **WHEN** 视频文件被识别为 HDR10 内容
- **THEN** 其海报卡片右上角显示带琥珀/金色背景的"HDR"徽章

#### 场景：视频支持 Dolby Vision
- **WHEN** 视频文件被识别为 Dolby Vision 内容
- **THEN** 其海报卡片右上角显示带绿色背景的"DV"徽章

#### 场景：视频支持 Dolby Atmos
- **WHEN** 视频文件被识别为含 Dolby Atmos 音频
- **THEN** 其海报卡片右上角显示带蓝色背景的"Atmos"徽章，如存在其他徽章则位于其下方

### 需求：文件源标签页
系统应使用 WinUI 标准 ListView 配合卡片样式项目模板来显示文件源标签页。页面标题为"文件源"（使用 WinUI TitleLarge 样式），右上角有 CommandBar 或简单按钮用于"+ 添加文件源"。

每个源项目使用 WinUI 卡片样式容器（默认 CornerRadius 和阴影），包含：
- 左侧：SymbolIcon（本地文件夹用 Folder，WebDAV 用 Globe）
- 中间：第一行为源名称（Subtitle 样式），第二行为路径/URL（Caption 样式）
- 右侧：状态指示器（WinUI ProgressRing 表示扫描中，FontIcon 勾选表示完成，FontIcon 警告表示错误）和 Caption 样式的文件计数

#### 场景：用户查看含已有源的文件源标签页
- **WHEN** 用户导航到文件源标签页且已有配置的源
- **THEN** 系统将每个源显示为 WinUI 卡片样式列表项，包含类型图标、名称、路径、扫描状态指示器和文件计数

#### 场景：用户点击添加新源
- **WHEN** 用户在文件源标签页点击"+ 添加文件源"按钮
- **THEN** 弹出 WinUI ContentDialog，提供两个选项："本地文件夹"（带 Folder 图标）和"WebDAV"（带 Globe 图标）

#### 场景：用户右键点击源卡片
- **WHEN** 用户右键点击已有源卡片
- **THEN** 弹出 WinUI MenuFlyout，选项为："重新扫描"、"编辑"、"删除"

### 需求：设置标签页 - 关于页面
系统应使用 WinUI 标准布局和排版将设置标签页显示为居中的关于页面。内容垂直居中：

1. 应用 Logo：Image 控件，120x120px，居中
2. 应用名称："SweetPlayer"，TitleLarge 样式，居中
3. 版本号："Version 1.0.0"，Caption 样式，名称下方居中
4. 版权信息："© 2024 SweetPlayer"，Caption 样式，版本下方居中

#### 场景：用户查看设置标签页
- **WHEN** 用户导航到设置标签页
- **THEN** 系统使用 WinUI 默认排版显示关于部分：居中 Logo、TitleLarge 样式的应用名称、下方 Caption 样式的版本和版权信息

### 需求：暗色主题 WinUI 风格
系统应使用 WinUI 3 内置的 Fluent Design 暗色主题（RequestedTheme="Dark"），搭配原生控件和标准样式。无需自定义玻璃/亚克力/磨砂效果——依赖 WinUI 默认暗色主题颜色和控件外观：

- 使用 WinUI 原生暗色主题系统颜色（ApplicationPageBackgroundThemeBrush、CardBackgroundFillColorDefaultBrush 等）
- 使用标准 WinUI 控件：NavigationView、GridView、ListView、Button、TextBox、InfoBar、ContentDialog
- 卡片容器使用 WinUI 内置卡片样式（边框、微妙背景阴影）
- 动画使用 WinUI 内置隐式动画和页面过渡（EntranceThemeTransition、DrillInNavigationTransitionInfo）
- 排版使用 WinUI 字体阶梯（TitleLarge、Subtitle、Body、Caption）
- 圆角半径遵循 WinUI 默认值（ControlCornerRadius = 4px，OverlayCornerRadius = 8px）

#### 场景：应用渲染界面
- **WHEN** 应用运行时
- **THEN** 所有视图使用 WinUI 3 暗色主题渲染，搭配原生 Fluent Design 控件、标准阴影和间距，以及内置主题过渡

### 需求：主屏幕搜索和筛选
系统应在主页面内容区域顶部提供 WinUI AutoSuggestBox，PlaceholderText="搜索影片..."，带 QueryIcon。使用 WinUI 默认暗色主题样式。在搜索框左侧提供"刮削影片"按钮，用于手动触发未刮削文件的元数据获取。

#### 场景：用户按中文标题搜索

- **WHEN** 用户在 Home 标签页的 AutoSuggestBox 中输入中文电影或剧集标题
- **THEN** GridView 实时筛选仅显示匹配项目

#### 场景：用户手动触发刮削

- **WHEN** 用户点击主屏幕右上角的"刮削影片"按钮
- **THEN** 系统查询数据库中所有未刮削的视频文件
- **AND** 将这些文件批量加入刮削队列
- **AND** 按钮显示加载状态（ProgressRing 动画）
- **AND** 刮削进行时按钮禁用，防止重复触发

#### 场景：刮削完成

- **WHEN** 所有文件刮削完成
- **THEN** 按钮恢复正常状态，重新启用
- **AND** 主屏幕海报墙自动刷新显示新刮削的影片
