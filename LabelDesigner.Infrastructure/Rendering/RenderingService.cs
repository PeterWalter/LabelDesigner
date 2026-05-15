using System.Numerics;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;

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

        // Apply viewport transform
        var originalTransform = ds.Transform;
        ds.Transform = Matrix3x2.CreateTranslation(-(float)viewport.X, -(float)viewport.Y)
                     * Matrix3x2.CreateScale(zoom);

        // 1. Page background
        float pageW = (float)document.Page.WidthMm * 3.78f;
        float pageH = (float)document.Page.HeightMm * 3.78f;
        ds.FillRectangle(0, 0, pageW, pageH, Colors.White);

        // 2. Grid (zoom-aware)
        if (zoom > 0.5f)
        {
            int gridSize = 20;
            for (float x = 0; x < pageW; x += gridSize)
                ds.DrawLine(x, 0, x, pageH, Colors.LightGray, 0.5f);
            for (float y = 0; y < pageH; y += gridSize)
                ds.DrawLine(0, y, pageW, y, Colors.LightGray, 0.5f);
        }

        // 3. Margin guides
        var m = document.Page.Margins;
        float ml = (float)m.Left * 3.78f;
        float mt = (float)m.Top * 3.78f;
        float mr = pageW - (float)m.Right * 3.78f;
        float mb = pageH - (float)m.Bottom * 3.78f;
        ds.DrawRectangle(ml, mt, mr - ml, mb - mt, Colors.Gray, 1);

        // 4. Page outline
        ds.DrawRectangle(0, 0, pageW, pageH, Colors.Black, 2);

        // 5. Elements by layer, by ZIndex
        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;

            var elements = layer.ElementIds
                .Select(id => document.AllElements.FirstOrDefault(e => e.Id == id))
                .Where(e => e != null && e.Visible)
                .OrderBy(e => e!.ZIndex);

            foreach (var el in elements)
            {
                if (el == null) continue;

                // Apply local transform (rotation around center)
                var local = ds.Transform;
                ds.Transform = el.GetLocalTransform() * local;

                DrawElement(ds, el);

                // Draw selection handles if selected
                if (selectedSet.Contains(el.Id))
                    DrawSelectionHandles(ds, el);

                ds.Transform = local; // restore
            }
        }

        // Restore original transform
        ds.Transform = originalTransform;
    }

    private void DrawElement(CanvasDrawingSession ds, DesignElement el)
    {
        if (el is BarcodeElement barcode)
            DrawBarcode(ds, barcode);
        else if (el is TextElement text)
            DrawText(ds, text);
        else if (el is ShapeElement shape)
            DrawShape(ds, shape);
        else if (el is LineElement line)
            DrawLine(ds, line);
    }

    private void DrawBarcode(CanvasDrawingSession ds, BarcodeElement b)
    {
        var bounds = b.Bounds;
        float padding = 6;
        float textHeight = 18;

        RectD barcodeRect = bounds;
        float textX = 0, textY = 0;
        string text = b.DisplayText;

        switch (b.TextPosition)
        {
            case BarcodeTextPosition.Top:
                barcodeRect = new RectD(bounds.X, bounds.Y + textHeight + padding,
                    bounds.Width, bounds.Height - textHeight - padding);
                textX = (float)(bounds.X + bounds.Width / 2);
                textY = (float)bounds.Y;
                break;

            case BarcodeTextPosition.Bottom:
                barcodeRect = new RectD(bounds.X, bounds.Y,
                    bounds.Width, bounds.Height - textHeight - padding);
                textX = (float)(bounds.X + bounds.Width / 2);
                textY = (float)(bounds.Y + bounds.Height - textHeight);
                break;

            case BarcodeTextPosition.Left:
                barcodeRect = new RectD(bounds.X + 60, bounds.Y,
                    bounds.Width - 60, bounds.Height);
                textX = (float)bounds.X;
                textY = (float)(bounds.Y + bounds.Height / 2);
                break;

            case BarcodeTextPosition.Right:
                barcodeRect = new RectD(bounds.X, bounds.Y,
                    bounds.Width - 60, bounds.Height);
                textX = (float)(bounds.X + bounds.Width - 60);
                textY = (float)(bounds.Y + bounds.Height / 2);
                break;
        }

        var bmp = _barcode.Generate(b.Value, ZXing.BarcodeFormat.CODE_128,
            (int)barcodeRect.Width, (int)barcodeRect.Height);

        if (bmp != null)
        {
            var img = CanvasBitmap.CreateFromSoftwareBitmap(ds.Device, bmp);
            ds.DrawImage(img, barcodeRect.ToWinRect());
        }

        var format = new CanvasTextFormat
        {
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            FontSize = 14
        };

        ds.DrawText(text, new Vector2(textX, textY), Colors.Black, format);
    }

    private void DrawText(CanvasDrawingSession ds, TextElement txt)
    {
        var format = new CanvasTextFormat
        {
            FontSize = (float)txt.FontSize
        };

        ds.DrawText(txt.Text, (float)txt.Bounds.X, (float)txt.Bounds.Y, Colors.Black, format);
    }

    private void DrawShape(CanvasDrawingSession ds, ShapeElement shape)
    {
        var b = shape.Bounds.ToWinRect();
        ds.FillRectangle(b, Colors.LightGray);
        ds.DrawRectangle(b, Colors.Black, (float)shape.StrokeWidth);
    }

    private void DrawLine(CanvasDrawingSession ds, LineElement line)
    {
        ds.DrawLine(
            new Vector2((float)line.X1, (float)line.Y1),
            new Vector2((float)line.X2, (float)line.Y2),
            Colors.Black,
            (float)line.StrokeWidth);
    }

    private static void DrawSelectionHandles(CanvasDrawingSession ds, DesignElement el)
    {
        var rect = el.Bounds.ToWinRect();
        float s = 6;

        var points = new[]
        {
            new Vector2((float)rect.Left, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Bottom),
            new Vector2((float)rect.Left, (float)rect.Bottom),
        };

        foreach (var p in points)
        {
            ds.FillRectangle(p.X - s, p.Y - s, s * 2, s * 2, Colors.White);
            ds.DrawRectangle(p.X - s, p.Y - s, s * 2, s * 2, Colors.Blue);
        }
    }
}
