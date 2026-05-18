using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.App.ViewModels;

public partial class PropertiesViewModel : ObservableObject
{
    private readonly IUndoRedoService _undoRedo;
    private DesignElement? _trackedElement;
    private bool _isTrackingUpdate;

    public Action? RequestRedraw { get; set; }

    public PropertiesViewModel(IUndoRedoService undoRedo)
    {
        _undoRedo = undoRedo;
    }

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
    private string _fontFamily = "Segoe UI";

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private bool _isUnderline;

    [ObservableProperty]
    private int _textAlignmentIndex; // 0=Left, 1=Center, 2=Right

    [ObservableProperty]
    private bool _isMultiline;

    [ObservableProperty]
    private string _foreColor = "#000000";

    [ObservableProperty]
    private string _barcodeValue = "";

    [ObservableProperty]
    private string _barcodeTextFontFamily = "Segoe UI";

    [ObservableProperty]
    private double _barcodeTextFontSize = 12;

    [ObservableProperty]
    private string _barcodeTextColor = "#000000";

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
        _isTrackingUpdate = true;
        _trackedElement = el;
        if (el == null)
        {
            ElementType = "";
            _isTrackingUpdate = false;
            return;
        }

        ElementType = el.GetType().Name.Replace("Element", "");
        ElementName = el.Name;
        PosX = el.Bounds.X;
        PosY = el.Bounds.Y;
        Width = el.Bounds.Width;
        Height = el.Bounds.Height;
        Rotation = el.Rotation;

