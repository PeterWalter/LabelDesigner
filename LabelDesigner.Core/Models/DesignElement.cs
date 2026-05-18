using System.Numerics;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public abstract class DesignElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public RectD Bounds { get; set; }
    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
    public Dictionary<string, string> Metadata { get; } = new();
    public Guid? ParentId { get; set; }
    public int ZIndex { get; set; }

    public Matrix3x2 GetLocalTransform()
    {
        float cx = (float)(Bounds.X + Bounds.Width / 2);
        float cy = (float)(Bounds.Y + Bounds.Height / 2);
        float radians = (float)(Rotation * Math.PI / 180.0);

        return
            Matrix3x2.CreateTranslation((float)Bounds.X, (float)Bounds.Y) *
            Matrix3x2.CreateTranslation(cx - (float)Bounds.X, cy - (float)Bounds.Y) *
            Matrix3x2.CreateRotation(radians) *
            Matrix3x2.CreateScale((float)ScaleX, (float)ScaleY) *
            Matrix3x2.CreateTranslation(-cx + (float)Bounds.X, -cy + (float)Bounds.Y);
    }

    public bool HitTest(PointD worldPoint)
    {
        var mat = GetLocalTransform();
        Matrix3x2.Invert(mat, out var inv);
        if (!Matrix3x2.Invert(mat, out inv)) return false;
        var local = Vector2.Transform(new Vector2((float)worldPoint.X, (float)worldPoint.Y), inv);
        return local.X >= 0 && local.X <= (float)Bounds.Width &&
               local.Y >= 0 && local.Y <= (float)Bounds.Height;
    }

    public bool HitTestMargin(PointD worldPoint, double margin)
    {
        var mat = GetLocalTransform();
        if (!Matrix3x2.Invert(mat, out var inv)) return false;
        var local = Vector2.Transform(new Vector2((float)worldPoint.X, (float)worldPoint.Y), inv);
        return local.X >= -(float)margin && local.X <= (float)(Bounds.Width + margin) &&
               local.Y >= -(float)margin && local.Y <= (float)(Bounds.Height + margin);
    }
}
