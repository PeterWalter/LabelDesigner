using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace LabelDesigner.App.Converters;

/// <summary>
/// Returns one of two strings based on a bool.
/// ConverterParameter format: "TrueValue|FalseValue"
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool bv && bv;
        string param = parameter as string ?? "|";
        var parts = param.Split('|');
        return b ? (parts.Length > 0 ? parts[0] : "") : (parts.Length > 1 ? parts[1] : "");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
