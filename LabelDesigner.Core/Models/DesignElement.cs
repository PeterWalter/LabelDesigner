using System.Numerics;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public abstract class DesignElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public RectD Bounds { get; set; }
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1.0;
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
    public Dictionary<string, string> Metadata { get; } = new();
    public Guid? ParentId { get; set; }
    public int ZIndex { get; set; }

    public Matrix3x2 GetLocalTransform()
    {
        var centerX = Bounds.X + Bounds.Width / 2;
        var centerY = Bounds.Y + Bounds.Height / 2;
        var radians = Rotation * Math.PI / 180.0;
        return Matrix3x2.CreateRotation((float)radians, new Vector2((float)centerX, (float)centerY));
    }
}
