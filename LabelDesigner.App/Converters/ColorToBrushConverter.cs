using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace LabelDesigner.App.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Windows.UI.Color color)
            return new SolidColorBrush(color);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is SolidColorBrush brush)
            return brush.Color;
        return Windows.UI.Color.FromArgb(255, 200, 200, 200);
    }
}
