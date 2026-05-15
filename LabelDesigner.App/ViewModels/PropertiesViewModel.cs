using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.App.ViewModels;

public partial class PropertiesViewModel : ObservableObject
{
    private DesignElement? _trackedElement;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private string _elementType = "";

    [ObservableProperty]
    private string _elementName = "";

    [ObservableProperty]
    private double _posX;

    [ObservableProperty]
    private double _posY;

    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private double _rotation;

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private double _fontSize = 14;

    [ObservableProperty]
    private string _barcodeValue = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FillColorValue))]
    private string _fillColor = "#CCCCCC";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StrokeColorValue))]
    private string _strokeColor = "#000000";

    public Windows.UI.Color FillColorValue => ParseColor(FillColor);
    public Windows.UI.Color StrokeColorValue => ParseColor(StrokeColor);

    public List<Windows.UI.Color> PaletteColors { get; } = new()
    {
        Windows.UI.Color.FromArgb(255, 0, 0, 0),
        Windows.UI.Color.FromArgb(255, 128, 128, 128),
        Windows.UI.Color.FromArgb(255, 192, 192, 192),
        Windows.UI.Color.FromArgb(255, 255, 255, 255),
        Windows.UI.Color.FromArgb(255, 128, 0, 0),
        Windows.UI.Color.FromArgb(255, 255, 0, 0),
        Windows.UI.Color.FromArgb(255, 255, 128, 128),
        Windows.UI.Color.FromArgb(255, 255, 192, 192),
        Windows.UI.Color.FromArgb(255, 128, 128, 0),
        Windows.UI.Color.FromArgb(255, 255, 255, 0),
        Windows.UI.Color.FromArgb(255, 128, 255, 0),
        Windows.UI.Color.FromArgb(255, 192, 255, 128),
        Windows.UI.Color.FromArgb(255, 0, 128, 0),
        Windows.UI.Color.FromArgb(255, 0, 255, 0),
        Windows.UI.Color.FromArgb(255, 128, 255, 128),
        Windows.UI.Color.FromArgb(255, 192, 255, 192),
        Windows.UI.Color.FromArgb(255, 0, 128, 128),
        Windows.UI.Color.FromArgb(255, 0, 255, 255),
        Windows.UI.Color.FromArgb(255, 128, 255, 255),
        Windows.UI.Color.FromArgb(255, 192, 255, 255),
        Windows.UI.Color.FromArgb(255, 0, 0, 128),
        Windows.UI.Color.FromArgb(255, 0, 0, 255),
        Windows.UI.Color.FromArgb(255, 128, 128, 255),
        Windows.UI.Color.FromArgb(255, 192, 192, 255),
        Windows.UI.Color.FromArgb(255, 128, 0, 128),
        Windows.UI.Color.FromArgb(255, 255, 0, 255),
        Windows.UI.Color.FromArgb(255, 255, 128, 255),
        Windows.UI.Color.FromArgb(255, 255, 192, 255),
        Windows.UI.Color.FromArgb(255, 0, 128, 192),
        Windows.UI.Color.FromArgb(255, 64, 128, 128),
        Windows.UI.Color.FromArgb(255, 128, 64, 64),
        Windows.UI.Color.FromArgb(255, 64, 64, 128),
    };

    private static Windows.UI.Color ParseColor(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 6) return Windows.UI.Color.FromArgb(255, 200, 200, 200);
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }
        if (hex.Length == 8)
        {
            byte a = Convert.ToByte(hex.Substring(0, 2), 16);
            byte r = Convert.ToByte(hex.Substring(2, 2), 16);
            byte g = Convert.ToByte(hex.Substring(4, 2), 16);
            byte b = Convert.ToByte(hex.Substring(6, 2), 16);
            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
        return Windows.UI.Color.FromArgb(255, 200, 200, 200);
    }

    [RelayCommand]
    private void SelectColor(Windows.UI.Color color)
    {
        if (_trackedElement is ShapeElement sh) sh.Fill = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (_trackedElement is ShapeElement sh2) sh2.Stroke = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (_trackedElement is LineElement ln) ln.Stroke = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        FillColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        StrokeColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public bool IsVisible => !string.IsNullOrEmpty(ElementType);

    public void TrackElement(DesignElement? el)
    {
        _trackedElement = el;
        if (el == null) { ElementType = ""; return; }

        ElementType = el.GetType().Name.Replace("Element", "");
        ElementName = el.Name;
        PosX = el.Bounds.X;
        PosY = el.Bounds.Y;
        Width = el.Bounds.Width;
        Height = el.Bounds.Height;
        Rotation = el.Rotation;

        if (el is TextElement txt) { Text = txt.Text; FontSize = txt.FontSize; }
        if (el is BarcodeElement bc) { BarcodeValue = bc.Value; }
        if (el is ShapeElement sh) { FillColor = sh.Fill; StrokeColor = sh.Stroke; }
        if (el is LineElement ln) { StrokeColor = ln.Stroke; }
    }

    partial void OnElementNameChanged(string value) { if (_trackedElement != null) _trackedElement.Name = value; }
    partial void OnPosXChanged(double value) { if (_trackedElement != null) _trackedElement.Bounds = new RectD(value, _trackedElement.Bounds.Y, _trackedElement.Bounds.Width, _trackedElement.Bounds.Height); }
    partial void OnPosYChanged(double value) { if (_trackedElement != null) _trackedElement.Bounds = new RectD(_trackedElement.Bounds.X, value, _trackedElement.Bounds.Width, _trackedElement.Bounds.Height); }
    partial void OnWidthChanged(double value) { if (_trackedElement != null) _trackedElement.Bounds = new RectD(_trackedElement.Bounds.X, _trackedElement.Bounds.Y, value, _trackedElement.Bounds.Height); }
    partial void OnHeightChanged(double value) { if (_trackedElement != null) _trackedElement.Bounds = new RectD(_trackedElement.Bounds.X, _trackedElement.Bounds.Y, _trackedElement.Bounds.Width, value); }
    partial void OnRotationChanged(double value) { if (_trackedElement != null) _trackedElement.Rotation = value; }
    partial void OnTextChanged(string value) { if (_trackedElement is TextElement txt) txt.Text = value; }
    partial void OnFontSizeChanged(double value) { if (_trackedElement is TextElement txt) txt.FontSize = value; }
    partial void OnBarcodeValueChanged(string value) { if (_trackedElement is BarcodeElement bc) bc.Value = value; }
    partial void OnFillColorChanged(string value) { if (_trackedElement is ShapeElement sh) sh.Fill = value; }
    partial void OnStrokeColorChanged(string value) {
        if (_trackedElement is ShapeElement sh) sh.Stroke = value;
        if (_trackedElement is LineElement ln) ln.Stroke = value;
    }
}
