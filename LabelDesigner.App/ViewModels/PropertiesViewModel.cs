using CommunityToolkit.Mvvm.ComponentModel;
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
}
