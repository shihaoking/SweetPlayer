using Microsoft.UI.Xaml;

namespace SweetPlayer.Views;

/// <summary>
/// 全局主窗口访问器：WinUI 3 桌面应用中很多 API（FolderPicker / Win32 互操作）需要主窗口
/// 的 HWND，这里提供一个轻量静态入口供各 Page 与服务复用，避免在 App 类型上反复挂属性。
/// </summary>
public static class MainWindowAccessor
{
    /// <summary>当前激活的主窗口实例（在 App.OnLaunched 中赋值）。</summary>
    public static Window? Current { get; set; }
}
