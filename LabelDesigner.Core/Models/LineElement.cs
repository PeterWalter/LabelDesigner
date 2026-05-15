namespace LabelDesigner.Core.Models;

public class LineElement : DesignElement
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; } = 100;
    public double Y2 { get; set; } = 0;
    public string Stroke { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1;
}
