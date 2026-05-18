using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;

namespace LabelDesigner.Infrastructure.Rendering;

/// <summary>
/// Shared Win2D element rendering logic used by both the canvas preview and
/// high-resolution print/rasterization paths. The <paramref name="scale"/>
/// parameter compensates for font size and stroke width when DPI differs from 96.
/// </summary>
internal static class ElementRenderer
{
    // Caches both CPU (SoftwareBitmap) and GPU (CanvasBitmap) resources.
    // CanvasBitmap holds a Direct3D texture — must be cached to avoid per-frame GPU allocation.
    // SoftwareBitmap is IDisposable — disposed when entries are evicted.
    private static readonly Dictionary<string, (Windows.Graphics.Imaging.SoftwareBitmap? Sw, CanvasBitmap? Gpu)> _barcodeCache = new();
    private static readonly Dictionary<string, CanvasBitmap> _svgCache = new();

    private static CanvasBitmap? GetCachedBarcodeBitmap(
        CanvasDrawingSession ds, IBarcodeService barcode, string value, BarcodeSymbology symbology, int width, int height)
    {
        if (width <= 0 || height <= 0) return null;
        var key = $"{value}|{symbology}|{width}|{height}";
        if (_barcodeCache.TryGetValue(key, out var cached) && cached.Gpu != null)
            return cached.Gpu;

        // Dispose old entries for the same key before replacing.
        if (_barcodeCache.TryGetValue(key, out var old))
        {
            old.Gpu?.Dispose();
            old.Sw?.Dispose();
        }

        var format = symbology switch
        {
            BarcodeSymbology.Code39 => ZXing.BarcodeFormat.CODE_39,
            BarcodeSymbology.QRCode => ZXing.BarcodeFormat.QR_CODE,
            BarcodeSymbology.EAN13 => ZXing.BarcodeFormat.EAN_13,
            BarcodeSymbology.EAN8 => ZXing.BarcodeFormat.EAN_8,
            BarcodeSymbology.UPCA => ZXing.BarcodeFormat.UPC_A,
            BarcodeSymbology.DataMatrix => ZXing.BarcodeFormat.DATA_MATRIX,
            BarcodeSymbology.PDF417 => ZXing.BarcodeFormat.PDF_417,
            BarcodeSymbology.Aztec => ZXing.BarcodeFormat.AZTEC,
            BarcodeSymbology.ITF => ZXing.BarcodeFormat.ITF,
            _ => ZXing.BarcodeFormat.CODE_128
        };

        var sw = barcode.Generate(value, format, width, height);
        CanvasBitmap? gpu = sw != null ? CanvasBitmap.CreateFromSoftwareBitmap(ds.Device, sw) : null;
        _barcodeCache[key] = (sw, gpu);
        return gpu;
    }

    /// <summary>Disposes all cached bitmaps and clears the cache. Call when a document is closed or cleared.</summary>
    internal static void ClearBarcodeCache()
    {
        foreach (var (sw, gpu) in _barcodeCache.Values)
        {
            gpu?.Dispose();
            sw?.Dispose();
        }
        _barcodeCache.Clear();

        foreach (var bitmap in _svgCache.Values)
            bitmap.Dispose();
        _svgCache.Clear();
    }

