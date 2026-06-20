using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.MediaSources;

/// <summary>
/// 媒体文件源管理服务：负责本地与 WebDAV 文件源的添加、查询与删除。
/// </summary>
public interface IMediaSourceService
{
    /// <summary>
    /// 添加本地文件夹源（验证路径存在）。
    /// </summary>
    Task<MediaSource> AddLocalSourceAsync(string path, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 添加 WebDAV 文件源（通过 HTTP PROPFIND 验证连接）。
    /// </summary>
    Task<MediaSource> AddWebDavSourceAsync(string url, string username, string password, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// 加载所有已配置的文件源。
    /// </summary>
    Task<IReadOnlyList<MediaSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 Id 删除文件源（级联删除其下视频文件记录）。
    /// </summary>
    Task<bool> RemoveSourceAsync(int sourceId, CancellationToken cancellationToken = default);
}
