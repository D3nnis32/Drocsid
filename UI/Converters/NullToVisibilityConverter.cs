using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UI.Converters
{
    // Converts a null value to Visible and a non-null value to Collapsed
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}