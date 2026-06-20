namespace SweetPlayer.Services.Detection;

/// <summary>
/// Windows 系统 HDR 模式管理服务。
/// </summary>
public interface IWindowsHdrService
{
    /// <summary>当前主显示器是否支持 HDR。</summary>
    bool IsHdrSupported();

    /// <summary>当前主显示器是否已启用 HDR。</summary>
    bool IsHdrEnabled();

    /// <summary>启用系统 HDR；调用前会记录原始状态以便后续恢复。</summary>
    Task EnableHdrAsync();

    /// <summary>恢复到上次 <see cref="EnableHdrAsync"/> 之前的 HDR 状态。</summary>
    Task DisableHdrAsync();
}
