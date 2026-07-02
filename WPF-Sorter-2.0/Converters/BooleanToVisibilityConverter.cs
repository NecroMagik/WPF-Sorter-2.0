using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPF_Sorter_2._0.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Защита от null, DBNull и пустых строк
        if (value == null || value == DBNull.Value)
            return Visibility.Collapsed;

        // Если значение уже булево
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;

        // Если значение строка
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
                return Visibility.Collapsed;

            if (bool.TryParse(stringValue, out bool result))
                return result ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility == Visibility.Visible;
        return false;
    }
}