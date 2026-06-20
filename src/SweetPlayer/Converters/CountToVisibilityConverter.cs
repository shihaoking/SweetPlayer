using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SweetPlayer.Converters;

/// <summary>
/// 将集合数量转换为 <see cref="Visibility"/>。
/// 当数量为 0 时显示，否则隐藏（可通过 Inverse 参数反转）。
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value switch
        {
            int i => i,
            long l => l,
            _ => 1 // 默认认为非空
        };

        var isEmpty = count == 0;

        if (parameter is string p && string.Equals(p, "Inverse", StringComparison.OrdinalIgnoreCase))
        {
            isEmpty = !isEmpty;
        }

        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
