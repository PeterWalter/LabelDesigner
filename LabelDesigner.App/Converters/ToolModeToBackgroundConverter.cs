using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using LabelDesigner.Core.Enums;

namespace LabelDesigner.App.Converters;

public class ToolModeToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ToolMode activeTool || parameter is not string paramStr)
            return new SolidColorBrush(Colors.Transparent);

        if (!Enum.TryParse<ToolMode>(paramStr, out var expectedTool))
            return new SolidColorBrush(Colors.Transparent);

        // Active tool gets a slightly darker background
        return activeTool == expectedTool
            ? new SolidColorBrush(Color.FromArgb(100, 0, 120, 215))  // Light blue highlight
            : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
