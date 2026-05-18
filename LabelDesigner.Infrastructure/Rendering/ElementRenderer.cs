using System.Numerics;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace LabelDesigner.Infrastructure.Rendering;

/// <summary>
/// Shared Win2D element rendering logic used by both the canvas preview and
/// high-resolution print/rasterization paths. The <paramref name="scale"/>
/// parameter compensates for font size and stroke width when DPI differs from 96.
/// </summary>
internal static class ElementRenderer
{
    private static readonly Dictionary<string, Windows.Graphics.Imaging.SoftwareBitmap?> _barcodeCache = new();

    private static Windows.Graphics.Imaging.SoftwareBitmap? GetCachedBarcode(
        IBarcodeService barcode, string value, int width, int height)
    {
        var key = $"{value}|{width}|{height}";
        if (!_barcodeCache.TryGetValue(key, out var cached))
        {
            cached = barcode.Generate(value, ZXing.BarcodeFormat.CODE_128, width, height);
            _barcodeCache[key] = cached;
        }
        return cached;
    }

    internal static void DrawElement(
        CanvasDrawingSession ds,
        DesignElement el,
        Dictionary<Guid, DesignElement> lookup,
        IBarcodeService barcode,
        float scale = 1f)
    {
        if (el is BarcodeElement b) DrawBarcode(ds, b, barcode, scale);
        else if (el is TextElement txt) DrawText(ds, txt, scale);
        else if (el is ShapeElement shape) DrawShape(ds, shape, scale);
        else if (el is LineElement line) DrawLine(ds, line, scale);
        else if (el is ImageElement image) DrawImage(ds, image);
        else if (el is ContainerElement container)
        {
            foreach (var childId in container.ChildIds)
            {
                if (lookup.TryGetValue(childId, out var child) && child.Visible)
                    DrawElement(ds, child, lookup, barcode, scale);
            }
        }
    }

    private static void DrawBarcode(
        CanvasDrawingSession ds, BarcodeElement b, IBarcodeService barcode, float scale)
    {
        var bounds = b.Bounds;
        float padding = 6 * scale, textHeight = 18 * scale;
        RectD barcodeRect = bounds;
        float tx = 0, ty = 0;
        string text = b.DisplayText;

        switch (b.TextPosition)
        {
            case BarcodeTextPosition.Top:
                barcodeRect = new(bounds.X, bounds.Y + textHeight + padding,
                    bounds.Width, bounds.Height - textHeight - padding);
                tx = (float)(bounds.X + bounds.Width / 2); ty = (float)bounds.Y;
                break;
            case BarcodeTextPosition.Bottom:
                barcodeRect = new(bounds.X, bounds.Y,
                    bounds.Width, bounds.Height - textHeight - padding);
                tx = (float)(bounds.X + bounds.Width / 2);
                ty = (float)(bounds.Y + bounds.Height - textHeight);
                break;
            case BarcodeTextPosition.Left:
                barcodeRect = new(bounds.X + 60 * scale, bounds.Y,
                    bounds.Width - 60 * scale, bounds.Height);
                tx = (float)bounds.X; ty = (float)(bounds.Y + bounds.Height / 2);
                break;
            case BarcodeTextPosition.Right:
                barcodeRect = new(bounds.X, bounds.Y,
                    bounds.Width - 60 * scale, bounds.Height);
                tx = (float)(bounds.X + bounds.Width - 60 * scale);
                ty = (float)(bounds.Y + bounds.Height / 2);
                break;
        }

        var bmp = GetCachedBarcode(barcode, b.Value, (int)barcodeRect.Width, (int)barcodeRect.Height);
        if (bmp != null)
        {
            var img = CanvasBitmap.CreateFromSoftwareBitmap(ds.Device, bmp);
            ds.DrawImage(img, barcodeRect.ToWinRect());
        }

        var textColor = ParseColor(b.TextColor, Colors.Black);
        ds.DrawText(text, new Vector2(tx, ty), textColor, new CanvasTextFormat
        {
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            FontSize = (float)b.TextFontSize * scale,
            FontFamily = string.IsNullOrEmpty(b.TextFontFamily) ? "Segoe UI" : b.TextFontFamily
        });
    }

