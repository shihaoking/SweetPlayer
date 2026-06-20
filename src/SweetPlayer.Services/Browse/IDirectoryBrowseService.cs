using SweetPlayer.Core.Models;

namespace SweetPlayer.Services.Browse;

public interface IDirectoryBrowseService
{
    Task<List<BrowseEntry>> ListDirectoryAsync(MediaSource source, string relativePath, CancellationToken ct = default);
}
