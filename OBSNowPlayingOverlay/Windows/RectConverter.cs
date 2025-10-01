using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OBSNowPlayingOverlay.Windows
{
    public class RectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is double width &&
                values[1] is double height)
            {
                return new Rect(0, 0, width, height);
            }

            return Rect.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is Rect rect)
            {
                var values = new object[2];
                values[0] = rect.Width;
                values[1] = rect.Height;

                return values;
            }
            else
            {
                return Array.Empty<object>();
            }
        }
    }
}