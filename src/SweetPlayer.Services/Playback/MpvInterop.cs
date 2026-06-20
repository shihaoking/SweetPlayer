using System.Runtime.InteropServices;

namespace SweetPlayer.Services.Playback;

/// <summary>
/// libmpv 核心 C API 的 P/Invoke 封装。
/// </summary>
/// <remarks>
/// 仅声明关键函数与常量；具体的渲染上下文回调 / 选项映射由调用方按需扩展。
/// libmpv 动态库（mpv-1.dll / mpv-2.dll）需随应用一同部署。
/// </remarks>
public static class MpvInterop
{
    /// <summary>libmpv 动态库名（无后缀，由 P/Invoke 解析）。</summary>
    public const string LibraryName = "mpv-2";

    // ---------- 错误码 ----------
    public const int MPV_ERROR_SUCCESS = 0;

    // ---------- 事件 ID ----------
    public const int MPV_EVENT_NONE = 0;
    public const int MPV_EVENT_SHUTDOWN = 1;
    public const int MPV_EVENT_LOG_MESSAGE = 2;
    public const int MPV_EVENT_GET_PROPERTY_REPLY = 3;
    public const int MPV_EVENT_SET_PROPERTY_REPLY = 4;
    public const int MPV_EVENT_COMMAND_REPLY = 5;
    public const int MPV_EVENT_START_FILE = 6;
    public const int MPV_EVENT_END_FILE = 7;
    public const int MPV_EVENT_FILE_LOADED = 8;
    public const int MPV_EVENT_PROPERTY_CHANGE = 22;
    public const int MPV_EVENT_PLAYBACK_RESTART = 21;

    // ---------- 数据格式 ----------
    public const int MPV_FORMAT_NONE = 0;
    public const int MPV_FORMAT_STRING = 1;
    public const int MPV_FORMAT_OSD_STRING = 2;
    public const int MPV_FORMAT_FLAG = 3;
    public const int MPV_FORMAT_INT64 = 4;
    public const int MPV_FORMAT_DOUBLE = 5;

    // ---------- Render API 常量 ----------
    public const int MPV_RENDER_PARAM_INVALID = 0;
    public const int MPV_RENDER_PARAM_API_TYPE = 1;
    public const int MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
    public const int MPV_RENDER_PARAM_OPENGL_FBO = 3;
    public const int MPV_RENDER_PARAM_FLIP_Y = 4;
    public const int MPV_RENDER_PARAM_DEPTH = 5;
    public const int MPV_RENDER_PARAM_ICC_PROFILE = 6;
    public const int MPV_RENDER_PARAM_AMBIENT_LIGHT = 7;
    public const int MPV_RENDER_PARAM_X11_DISPLAY = 8;
    public const int MPV_RENDER_PARAM_WL_DISPLAY = 9;
    public const int MPV_RENDER_PARAM_ADVANCED_CONTROL = 10;
    public const int MPV_RENDER_PARAM_NEXT_FRAME_INFO = 11;
    public const int MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12;
    public const int MPV_RENDER_PARAM_SKIP_RENDERING = 13;
    public const int MPV_RENDER_PARAM_DRM_DISPLAY = 14;
    public const int MPV_RENDER_PARAM_DRM_OSD_SIZE = 15;
    public const int MPV_RENDER_PARAM_DRM_DISPLAY_V2 = 16;
    public const int MPV_RENDER_PARAM_SW_SIZE = 17;
    public const int MPV_RENDER_PARAM_SW_FORMAT = 18;
    public const int MPV_RENDER_PARAM_SW_STRIDE = 19;
    public const int MPV_RENDER_PARAM_SW_POINTER = 20;

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvRenderParam
    {
        public int type;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvEvent
    {
        public int event_id;
        public int error;
        public ulong reply_userdata;
        public IntPtr data;
    }

    // ---------- 核心生命周期 ----------
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    // ---------- 选项 / 属性 ----------
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_observe_property(IntPtr ctx, ulong reply_userdata, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int format);

    // ---------- 命令 ----------
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr args);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command_async(IntPtr ctx, ulong reply_userdata, IntPtr args);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_command_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string args);

    // ---------- 事件循环 ----------
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_wakeup(IntPtr ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_set_wakeup_callback(IntPtr ctx, IntPtr cb, IntPtr d);

    // ---------- Render API ----------
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(out IntPtr res, IntPtr ctx, IntPtr param);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(IntPtr ctx, IntPtr param);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(IntPtr ctx, IntPtr callback, IntPtr callback_ctx);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr ctx);
}
