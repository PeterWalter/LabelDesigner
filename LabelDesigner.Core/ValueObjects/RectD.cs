namespace LabelDesigner.Core.ValueObjects;

public struct RectD
{
    public double X;
    public double Y;
    public double Width;
    public double Height;

    public RectD(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public readonly double Left => X;
    public readonly double Top => Y;
    public readonly double Right => X + Width;
    public readonly double Bottom => Y + Height;
    public readonly double CenterX => X + (Width / 2);
    public readonly double CenterY => Y + (Height / 2);

    public RectD Translate(double dx, double dy)
    {
        return new RectD(X + dx, Y + dy, Width, Height);
    }

    public readonly bool Contains(PointD point)
    {
        return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
    }

    public readonly bool Intersects(RectD other)
    {
        return Right >= other.Left &&
               Left <= other.Right &&
               Bottom >= other.Top &&
               Top <= other.Bottom;
    }

    public RectD SnapOriginToGrid(double gridSize)
    {
        if (gridSize <= 0)
        {
            return this;
        }

        return new RectD(
            Math.Round(X / gridSize) * gridSize,
            Math.Round(Y / gridSize) * gridSize,
            Width,
            Height);
    }

    public RectD EnsureMinimumSize(double minimumWidth, double minimumHeight)
    {
        return new RectD(
            X,
            Y,
            Math.Max(Width, minimumWidth),
            Math.Max(Height, minimumHeight));
    }

    public RectD ClampToBounds(RectD bounds)
    {
        var clampedWidth = Math.Min(Width, bounds.Width);
        var clampedHeight = Math.Min(Height, bounds.Height);
        var clampedX = Math.Clamp(X, bounds.X, bounds.Right - clampedWidth);
        var clampedY = Math.Clamp(Y, bounds.Y, bounds.Bottom - clampedHeight);

        return new RectD(clampedX, clampedY, clampedWidth, clampedHeight);
    }
}
