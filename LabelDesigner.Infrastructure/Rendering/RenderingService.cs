using System.Numerics;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Windows.UI;

namespace LabelDesigner.Infrastructure;

public class RenderService : IRenderService
{
    private readonly IBarcodeService _barcode;

    public RenderService(IBarcodeService barcode)
    {
        _barcode = barcode;
    }

    public void RenderScene(
        CanvasDrawingSession ds,
        SceneDocument document,
        IEnumerable<Guid> selectedIds,
        float zoom,
        RectD viewport)
    {
        ds.Clear(Colors.White);

        var selectedSet = new HashSet<Guid>(selectedIds);
        var originalTransform = ds.Transform;
        ds.Transform = Matrix3x2.CreateTranslation(-(float)viewport.X, -(float)viewport.Y)
                     * Matrix3x2.CreateScale(zoom);

        float pageW = (float)document.Page.WidthMm * 3.78f;
        float pageH = (float)document.Page.HeightMm * 3.78f;
        ds.FillRectangle(0, 0, pageW, pageH, Colors.White);

        if (zoom > 0.5f)
        {
            int gridSize = 20;
            for (float x = 0; x < pageW; x += gridSize)
                ds.DrawLine(x, 0, x, pageH, Colors.LightGray, 0.5f);
            for (float y = 0; y < pageH; y += gridSize)
                ds.DrawLine(0, y, pageW, y, Colors.LightGray, 0.5f);
        }

        var m = document.Page.Margins;
        ds.DrawRectangle((float)m.Left * 3.78f, (float)m.Top * 3.78f,
            pageW - (float)m.Left * 3.78f - (float)m.Right * 3.78f,
            pageH - (float)m.Top * 3.78f - (float)m.Bottom * 3.78f, Colors.Gray, 1);

        ds.DrawRectangle(0, 0, pageW, pageH, Colors.Black, 2);

        var lookup = document.AllElements.ToDictionary(e => e.Id);

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;

            var elements = layer.ElementIds
                .Select(id => lookup.GetValueOrDefault(id))
                .Where(e => e != null && e.Visible)
                .OrderBy(e => e!.ZIndex);

            foreach (var el in elements)
            {
                if (el == null) continue;

                var local = ds.Transform;
                ds.Transform = el.GetLocalTransform() * local;
                DrawElement(ds, el, lookup);
                if (selectedSet.Contains(el.Id))
                    DrawSelectionHandles(ds, el, zoom);
                ds.Transform = local;
            }
        }
        ds.Transform = originalTransform;
    }

    private void DrawElement(CanvasDrawingSession ds, DesignElement el, Dictionary<Guid, DesignElement> lookup)
    {
        if (el is BarcodeElement barcode)
            DrawBarcode(ds, barcode);
        else if (el is TextElement text)
            DrawText(ds, text);
        else if (el is ShapeElement shape)
            DrawShape(ds, shape);
        else if (el is LineElement line)
            DrawLine(ds, line);
        else if (el is ImageElement image)
            DrawImage(ds, image);
        else if (el is ContainerElement container)
            DrawContainer(ds, container, lookup);
    }

    private void DrawBarcode(CanvasDrawingSession ds, BarcodeElement b)
    {
        var bounds = b.Bounds;
        float padding = 6, textHeight = 18;
        RectD barcodeRect = bounds;
        float textX = 0, textY = 0;
        string text = b.DisplayText;

        switch (b.TextPosition)
        {
            case BarcodeTextPosition.Top:
                barcodeRect = new RectD(bounds.X, bounds.Y + textHeight + padding, bounds.Width, bounds.Height - textHeight - padding);
                textX = (float)(bounds.X + bounds.Width / 2); textY = (float)bounds.Y; break;
            case BarcodeTextPosition.Bottom:
                barcodeRect = new RectD(bounds.X, bounds.Y, bounds.Width, bounds.Height - textHeight - padding);
                textX = (float)(bounds.X + bounds.Width / 2); textY = (float)(bounds.Y + bounds.Height - textHeight); break;
            case BarcodeTextPosition.Left:
                barcodeRect = new RectD(bounds.X + 60, bounds.Y, bounds.Width - 60, bounds.Height);
                textX = (float)bounds.X; textY = (float)(bounds.Y + bounds.Height / 2); break;
            case BarcodeTextPosition.Right:
                barcodeRect = new RectD(bounds.X, bounds.Y, bounds.Width - 60, bounds.Height);
                textX = (float)(bounds.X + bounds.Width - 60); textY = (float)(bounds.Y + bounds.Height / 2); break;
        }

        var bmp = _barcode.Generate(b.Value, ZXing.BarcodeFormat.CODE_128, (int)barcodeRect.Width, (int)barcodeRect.Height);
        if (bmp != null)
        {
            var img = CanvasBitmap.CreateFromSoftwareBitmap(ds.Device, bmp);
            ds.DrawImage(img, barcodeRect.ToWinRect());
        }

        ds.DrawText(text, new Vector2(textX, textY), Colors.Black, new CanvasTextFormat
        {
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center, FontSize = 14
        });
    }

    private void DrawText(CanvasDrawingSession ds, TextElement txt)
    {
        var format = new CanvasTextFormat { FontSize = (float)txt.FontSize };
        ds.DrawText(txt.Text, (float)txt.Bounds.X, (float)txt.Bounds.Y, Colors.Black, format);
    }

    private void DrawShape(CanvasDrawingSession ds, ShapeElement shape)
    {
        var b = shape.Bounds.ToWinRect();
        var fill = ParseColor(shape.Fill, Colors.LightGray);
        var stroke = ParseColor(shape.Stroke, Colors.Black);
        float sw = (float)shape.StrokeWidth;
        var cx = new Vector2((float)(b.X + b.Width / 2), (float)(b.Y + b.Height / 2));

        if (shape.Type == ShapeType.Ellipse)
        {
            ds.FillEllipse(cx, (float)(b.Width / 2), (float)(b.Height / 2), fill);
            ds.DrawEllipse(cx, (float)(b.Width / 2), (float)(b.Height / 2), stroke, sw);
        }
        else
        {
            ds.FillRectangle(b, fill);
            ds.DrawRectangle(b, stroke, sw);
        }
    }

    private void DrawLine(CanvasDrawingSession ds, LineElement line)
    {
        ds.DrawLine(new Vector2((float)line.X1, (float)line.Y1),
            new Vector2((float)line.X2, (float)line.Y2),
            ParseColor(line.Stroke, Colors.Black), (float)line.StrokeWidth);
    }

    private void DrawImage(CanvasDrawingSession ds, ImageElement image)
    {
        var b = image.Bounds.ToWinRect();
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                var bitmap = CanvasBitmap.LoadAsync(ds.Device, image.SourcePath).GetAwaiter().GetResult();
                ds.DrawImage(bitmap, (float)b.X, (float)b.Y);
                return;
            }
        }
        catch { }
        ds.FillRectangle(b, Colors.LightGray);
        ds.DrawLine((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom, Colors.DarkGray, 1);
        ds.DrawLine((float)b.Right, (float)b.Top, (float)b.Left, (float)b.Bottom, Colors.DarkGray, 1);
    }

    private void DrawContainer(CanvasDrawingSession ds, ContainerElement container, Dictionary<Guid, DesignElement> lookup)
    {
        foreach (var childId in container.ChildIds.OrderBy(id => lookup.GetValueOrDefault(id)?.ZIndex ?? 0))
        {
            if (lookup.TryGetValue(childId, out var child) && child.Visible)
            {
                var local = ds.Transform;
                ds.Transform = child.GetLocalTransform() * local;
                DrawElement(ds, child, lookup);
                ds.Transform = local;
            }
        }
    }

    private static void DrawSelectionHandles(CanvasDrawingSession ds, DesignElement el, float zoom)
    {
        var rect = el.Bounds.ToWinRect();
        float zf = Math.Max(zoom, 0.25f);
        float s = 6f / zf, es = 4f / zf, rotR = 5f / zf, rotOff = 20f / zf;

        // Corner handles
        foreach (var p in new[] {
            new Vector2((float)rect.Left, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Bottom),
            new Vector2((float)rect.Left, (float)rect.Bottom),
        })
        {
            ds.FillRectangle(p.X - s, p.Y - s, s * 2, s * 2, Colors.White);
            ds.DrawRectangle(p.X - s, p.Y - s, s * 2, s * 2, Colors.Blue);
        }

        // Edge midpoints
        foreach (var p in new[] {
            new Vector2((float)(rect.Left + rect.Width / 2), (float)rect.Top),
            new Vector2((float)(rect.Right), (float)(rect.Top + rect.Height / 2)),
            new Vector2((float)(rect.Left + rect.Width / 2), (float)(rect.Bottom)),
            new Vector2((float)(rect.Left), (float)(rect.Top + rect.Height / 2)),
        })
        {
            ds.FillRectangle(p.X - es, p.Y - es, es * 2, es * 2, Colors.White);
            ds.DrawRectangle(p.X - es, p.Y - es, es * 2, es * 2, Colors.Blue);
        }

        // Rotation handle
        float rotCX = (float)(rect.Left + rect.Width / 2);
        float rotCY = (float)(rect.Top - rotOff);
        ds.DrawLine(new Vector2(rotCX, (float)rect.Top), new Vector2(rotCX, rotCY), Colors.Blue, 1);
        ds.FillCircle(new Vector2(rotCX, rotCY), rotR, Colors.White);
        ds.DrawCircle(new Vector2(rotCX, rotCY), rotR, Colors.Blue);

        // Dashed outline
        ds.DrawRectangle(rect, Colors.Blue, 1, new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash });
    }

    internal static Color ParseColor(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromArgb(255, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
        }
        catch { }
        return fallback;
    }
}
