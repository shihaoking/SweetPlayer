## 1. 目录浏览服务

- [x] 1.1 创建 IDirectoryBrowseService 接口和 BrowseEntry 数据模型
- [x] 1.2 实现本地文件源目录列举（Directory.EnumerateFileSystemEntries 单层，过滤视频扩展名）
- [x] 1.3 实现 WebDAV 文件源目录列举（PROPFIND Depth:1 单层，解析响应提取文件夹和视频文件）
- [x] 1.4 注册 IDirectoryBrowseService 到 DI 容器

## 2. 浏览页面 ViewModel

- [x] 2.1 创建 FileSourceBrowserViewModel（CurrentPath、BreadcrumbItems、Entries 集合、IsLoading、ErrorMessage）
- [x] 2.2 实现 LoadDirectoryAsync 方法（调用 IDirectoryBrowseService，文件夹在前文件在后排序）
- [x] 2.3 实现 NavigateToSubdirectoryCommand（进入子目录，更新面包屑）
- [x] 2.4 实现 NavigateToBreadcrumbCommand（点击面包屑跳转到指定层级）
- [x] 2.5 实现 GoBackCommand（返回文件源列表页）
- [x] 2.6 实现错误处理和重试逻辑

## 3. 浏览页面 UI

- [x] 3.1 创建 FileSourceBrowserPage.xaml 页面布局（顶部返回按钮+面包屑 + 内容 ListView + 加载/错误状态）
- [x] 3.2 实现面包屑导航 UI（BreadcrumbBar 或自定义 ItemsControl，支持点击跳转）
- [x] 3.3 实现内容列表 DataTemplate（文件夹：文件夹图标+名称；视频文件：文件图标+名称+大小）
- [x] 3.4 实现加载状态（ProgressRing）和错误状态（错误消息+重试按钮）和空状态提示
- [x] 3.5 实现文件夹点击进入子目录交互

## 4. 导航集成

- [x] 4.1 修改 SourcesPage 文件源卡片点击事件，导航到 FileSourceBrowserPage 并传递 MediaSource 参数
- [x] 4.2 在 NavigationService 中注册 FileSourceBrowserPage
- [x] 4.3 在 App.xaml.cs 中注册 FileSourceBrowserViewModel 到 DI