    private static void DrawText(CanvasDrawingSession ds, TextElement txt, float scale)
    {
        var alignment = txt.TextAlignment switch
        {
            TextAlignmentType.Center => CanvasHorizontalAlignment.Center,
            TextAlignmentType.Right  => CanvasHorizontalAlignment.Right,
            _                        => CanvasHorizontalAlignment.Left
        };

        var fontStyle = txt.Italic
            ? Windows.UI.Text.FontStyle.Italic
            : Windows.UI.Text.FontStyle.Normal;

        var fontWeight = txt.Bold
            ? Microsoft.UI.Text.FontWeights.Bold
            : Microsoft.UI.Text.FontWeights.Normal;

        var format = new CanvasTextFormat
        {
            FontSize           = (float)txt.FontSize * scale,
            FontFamily         = string.IsNullOrEmpty(txt.FontFamily) ? "Segoe UI" : txt.FontFamily,
            FontStyle          = fontStyle,
            FontWeight         = fontWeight,
            HorizontalAlignment = alignment,
            WordWrapping       = txt.IsMultiline ? CanvasWordWrapping.Wrap : CanvasWordWrapping.NoWrap
        };

        var color = ParseColor(txt.ForeColor, Colors.Black);
        var rect = txt.Bounds.ToWinRect();

        if (txt.IsMultiline)
            ds.DrawText(txt.Text, rect, color, format);
        else
            ds.DrawText(txt.Text, (float)rect.X, (float)rect.Y, color, format);

        if (txt.Underline)
        {
            float y = (float)(txt.Bounds.Y + txt.FontSize * scale + 2);
            ds.DrawLine((float)txt.Bounds.X, y,
                (float)(txt.Bounds.X + txt.Bounds.Width), y, color, 1);
        }
    }

    private static void DrawShape(CanvasDrawingSession ds, ShapeElement shape, float scale)
    {
        var b = shape.Bounds.ToWinRect();
        var fill   = ParseColor(shape.Fill, Colors.LightGray);
        var stroke = ParseColor(shape.Stroke, Colors.Black);
        float sw = (float)shape.StrokeWidth * scale;
        var cx = new Vector2((float)(b.X + b.Width / 2), (float)(b.Y + b.Height / 2));

        if (shape.Type == ShapeType.Ellipse)
        {
            ds.FillEllipse(cx, (float)(b.Width / 2), (float)(b.Height / 2), fill);
            ds.DrawEllipse(cx, (float)(b.Width / 2), (float)(b.Height / 2), stroke, sw);
        }
        else if (shape.Type == ShapeType.Triangle)
        {
            float xL = (float)b.X, xR = (float)(b.X + b.Width), xM = (float)(b.X + b.Width / 2);
            float yT = (float)b.Y, yB = (float)(b.Y + b.Height);
            var tri = new[] { new Vector2(xM, yT), new Vector2(xL, yB), new Vector2(xR, yB) };
            ds.FillGeometry(CanvasGeometry.CreatePolygon(ds, tri), fill);
            ds.DrawGeometry(CanvasGeometry.CreatePolygon(ds, tri), stroke, sw);
        }
        else
        {
            ds.FillRectangle(b, fill);
            ds.DrawRectangle(b, stroke, sw);
        }
    }

    private static void DrawLine(CanvasDrawingSession ds, LineElement line, float scale)
    {
        ds.DrawLine(
            new Vector2((float)line.X1, (float)line.Y1),
            new Vector2((float)line.X2, (float)line.Y2),
            ParseColor(line.Stroke, Colors.Black),
            (float)line.StrokeWidth * scale);
    }

    private static void DrawImage(CanvasDrawingSession ds, ImageElement image)
    {
        var b = image.Bounds.ToWinRect();
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                var bitmap = CanvasBitmap.LoadAsync(ds.Device, image.SourcePath).GetAwaiter().GetResult();
                var srcRect = new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
                var dstRect = b;

                if (image.Stretch == ImageStretch.Uniform)
                {
                    float s = Math.Min((float)(b.Width / bitmap.Size.Width),
                                       (float)(b.Height / bitmap.Size.Height));
                    float dw = (float)(bitmap.Size.Width * s), dh = (float)(bitmap.Size.Height * s);
                    dstRect = new Rect(b.X + (b.Width - dw) / 2, b.Y + (b.Height - dh) / 2, dw, dh);
                }
                else if (image.Stretch == ImageStretch.UniformToFill)
                {
                    float s = Math.Max((float)(b.Width / bitmap.Size.Width),
                                       (float)(b.Height / bitmap.Size.Height));
                    float dw = (float)(bitmap.Size.Width * s), dh = (float)(bitmap.Size.Height * s);
                    dstRect = new Rect(b.X + (b.Width - dw) / 2, b.Y + (b.Height - dh) / 2, dw, dh);
                }

                ds.DrawImage(bitmap, dstRect, srcRect, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                return;
            }
        }
        catch { }

        ds.FillRectangle(b, Colors.LightGray);
        ds.DrawLine((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom, Colors.DarkGray, 1);
        ds.DrawLine((float)b.Right, (float)b.Top, (float)b.Left, (float)b.Bottom, Colors.DarkGray, 1);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Extension point used by the rest of Infrastructure.</summary>
    internal static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        hex = hex.TrimStart('#');
        try
        {
            return hex.Length switch
            {
                6 => Color.FromArgb(255,
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16)),
                8 => Color.FromArgb(
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16),
                    Convert.ToByte(hex[6..8], 16)),
                _ => fallback
            };
        }
        catch { return fallback; }
    }
}
