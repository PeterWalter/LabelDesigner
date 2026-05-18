using System;
using System.Collections.Generic;
using System.Text;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public enum TextAlignmentType
{
    Left,
    Center,
    Right
}

public class TextElement : DesignElement
{
    public string Text { get; set; } = "Sample";
    public double FontSize { get; set; } = 14;
    public string FontFamily { get; set; } = "Segoe UI";
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;
    public bool Underline { get; set; } = false;
    public TextAlignmentType TextAlignment { get; set; } = TextAlignmentType.Left;
    public bool IsMultiline { get; set; } = false;
    public double LineSpacing { get; set; } = 0;
    public string ForeColor { get; set; } = "#000000";
}