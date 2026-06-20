namespace SweetPlayer.Services.Scraping;

public interface IPosterCacheService
{
    /// <summary>
    /// 海报缓存目录（绝对路径）。
    /// </summary>
    string CacheDirectory { get; }

    /// <summary>
    /// 根据 doubanId 获取本地海报文件路径，本地不存在时下载并缓存。
    /// </summary>
    Task<string?> GetOrDownloadPosterAsync(string doubanId, string posterUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// 仅获取本地路径（不下载）。
    /// </summary>
    string GetLocalPath(string doubanId);
}

/// <summary>
/// 海报缓存服务，文件命名规则：{doubanId}_poster.jpg。
/// </summary>
public class PosterCacheService : IPosterCacheService
{
    private readonly IDoubanClient _doubanClient;

    public string CacheDirectory { get; }

    public PosterCacheService(IDoubanClient doubanClient)
    {
        _doubanClient = doubanClient;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        CacheDirectory = Path.Combine(localAppData, "SweetPlayer", "posters");
        Directory.CreateDirectory(CacheDirectory);
    }

    public string GetLocalPath(string doubanId)
    {
        return Path.Combine(CacheDirectory, $"{doubanId}_poster.jpg");
    }

    public async Task<string?> GetOrDownloadPosterAsync(string doubanId, string posterUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doubanId)) return null;

        var localPath = GetLocalPath(doubanId);
        if (File.Exists(localPath))
        {
            var info = new FileInfo(localPath);
            if (info.Length > 0)
            {
                return localPath;
            }
        }

        if (string.IsNullOrWhiteSpace(posterUrl)) return null;

        return await _doubanClient.DownloadPosterAsync(posterUrl, localPath, cancellationToken).ConfigureAwait(false);
    }
}
