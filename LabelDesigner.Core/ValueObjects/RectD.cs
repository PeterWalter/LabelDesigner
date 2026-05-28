using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabelDesigner.Core.ValueObjects;

[JsonConverter(typeof(RectDJsonConverter))]
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

internal sealed class RectDJsonConverter : JsonConverter<RectD>
{
    public override RectD Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("RectD must be a JSON object");

        double x = 0;
        double y = 0;
        double width = 0;
        double height = 0;
        double? left = null;
        double? top = null;
        double? right = null;
        double? bottom = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Invalid RectD JSON");

            var name = reader.GetString();
            reader.Read();

            switch (name)
            {
                case "x":
                    x = reader.GetDouble();
                    break;
                case "y":
                    y = reader.GetDouble();
                    break;
                case "width":
                    width = reader.GetDouble();
                    break;
                case "height":
                    height = reader.GetDouble();
                    break;
                case "left":
                    left = reader.GetDouble();
                    break;
                case "top":
                    top = reader.GetDouble();
                    break;
                case "right":
                    right = reader.GetDouble();
                    break;
                case "bottom":
                    bottom = reader.GetDouble();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (left.HasValue || top.HasValue || right.HasValue || bottom.HasValue)
        {
            x = left ?? x;
            y = top ?? y;
            if (right.HasValue)
                width = right.Value - x;
            if (bottom.HasValue)
                height = bottom.Value - y;
        }

        return new RectD(x, y, width, height);
    }

    public override void Write(Utf8JsonWriter writer, RectD value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("width", value.Width);
        writer.WriteNumber("height", value.Height);
        writer.WriteEndObject();
    }
}
