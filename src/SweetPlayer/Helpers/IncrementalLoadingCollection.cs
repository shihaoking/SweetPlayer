using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace SweetPlayer.Helpers;

/// <summary>
/// 支持增量加载的 ObservableCollection。WinUI <see cref="Microsoft.UI.Xaml.Controls.ListViewBase"/> 检测到
/// <see cref="ISupportIncrementalLoading"/> 后会在滚动接近末尾时自动调用 <see cref="LoadMoreItemsAsync"/>。
/// </summary>
/// <typeparam name="T">条目类型。</typeparam>
public sealed class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
{
    private readonly Func<int, int, IReadOnlyList<T>> _pageProvider;
    private readonly int _pageSize;
    private int _loadedCount;
    private int _totalCount;

    /// <summary>
    /// 构造一个分页增量加载集合。
    /// </summary>
    /// <param name="pageProvider">分页数据提供器：参数 (skip, take)，返回该页数据。</param>
    /// <param name="pageSize">每页加载条目数。</param>
    public IncrementalLoadingCollection(Func<int, int, IReadOnlyList<T>> pageProvider, int pageSize = 50)
    {
        _pageProvider = pageProvider ?? throw new ArgumentNullException(nameof(pageProvider));
        _pageSize = pageSize > 0 ? pageSize : 50;
    }

    /// <summary>当前加载完毕后是否仍有数据可加载。</summary>
    public bool HasMoreItems => _loadedCount < _totalCount;

    /// <summary>
    /// 重置数据源：传入新的总数，清空已加载条目，并预取首页。
    /// </summary>
    public void Reset(int totalCount)
    {
        _totalCount = totalCount < 0 ? 0 : totalCount;
        _loadedCount = 0;
        Clear();
        AppendPage(_pageSize);
    }

    /// <summary>
    /// 立即追加一页数据（同步调用，避免增量加载尚未触发时空白）。
    /// </summary>
    private uint AppendPage(int requested)
    {
        var take = Math.Min(requested, _totalCount - _loadedCount);
        if (take <= 0) return 0;

        var page = _pageProvider(_loadedCount, take);
        foreach (var item in page)
        {
            Add(item);
        }
        _loadedCount += page.Count;
        return (uint)page.Count;
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return AsyncInfo.Run(_ => Task.FromResult(new LoadMoreItemsResult
        {
            // WinUI 的 ListViewBase 保证在 UI 线程上调用此方法，
            // 因此可以直接同步操作 ObservableCollection。
            Count = AppendPage((int)Math.Max(count, (uint)_pageSize))
        }));
    }
}
