using System.Runtime.InteropServices;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// COM 接口用于将 DXGI SwapChain 关联到 WinUI 3 的 SwapChainPanel。
/// </summary>
[ComImport]
[Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISwapChainPanelNative
{
    /// <summary>
    /// 将 IDXGISwapChain 关联到 SwapChainPanel。
    /// </summary>
    /// <param name="swapChain">DXGI SwapChain 指针</param>
    /// <returns>HRESULT</returns>
    [PreserveSig]
    int SetSwapChain(IntPtr swapChain);
}
