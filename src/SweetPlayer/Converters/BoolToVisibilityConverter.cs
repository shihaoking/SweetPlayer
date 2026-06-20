using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SweetPlayer.Converters;

/// <summary>
/// 将 <see cref="bool"/> 转换为 <see cref="Visibility"/>。当 ConverterParameter 为
/// "Inverse" 时反转：true -> Collapsed，false -> Visible。
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        if (parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }
        return visible;
    }
}
