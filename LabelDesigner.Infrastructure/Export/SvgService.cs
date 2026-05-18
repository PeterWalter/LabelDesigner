using System.Globalization;
using System.Text;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using SkiaSharp;
using Svg.Skia;

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

    public byte[] RasterizeToPng(string svgFilePath, int pixelWidth, int pixelHeight)
    {
        if (string.IsNullOrWhiteSpace(svgFilePath))
            throw new ArgumentException("SVG file path is required.", nameof(svgFilePath));
        if (!File.Exists(svgFilePath))
            throw new FileNotFoundException("SVG file was not found.", svgFilePath);
        if (pixelWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Width must be > 0.");
        if (pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Height must be > 0.");

        var svg = new SKSvg();
        var picture = svg.Load(svgFilePath);
        if (picture == null)
            throw new InvalidOperationException($"Failed to parse SVG file '{svgFilePath}'.");

        var sourceRect = picture.CullRect;
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
            throw new InvalidOperationException($"SVG file '{svgFilePath}' has no drawable bounds.");

        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scale = Math.Min(pixelWidth / sourceRect.Width, pixelHeight / sourceRect.Height);
        var tx = (pixelWidth - sourceRect.Width * scale) / 2f - sourceRect.Left * scale;
        var ty = (pixelHeight - sourceRect.Height * scale) / 2f - sourceRect.Top * scale;

        canvas.Translate(tx, ty);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data == null)
            throw new InvalidOperationException($"Failed to encode rasterized SVG '{svgFilePath}' to PNG.");

        return data.ToArray();
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
