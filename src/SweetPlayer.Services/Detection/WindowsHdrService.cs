using System.Runtime.InteropServices;

namespace SweetPlayer.Services.Detection;

/// <summary>
/// 通过 Windows DisplayConfig API（user32.dll）查询/切换主显示器的 HDR 状态。
/// </summary>
/// <remarks>
/// 主要使用 <c>QueryDisplayConfig</c> + <c>DisplayConfigGetDeviceInfo</c>(GET_ADVANCED_COLOR_INFO)
/// 与 <c>DisplayConfigSetDeviceInfo</c>(SET_ADVANCED_COLOR_STATE)。
/// 仅作用于第一个 active 路径（主显示器）。
/// </remarks>
public class WindowsHdrService : IWindowsHdrService
{
    private bool? _previousHdrEnabled;

    /// <inheritdoc />
    public bool IsHdrSupported()
    {
        if (TryGetAdvancedColorInfo(out var info))
        {
            return (info.value & ADVANCED_COLOR_SUPPORTED) != 0;
        }
        return false;
    }

    /// <inheritdoc />
    public bool IsHdrEnabled()
    {
        if (TryGetAdvancedColorInfo(out var info))
        {
            return (info.value & ADVANCED_COLOR_ENABLED) != 0;
        }
        return false;
    }

    /// <inheritdoc />
    public Task EnableHdrAsync()
    {
        return Task.Run(() =>
        {
            _previousHdrEnabled = IsHdrEnabled();
            if (_previousHdrEnabled == true)
            {
                return;
            }

            if (IsHdrSupported())
            {
                TrySetAdvancedColorState(true);
            }
        });
    }

    /// <inheritdoc />
    public Task DisableHdrAsync()
    {
        return Task.Run(() =>
        {
            // 没有记录原始状态时，按禁用处理（不改动）
            if (_previousHdrEnabled is null)
            {
                return;
            }

            var previous = _previousHdrEnabled.Value;
            _previousHdrEnabled = null;

            var current = IsHdrEnabled();
            if (current == previous)
            {
                return;
            }

            TrySetAdvancedColorState(previous);
        });
    }

    // ---------- Native interop ----------

    private const uint ADVANCED_COLOR_SUPPORTED = 0x1;
    private const uint ADVANCED_COLOR_ENABLED = 0x2;

    private const int DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
    private const int DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;

    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const int ERROR_SUCCESS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // bit flags: 0x1 supported, 0x2 enabled, 0x4 force disabled, ...
        public int colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint value; // bit 0 = enable
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public int outputTechnology;
        public int rotation;
        public int scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public int scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DISPLAYCONFIG_MODE_INFO_BLOB
    {
        // 占位 64 字节，匹配 DISPLAYCONFIG_MODE_INFO 联合体大小。
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public int infoType;
        public uint id;
        public LUID adapterId;
        public DISPLAYCONFIG_MODE_INFO_BLOB blob;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE setPacket);

    private static bool TryGetActiveTarget(out LUID adapterId, out uint targetId)
    {
        adapterId = default;
        targetId = 0;

        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != ERROR_SUCCESS)
        {
            return false;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != ERROR_SUCCESS)
        {
            return false;
        }

        if (pathCount == 0)
        {
            return false;
        }

        // 取第一条 active 路径作为主显示器
        adapterId = paths[0].targetInfo.adapterId;
        targetId = paths[0].targetInfo.id;
        return true;
    }

    private static bool TryGetAdvancedColorInfo(out DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO info)
    {
        info = default;

        if (!TryGetActiveTarget(out var adapter, out var targetId))
        {
            return false;
        }

        info.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
        info.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        info.header.adapterId = adapter;
        info.header.id = targetId;

        return DisplayConfigGetDeviceInfo(ref info) == ERROR_SUCCESS;
    }

    private static bool TrySetAdvancedColorState(bool enable)
    {
        if (!TryGetActiveTarget(out var adapter, out var targetId))
        {
            return false;
        }

        var packet = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header =
            {
                type = DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = adapter,
                id = targetId,
            },
            value = enable ? 1u : 0u,
        };

        return DisplayConfigSetDeviceInfo(ref packet) == ERROR_SUCCESS;
    }
}
