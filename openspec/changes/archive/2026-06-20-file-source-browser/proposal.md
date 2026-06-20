## Why

当前"文件源"TAB 仅展示已添加的文件源卡片列表，用户无法直接浏览文件源内部的目录结构和文件。用户需要能够点击某个文件源后查看其包含的文件夹和文件，并支持进入子目录浏览，以便快速定位和了解文件源中的实际内容。

## What Changes

- 新增"文件源浏览"页面，点击文件源卡片后导航到该页面
- 展示当前目录下的文件夹和视频文件列表
- 支持点击文件夹进入子目录查看
- 顶部显示当前路径面包屑导航，支持快速返回上级目录
- 文件夹显示文件夹图标和名称
- 视频文件显示文件图标、文件名和文件大小

## Capabilities

### New Capabilities
- `file-source-browser`: 文件源内容浏览功能，支持目录层级导航、文件列表展示、面包屑路径导航

### Modified Capabilities

## Impact

- 新增 `Views/FileSourceBrowserPage.xaml` 页面和对应 ViewModel
- 修改 `SourcesPage` 中文件源卡片的点击事件，添加导航到浏览页面的逻辑
- 可能需要扩展 `IMediaScannerService` 以支持按路径列举单层目录内容（非递归）
- 不影响现有的扫描逻辑和数据库模型