    internal static void DrawElement(
        CanvasDrawingSession ds,
        DesignElement el,
        Dictionary<Guid, DesignElement> lookup,
        IBarcodeService barcode,
        ISvgService svg,
        float scale = 1f)
    {
        if (el is BarcodeElement b) DrawBarcode(ds, b, barcode, scale);
        else if (el is TextElement txt) DrawText(ds, txt, scale);
        else if (el is ShapeElement shape) DrawShape(ds, shape, scale);
        else if (el is LineElement line) DrawLine(ds, line, scale);
        else if (el is ImageElement image) DrawImage(ds, image, svg);
        else if (el is SvgElement svgEl) DrawSvg(ds, svgEl, svg);
        else if (el is ContainerElement container)
        {
            foreach (var childId in container.ChildIds)
            {
                if (lookup.TryGetValue(childId, out var child) && child.Visible)
                    DrawElement(ds, child, lookup, barcode, svg, scale);
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
                tx = (float)(bounds.X + bounds.Width / 2);
                ty = (float)(bounds.Y + textHeight / 2);
                break;
            case BarcodeTextPosition.Bottom:
                barcodeRect = new(bounds.X, bounds.Y,
                    bounds.Width, bounds.Height - textHeight - padding);
                tx = (float)(bounds.X + bounds.Width / 2);
                ty = (float)(bounds.Y + bounds.Height - textHeight / 2);
                break;
            case BarcodeTextPosition.Left:
                barcodeRect = new(bounds.X + 60 * scale, bounds.Y,
                    bounds.Width - 60 * scale, bounds.Height);
                tx = (float)(bounds.X + 30 * scale);
                ty = (float)(bounds.Y + bounds.Height / 2);
                break;
            case BarcodeTextPosition.Right:
                barcodeRect = new(bounds.X, bounds.Y,
                    bounds.Width - 60 * scale, bounds.Height);
                tx = (float)(bounds.X + bounds.Width - 30 * scale);
                ty = (float)(bounds.Y + bounds.Height / 2);
                break;
        }

        var bmp = GetCachedBarcodeBitmap(ds, barcode, b.Value, b.Symbology, (int)barcodeRect.Width, (int)barcodeRect.Height);
        if (bmp != null)
        {
            ds.DrawImage(bmp, barcodeRect.ToWinRect());
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

    private static CanvasBitmap? GetCachedSvgBitmap(
        CanvasDrawingSession ds, ISvgService svg, string svgPath, int width, int height)
    {
        if (width <= 0 || height <= 0 || string.IsNullOrWhiteSpace(svgPath) || !File.Exists(svgPath))
            return null;

        var key = $"{svgPath}|{width}|{height}";
        if (_svgCache.TryGetValue(key, out var cached))
            return cached;

        var pngBytes = svg.RasterizeToPng(svgPath, width, height);
        using var stream = new InMemoryRandomAccessStream();
        using (var outStream = stream.AsStreamForWrite())
        {
            outStream.Write(pngBytes, 0, pngBytes.Length);
            outStream.Flush();
        }
        stream.Seek(0);
        var bitmap = CanvasBitmap.LoadAsync(ds.Device, stream).GetAwaiter().GetResult();
        _svgCache[key] = bitmap;
        return bitmap;
    }

    private static void DrawImage(CanvasDrawingSession ds, ImageElement image, ISvgService svg)
    {
        var b = image.Bounds.ToWinRect();
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                CanvasBitmap? bitmap = null;
                if (Path.GetExtension(image.SourcePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    bitmap = GetCachedSvgBitmap(ds, svg, image.SourcePath, Math.Max(1, (int)b.Width), Math.Max(1, (int)b.Height));
                }
                else
                {
                    bitmap = CanvasBitmap.LoadAsync(ds.Device, image.SourcePath).GetAwaiter().GetResult();
                }

                if (bitmap == null)
                    return;

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
                if (!Path.GetExtension(image.SourcePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    bitmap.Dispose();
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering image '{image.SourcePath}': {ex.Message}");
        }

        // Placeholder for missing image
        ds.FillRectangle(b, Colors.LightGray);
        ds.DrawLine((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom, Colors.DarkGray, 1);
        ds.DrawLine((float)b.Right, (float)b.Top, (float)b.Left, (float)b.Bottom, Colors.DarkGray, 1);
    }

    private static void DrawSvg(CanvasDrawingSession ds, SvgElement svg, ISvgService svgService)
    {
        var b = svg.Bounds.ToWinRect();
        try
        {
            if (!string.IsNullOrEmpty(svg.SourcePath) && System.IO.File.Exists(svg.SourcePath))
            {
                var bitmap = GetCachedSvgBitmap(ds, svgService, svg.SourcePath, Math.Max(1, (int)b.Width), Math.Max(1, (int)b.Height));

                if (bitmap == null)
                    return;

                var srcRect = new Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
                var dstRect = b;

                if (svg.Stretch == "Uniform")
                {
                    float s = Math.Min((float)(b.Width / bitmap.Size.Width),
                                       (float)(b.Height / bitmap.Size.Height));
                    float dw = (float)(bitmap.Size.Width * s), dh = (float)(bitmap.Size.Height * s);
                    dstRect = new Rect(b.X + (b.Width - dw) / 2, b.Y + (b.Height - dh) / 2, dw, dh);
                }
                else if (svg.Stretch == "UniformToFill")
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering SVG '{svg.SourcePath}': {ex.Message}");
        }

        // Placeholder for missing SVG
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
