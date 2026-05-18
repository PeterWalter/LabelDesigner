using LabelDesigner.Core.Enums;

namespace LabelDesigner.Core.Models;

public class DocumentDefaults
{
    public BarcodeTextPosition BarcodeTextPosition { get; set; } = BarcodeTextPosition.Bottom;
    public string BarcodeTextFontFamily { get; set; } = "Segoe UI";
    public double BarcodeTextFontSize { get; set; } = 12;
    public string BarcodeTextColor { get; set; } = "#000000";

    public string TextFontFamily { get; set; } = "Segoe UI";
    public double TextFontSize { get; set; } = 14;
    public string TextColor { get; set; } = "#000000";
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public bool TextUnderline { get; set; }
    public TextAlignmentType TextAlignment { get; set; } = TextAlignmentType.Left;
    public bool TextMultiline { get; set; }
    public double TextLineSpacing { get; set; }
}
