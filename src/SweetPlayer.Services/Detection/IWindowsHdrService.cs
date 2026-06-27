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

    /// <summary>启用系统 HDR。</summary>
    Task EnableHdrAsync();

    /// <summary>关闭系统 HDR。</summary>
    Task DisableHdrAsync();
}
