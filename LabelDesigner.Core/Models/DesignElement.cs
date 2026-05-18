using System.Numerics;
using LabelDesigner.Core.ValueObjects;
using System.Text.Json.Serialization;

namespace LabelDesigner.Core.Models;

public abstract class DesignElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public RectD Bounds { get; set; }
    [JsonIgnore]
    public Vector2 Position
    {
        get => new((float)Bounds.X, (float)Bounds.Y);
        set => Bounds = new RectD(value.X, value.Y, Bounds.Width, Bounds.Height);
    }

    [JsonIgnore]
    public Vector2 Size
    {
        get => new((float)Bounds.Width, (float)Bounds.Height);
        set => Bounds = new RectD(Bounds.X, Bounds.Y, value.X, value.Y);
    }

    public double Rotation { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
    [JsonIgnore]
    public bool IsSelected { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public Guid? ParentId { get; set; }
    public int ZIndex { get; set; }

    [JsonIgnore]
    public RectD BoundingBox => Bounds;

    [JsonIgnore]
    public Matrix3x2 Transform => GetLocalTransform();

    [JsonIgnore]
    public Matrix3x2 InverseTransform
    {
        get
        {
            var transform = Transform;
            return Matrix3x2.Invert(transform, out var inverse) ? inverse : Matrix3x2.Identity;
        }
    }

    public Matrix3x2 GetLocalTransform()
    {
        float w = Math.Max((float)Bounds.Width, 1f);
        float h = Math.Max((float)Bounds.Height, 1f);
        float cx = (float)(Bounds.X + w / 2);
        float cy = (float)(Bounds.Y + h / 2);
        float radians = (float)(Rotation * Math.PI / 180.0);
        float sx = (float)(Math.Abs(ScaleX) < 0.001 ? 0.001 : ScaleX);
        float sy = (float)(Math.Abs(ScaleY) < 0.001 ? 0.001 : ScaleY);

        return
            Matrix3x2.CreateTranslation(cx, cy) *
            Matrix3x2.CreateRotation(radians) *
            Matrix3x2.CreateScale(sx, sy) *
            Matrix3x2.CreateTranslation(-w / 2f, -h / 2f);
    }

    public virtual bool HitTest(PointD worldPoint)
    {
        var local = WorldToObjectLocal(worldPoint);
        return local.X >= 0 && local.X <= (float)Bounds.Width &&
               local.Y >= 0 && local.Y <= (float)Bounds.Height;
    }

    public virtual bool HitTestMargin(PointD worldPoint, double margin)
    {
        var local = WorldToObjectLocal(worldPoint);
        return local.X >= -(float)margin && local.X <= (float)(Bounds.Width + margin) &&
               local.Y >= -(float)margin && local.Y <= (float)(Bounds.Height + margin);
    }

    public PointD WorldToObjectLocal(PointD worldPoint)
    {
        var local = Vector2.Transform(new Vector2((float)worldPoint.X, (float)worldPoint.Y), InverseTransform);
        return new PointD(local.X, local.Y);
    }
}
