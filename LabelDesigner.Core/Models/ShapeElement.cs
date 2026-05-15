using LabelDesigner.Core.Enums;

namespace LabelDesigner.Core.Models;

public class ShapeElement : DesignElement
{
    public ShapeType Type { get; set; }
    public string? PathData { get; set; }
    public string Fill { get; set; } = "#000000";
    public string Stroke { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1;
}