        if (el is TextElement txt)
        {
            Text = txt.Text;
            FontSize = txt.FontSize;
            FontFamily = txt.FontFamily;
            IsBold = txt.Bold;
            IsItalic = txt.Italic;
            IsUnderline = txt.Underline;
            TextAlignmentIndex = (int)txt.TextAlignment;
            IsMultiline = txt.IsMultiline;
            ForeColor = txt.ForeColor;
        }
        if (el is BarcodeElement bc)
        {
            BarcodeValue = bc.Value;
            BarcodeTextFontFamily = bc.TextFontFamily;
            BarcodeTextFontSize = bc.TextFontSize;
            BarcodeTextColor = bc.TextColor;
        }
        if (el is ShapeElement sh) { FillColor = sh.Fill; StrokeColor = sh.Stroke; }
        if (el is LineElement ln) { StrokeColor = ln.Stroke; }
        _isTrackingUpdate = false;
    }

    partial void OnElementNameChanged(string value)
        => ApplyPropertyChange(e => e.Name, (e, v) => e.Name = v, value, "Rename element");

    partial void OnPosXChanged(double value)
        => ApplyPropertyChange(e => e.Bounds.X, (e, v) => e.Bounds = new RectD(v, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height), value, "Move element X");

    partial void OnPosYChanged(double value)
        => ApplyPropertyChange(e => e.Bounds.Y, (e, v) => e.Bounds = new RectD(e.Bounds.X, v, e.Bounds.Width, e.Bounds.Height), value, "Move element Y");

    partial void OnWidthChanged(double value)
        => ApplyPropertyChange(e => e.Bounds.Width, (e, v) => e.Bounds = new RectD(e.Bounds.X, e.Bounds.Y, v, e.Bounds.Height), value, "Resize element width");

    partial void OnHeightChanged(double value)
        => ApplyPropertyChange(e => e.Bounds.Height, (e, v) => e.Bounds = new RectD(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, v), value, "Resize element height");

    partial void OnRotationChanged(double value)
        => ApplyPropertyChange(e => e.Rotation, (e, v) => e.Rotation = v, value, "Rotate element");

    partial void OnTextChanged(string value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
        {
            ApplyPropertyChange(_ => txt.Text, (_, v) => txt.Text = v, value, "Edit text");
        }
    }

    partial void OnFontSizeChanged(double value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
        {
            ApplyPropertyChange(_ => txt.FontSize, (_, v) => txt.FontSize = v, value, "Change text size");
        }
    }

    partial void OnFontFamilyChanged(string value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.FontFamily, (_, v) => txt.FontFamily = v, value, "Change font family");
    }

    partial void OnIsBoldChanged(bool value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.Bold, (_, v) => txt.Bold = v, value, "Toggle bold");
    }

    partial void OnIsItalicChanged(bool value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.Italic, (_, v) => txt.Italic = v, value, "Toggle italic");
    }

    partial void OnIsUnderlineChanged(bool value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.Underline, (_, v) => txt.Underline = v, value, "Toggle underline");
    }

    partial void OnTextAlignmentIndexChanged(int value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => (int)txt.TextAlignment, (_, v) => txt.TextAlignment = (LabelDesigner.Core.Models.TextAlignmentType)v, value, "Change text alignment");
    }

    partial void OnIsMultilineChanged(bool value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.IsMultiline, (_, v) => txt.IsMultiline = v, value, "Toggle multiline");
    }

    partial void OnForeColorChanged(string value)
    {
        if (_trackedElement is TextElement txt && !_isTrackingUpdate)
            ApplyPropertyChange(_ => txt.ForeColor, (_, v) => txt.ForeColor = v, value, "Change text color");
    }

    partial void OnBarcodeValueChanged(string value)
    {
        if (_trackedElement is BarcodeElement bc && !_isTrackingUpdate)
        {
            ApplyPropertyChange(_ => bc.Value, (_, v) => bc.Value = v, value, "Edit barcode value");
        }
    }

    partial void OnBarcodeTextFontFamilyChanged(string value)
    {
        if (_trackedElement is BarcodeElement bc && !_isTrackingUpdate)
            ApplyPropertyChange(_ => bc.TextFontFamily, (_, v) => bc.TextFontFamily = v, value, "Change barcode text font");
    }

    partial void OnBarcodeTextFontSizeChanged(double value)
    {
        if (_trackedElement is BarcodeElement bc && !_isTrackingUpdate)
            ApplyPropertyChange(_ => bc.TextFontSize, (_, v) => bc.TextFontSize = v, value, "Change barcode text size");
    }

    partial void OnBarcodeTextColorChanged(string value)
    {
        if (_trackedElement is BarcodeElement bc && !_isTrackingUpdate)
            ApplyPropertyChange(_ => bc.TextColor, (_, v) => bc.TextColor = v, value, "Change barcode text color");
    }

    partial void OnFillColorChanged(string value)
    {
        if (_trackedElement is ShapeElement sh && !_isTrackingUpdate)
        {
            ApplyPropertyChange(_ => sh.Fill, (_, v) => sh.Fill = v, value, "Change fill color");
        }
    }

    partial void OnStrokeColorChanged(string value)
    {
        if (_isTrackingUpdate) return;

        if (_trackedElement is ShapeElement sh)
        {
            ApplyPropertyChange(_ => sh.Stroke, (_, v) => sh.Stroke = v, value, "Change stroke color");
        }
        else if (_trackedElement is LineElement ln)
        {
            ApplyPropertyChange(_ => ln.Stroke, (_, v) => ln.Stroke = v, value, "Change stroke color");
        }
    }

    private void ApplyPropertyChange<T>(
        Func<DesignElement, T> getCurrent,
        Action<DesignElement, T> setValue,
        T newValue,
        string description)
    {
        if (_trackedElement == null || _isTrackingUpdate)
        {
            return;
        }

        var oldValue = getCurrent(_trackedElement);
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        _undoRedo.Execute(new PropertyEditCommand<T>(
            _trackedElement,
            setValue,
            oldValue,
            newValue,
            description,
            RequestRedraw));
    }

    private sealed class PropertyEditCommand<T> : IUndoableCommand
    {
        private readonly DesignElement _element;
        private readonly Action<DesignElement, T> _setValue;
        private readonly T _oldValue;
        private readonly T _newValue;
        private readonly Action? _onChanged;

        public PropertyEditCommand(
            DesignElement element,
            Action<DesignElement, T> setValue,
            T oldValue,
            T newValue,
            string description,
            Action? onChanged)
        {
            _element = element;
            _setValue = setValue;
            _oldValue = oldValue;
            _newValue = newValue;
            Description = description;
            _onChanged = onChanged;
        }

        public string Description { get; }

        public void Execute()
        {
            _setValue(_element, _newValue);
            _onChanged?.Invoke();
        }

        public void Undo()
        {
            _setValue(_element, _oldValue);
            _onChanged?.Invoke();
        }
    }
}
