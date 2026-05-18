using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LabelDesigner.App.Converters;

/// <summary>
/// Returns one of two SolidColorBrushes based on a bool value.
/// Parameter format: "TrueHex|FalseHex"  e.g. "#1A73E8|#AAAAAA"
/// </summary>
public class BoolToSolidBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool bv && bv;
        string param = parameter as string ?? "#000000|#AAAAAA";
        var parts = param.Split('|');
        string hex = b ? (parts.Length > 0 ? parts[0] : "#000000")
                       : (parts.Length > 1 ? parts[1] : "#AAAAAA");
        return new SolidColorBrush(ParseHex(hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        try
        {
            return hex.Length switch
            {
                6 => Color.FromArgb(255,
                    System.Convert.ToByte(hex[..2], 16),
                    System.Convert.ToByte(hex[2..4], 16),
                    System.Convert.ToByte(hex[4..6], 16)),
                _ => Color.FromArgb(255, 170, 170, 170)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse hex color '{hex}': {ex.Message}");
            return Color.FromArgb(255, 170, 170, 170);
        }
    }
}
