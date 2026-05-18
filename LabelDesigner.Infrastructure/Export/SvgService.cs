using System.Globalization;
using System.Text;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Infrastructure.Export;

public sealed class SvgService : ISvgService
{
    public string ToSvgPath(ShapeElement shape)
    {
        var b = shape.Bounds;
        return shape.Type switch
        {
            ShapeType.Ellipse => $"M {F(b.CenterX)} {F(b.Y)} A {F(b.Width / 2)} {F(b.Height / 2)} 0 1 0 {F(b.CenterX)} {F(b.Y + b.Height)} A {F(b.Width / 2)} {F(b.Height / 2)} 0 1 0 {F(b.CenterX)} {F(b.Y)}",
            ShapeType.Triangle => $"M {F(b.CenterX)} {F(b.Y)} L {F(b.X)} {F(b.Y + b.Height)} L {F(b.X + b.Width)} {F(b.Y + b.Height)} Z",
            _ => $"M {F(b.X)} {F(b.Y)} H {F(b.X + b.Width)} V {F(b.Y + b.Height)} H {F(b.X)} Z"
        };
    }

    public string ExportToSvg(SceneDocument document)
    {
        var width = document.Page.WidthMm.ToString(CultureInfo.InvariantCulture);
        var height = document.Page.HeightMm.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\">");
        sb.AppendLine("  <!-- SVG export stub: full element rendering will be implemented in a later phase. -->");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
