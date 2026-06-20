using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SweetPlayer.Core.Models;
using SweetPlayer.Services.MediaSources;
using SweetPlayer.Services.Scanning;

namespace SweetPlayer.ViewModels;

/// <summary>
/// 文件源管理页 ViewModel：负责加载、添加、删除文件源以及触发手动扫描。
/// 仅 ViewModel 层，UI 在模块 7 实现。
/// </summary>
public sealed partial class SourcesViewModel : ViewModelBase
{
    private readonly IMediaSourceService _sourceService;
    private readonly IMediaScannerService _scannerService;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public SourcesViewModel(
        IMediaSourceService sourceService,
        IMediaScannerService scannerService)
    {
        _sourceService = sourceService;
        _scannerService = scannerService;
        Sources = new ObservableCollection<MediaSource>();
    }

    /// <summary>
    /// 文件源列表，绑定到 UI。
    /// </summary>
    public ObservableCollection<MediaSource> Sources { get; }

    /// <summary>
    /// 从数据库加载文件源列表。
    /// </summary>
    public async Task LoadSourcesAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var items = await _sourceService.GetSourcesAsync(cancellationToken).ConfigureAwait(true);
            Sources.Clear();
            foreach (var item in items)
            {
                Sources.Add(item);
            }
            StatusMessage = $"已加载 {items.Count} 个文件源";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载文件源失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddLocalSourceAsync(LocalSourceInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Path) || string.IsNullOrWhiteSpace(input.Name))
        {
            StatusMessage = "请提供本地路径和名称";
            return;
        }

        IsBusy = true;
        try
        {
            var source = await _sourceService.AddLocalSourceAsync(input.Path, input.Name).ConfigureAwait(true);
            Sources.Add(source);
            StatusMessage = $"已添加本地源：{source.Name}，正在扫描...";

            // 自动触发首次扫描
            var scanResult = await _scannerService.ScanSourceAsync(source).ConfigureAwait(true);
            if (scanResult.HasError)
            {
                StatusMessage = $"已添加 {source.Name}，但扫描失败：{scanResult.ErrorMessage}";
            }
            else
            {
                StatusMessage = $"已添加 {source.Name}，扫描完成：找到 {scanResult.TotalCount} 个视频文件";
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"添加本地源失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddWebDavSourceAsync(WebDavSourceInput? input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Url) || string.IsNullOrWhiteSpace(input.Name))
        {
            StatusMessage = "请提供 WebDAV URL 和名称";
            return;
        }

        IsBusy = true;
        try
        {
            var source = await _sourceService.AddWebDavSourceAsync(
                input.Url,
                input.Username ?? string.Empty,
                input.Password ?? string.Empty,
                input.Name).ConfigureAwait(true);
            Sources.Add(source);
            StatusMessage = $"已添加 WebDAV 源：{source.Name}，正在扫描...";

            // 自动触发首次扫描
            var scanResult = await _scannerService.ScanSourceAsync(source).ConfigureAwait(true);
            if (scanResult.HasError)
            {
                StatusMessage = $"已添加 {source.Name}，但扫描失败：{scanResult.ErrorMessage}";
            }
            else
            {
                StatusMessage = $"已添加 {source.Name}，扫描完成：找到 {scanResult.TotalCount} 个视频文件";
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"添加 WebDAV 源失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSourceAsync(MediaSource? source)
    {
        if (source is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _sourceService.RemoveSourceAsync(source.Id).ConfigureAwait(true);
            if (ok)
            {
                Sources.Remove(source);
                StatusMessage = $"已删除文件源：{source.Name}";
            }
            else
            {
                StatusMessage = $"未找到要删除的文件源：{source.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除文件源失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanSourceAsync(MediaSource? source)
    {
        if (source is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _scannerService.ScanSourceAsync(source).ConfigureAwait(true);
            if (result.HasError)
            {
                StatusMessage = $"扫描 {source.Name} 失败：{result.ErrorMessage}";
            }
            else
            {
                StatusMessage = $"扫描 {source.Name} 完成：新增 {result.AddedCount}，移除 {result.RemovedCount}，共 {result.TotalCount}";
                await LoadSourcesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"扫描文件源出错：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// 添加本地源的输入参数。
/// </summary>
public sealed record LocalSourceInput(string Path, string Name);

/// <summary>
/// 添加 WebDAV 源的输入参数。
/// </summary>
public sealed record WebDavSourceInput(string Url, string? Username, string? Password, string Name);
