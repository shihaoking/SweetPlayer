## Context

SweetPlayer 的"文件源"TAB 目前仅展示已添加的文件源卡片列表（类型、路径、扫描状态、文件数）。用户无法在 UI 中直接浏览文件源内部的目录结构。现有的 `MediaScannerService` 支持递归扫描本地和 WebDAV 文件源，但没有提供按路径列举单层目录的能力。

## Goals / Non-Goals

**Goals:**
- 用户点击文件源卡片后，能浏览该文件源的目录内容
- 展示当前目录下的文件夹和视频文件（区分图标）
- 支持点击文件夹进入子目录
- 提供面包屑导航快速返回上级
- 同时支持本地文件源和 WebDAV 文件源

**Non-Goals:**
- 不在浏览页面中实现视频播放（播放通过海报墙入口）
- 不支持文件的复制/移动/删除等管理操作
- 不显示非视频文件（仅视频文件和文件夹）

## Decisions

### 1. 目录浏览服务：新增 IDirectoryBrowseService

**选择**: 在 `SweetPlayer.Services` 中新增 `IDirectoryBrowseService`，提供按路径单层列举的接口

**理由**:
- 与现有的 `IMediaScannerService`（负责递归扫描+数据库写入）职责分离
- 浏览是只读操作，不需要写入数据库
- 统一抽象本地文件系统和 WebDAV 的目录列举

**接口设计**:
```csharp
public interface IDirectoryBrowseService
{
    Task<List<BrowseEntry>> ListDirectoryAsync(MediaSource source, string relativePath, CancellationToken ct);
}

public class BrowseEntry
{
    public string Name { get; set; }
    public string RelativePath { get; set; }
    public bool IsDirectory { get; set; }
    public long? FileSize { get; set; }  // 仅文件有值
}
```

### 2. 页面导航方式

**选择**: 新建独立的 `FileSourceBrowserPage`，通过 Frame 导航传递 MediaSource 参数

**理由**:
- 与 SourcesPage 解耦，避免单页面逻辑过于复杂
- 支持 WinUI 标准的页面导航动画
- 面包屑 + 返回按钮提供清晰的导航路径

### 3. 文件过滤策略

**选择**: 在浏览时同时显示文件夹和视频文件，非视频文件不显示

**理由**:
- 与应用的核心场景一致（视频播放器）
- 复用已有的视频扩展名过滤列表（.mkv, .mp4, .avi 等）
- 减少界面噪音

## Risks / Trade-offs

- **[WebDAV 目录性能]** → 大目录列举可能较慢，使用加载指示器（ProgressRing）提示用户
- **[路径编码]** → WebDAV 路径中的特殊字符（中文、空格、+号等）需要正确 URL 编码，复用已有的 PROPFIND 逻辑并确保编码正确
- **[深层嵌套]** → 面包屑可能很长，使用水平滚动或省略中间路径
