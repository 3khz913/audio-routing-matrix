using System;
using System.Globalization;
using System.Windows.Data;

namespace mixer.Views
{
    public class VuHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var pct = value is double d ? d : 0;
            return Math.Max(1, pct / 100.0 * 16.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
