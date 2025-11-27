using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PulseLink.Converters;

public class InvertBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return booleanValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible; // Default to visible if value is not a boolean
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // One-way conversion, not implemented
        throw new NotImplementedException();
    }
}
