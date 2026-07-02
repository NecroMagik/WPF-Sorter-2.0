using System.Globalization;
using System.Windows.Data;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Converters;

public class ExtensionsDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileCategory category)
        {
            var extensions = string.Join(" ", category.Extensions.Take(6));
            return category.Extensions.Count > 6 ? extensions + " …" : extensions;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}