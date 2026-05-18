using System.Numerics;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public class LineElement : DesignElement
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; } = 100;
    public double Y2 { get; set; } = 0;
    public string Stroke { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1;

    public override bool HitTest(PointD worldPoint) => HitTestMargin(worldPoint, Math.Max(4, StrokeWidth + 2));

    public override bool HitTestMargin(PointD worldPoint, double margin)
    {
        var mat = GetLocalTransform();
        if (!Matrix3x2.Invert(mat, out var inv)) return false;

        var local = Vector2.Transform(new Vector2((float)worldPoint.X, (float)worldPoint.Y), inv);
        var dist = DistanceToSegment(local.X, local.Y, (float)X1, (float)Y1, (float)X2, (float)Y2);
        return dist <= (float)margin;
    }

    private static float DistanceToSegment(float px, float py, float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
            return MathF.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        var t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0f, 1f);
        var nearestX = x1 + t * dx;
        var nearestY = y1 + t * dy;
        return MathF.Sqrt((px - nearestX) * (px - nearestX) + (py - nearestY) * (py - nearestY));
    }
}
