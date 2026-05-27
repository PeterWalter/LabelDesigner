using LabelDesigner.Core.Enums;

namespace LabelDesigner.Core.Models;

public class BarcodeElement : DesignElement
{
    public string Value { get; set; } = "0123456789";

    public BarcodeSymbology Symbology { get; set; } = BarcodeSymbology.Code128;

    public BarcodeTextPosition TextPosition { get; set; } = BarcodeTextPosition.Bottom;

    public string DisplayText => Value;

    public string TextFontFamily { get; set; } = "Segoe UI";
    public double TextFontSize { get; set; } = 12;
    public bool TextBold { get; set; } = false;
    public bool TextItalic { get; set; } = false;
    public string TextColor { get; set; } = "#000000";
}
