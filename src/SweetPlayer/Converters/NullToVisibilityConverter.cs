using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SweetPlayer.Converters;

/// <summary>
/// 当值为 null 或空字符串时返回 Collapsed，否则 Visible。ConverterParameter="Inverse" 反转结果。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var hasValue = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true,
        };
        if (parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
