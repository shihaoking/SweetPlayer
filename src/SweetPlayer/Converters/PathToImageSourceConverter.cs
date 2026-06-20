using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SweetPlayer.Converters;

/// <summary>
/// 将本地文件路径或 URI 字符串转换为 <see cref="BitmapImage"/>，便于直接绑定到
/// Image.Source 上。空值返回 null（控件应通过 NullToVisibilityConverter 隐藏）。
/// </summary>
/// <remarks>
/// 内存优化：
/// 1. <see cref="BitmapImage.DecodePixelWidth"/> 限制为海报展示宽度，避免 4K 海报占用大量内存。
/// 2. <see cref="BitmapCreateOptions.IgnoreImageCache"/> 不开启，复用 WinUI 解码缓存。
/// 3. 解码尺寸基于物理像素，按 DecodePixelType.Logical 与系统缩放对齐。
/// </remarks>
public sealed class PathToImageSourceConverter : IValueConverter
{
    /// <summary>海报卡片显示宽度（与 HomePage 卡片一致），用于 DecodePixelWidth。</summary>
    private const int DefaultDecodePixelWidth = 160;

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null) return null;
        var s = value as string ?? value.ToString();
        if (string.IsNullOrWhiteSpace(s)) return null;

        try
        {
            var bitmap = new BitmapImage
            {
                // 限制解码尺寸：仅按显示宽度解码，避免高分辨率海报占用过多内存
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = DefaultDecodePixelWidth,
            };

            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                bitmap.UriSource = uri;
                return bitmap;
            }
            if (File.Exists(s))
            {
                bitmap.UriSource = new Uri(s);
                return bitmap;
            }
        }
        catch
        {
            // 忽略加载错误，UI 将显示占位图
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
