using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Common;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Windows.Graphics.Imaging;
using System.Numerics;

namespace LabelDesigner.Infrastructure.Export;

public class PrintService : IPrintService
{
    private readonly IBarcodeService _barcode;

    public PrintService(IBarcodeService barcode)
    {
        _barcode = barcode;
    }

    public async Task PrintAsync(SceneDocument document)
    {
        var bitmap = RenderDocumentToBitmap(document, 300);
        // Print submission happens in the App layer via PrintManager
        await Task.CompletedTask;
    }

    public async Task ShowPrintPreviewAsync(SceneDocument document)
    {
        await Task.CompletedTask;
    }

    public SoftwareBitmap RenderDocumentToBitmap(SceneDocument document, float dpi)
    {
        double pageWidthPx = document.Page.WidthMm * dpi / 25.4;
        double pageHeightPx = document.Page.HeightMm * dpi / 25.4;

        using var device = CanvasDevice.GetSharedDevice();
        using var renderTarget = new CanvasRenderTarget(device,
            (int)pageWidthPx, (int)pageHeightPx, dpi);

        using var ds = renderTarget.CreateDrawingSession();
        ds.Clear(Colors.White);
        ds.Transform = Matrix3x2.CreateScale(dpi / 96.0f);

        var lookup = document.AllElements.ToDictionary(e => e.Id);
        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;
            foreach (var id in layer.ElementIds)
            {
                if (!lookup.TryGetValue(id, out var el) || !el.Visible) continue;
                DrawElementHighRes(ds, el, lookup, dpi / 96.0f);
            }
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            renderTarget.GetPixelBytes().AsBuffer(),
            BitmapPixelFormat.Bgra8,
            (int)pageWidthPx,
            (int)pageHeightPx,
            BitmapAlphaMode.Premultiplied);
    }

    private void DrawElementHighRes(CanvasDrawingSession ds, DesignElement el,
        Dictionary<Guid, DesignElement> lookup, float scale)
    {
        if (el is BarcodeElement barcode)
            DrawBarcodeHighRes(ds, barcode, scale);
        else if (el is TextElement text)
            DrawTextHighRes(ds, text, scale);
        else if (el is ShapeElement shape)
            DrawShapeHighRes(ds, shape, scale);
        else if (el is LineElement line)
            DrawLineHighRes(ds, line, scale);
        else if (el is ImageElement image)
            DrawImageHighRes(ds, image, scale);
        else if (el is ContainerElement container)
        {
            foreach (var childId in container.ChildIds)
            {
                if (lookup.TryGetValue(childId, out var child) && child.Visible)
                    DrawElementHighRes(ds, child, lookup, scale);
            }
        }
    }

    private void DrawBarcodeHighRes(CanvasDrawingSession ds, BarcodeElement b, float scale)
    {
        var bounds = b.Bounds;
        float padding = 6 * scale;
        float textHeight = 18 * scale;
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
                barcodeRect = new RectD(bounds.X + 60 * scale, bounds.Y, bounds.Width - 60 * scale, bounds.Height);
                textX = (float)bounds.X; textY = (float)(bounds.Y + bounds.Height / 2); break;
            case BarcodeTextPosition.Right:
                barcodeRect = new RectD(bounds.X, bounds.Y, bounds.Width - 60 * scale, bounds.Height);
                textX = (float)(bounds.X + bounds.Width - 60 * scale); textY = (float)(bounds.Y + bounds.Height / 2); break;
        }

        var bmp = _barcode.Generate(b.Value, ZXing.BarcodeFormat.CODE_128,
            (int)barcodeRect.Width, (int)barcodeRect.Height);
        if (bmp != null)
        {
            var img = CanvasBitmap.CreateFromSoftwareBitmap(ds.Device, bmp);
            ds.DrawImage(img, barcodeRect.ToWinRect());
        }

        ds.DrawText(text, new Vector2(textX, textY), Colors.Black, new CanvasTextFormat
        {
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center,
            FontSize = 14 * scale
        });
    }

    private void DrawTextHighRes(CanvasDrawingSession ds, TextElement txt, float scale)
    {
        ds.DrawText(txt.Text, (float)txt.Bounds.X, (float)txt.Bounds.Y, Colors.Black,
            new CanvasTextFormat { FontSize = (float)txt.FontSize * scale });
    }

    private void DrawShapeHighRes(CanvasDrawingSession ds, ShapeElement shape, float scale)
    {
        var b = shape.Bounds.ToWinRect();
        var fill = Rendering.RenderService.ParseColor(shape.Fill, Colors.LightGray);
        var stroke = Rendering.RenderService.ParseColor(shape.Stroke, Colors.Black);
        float sw = (float)shape.StrokeWidth * scale;

        if (shape.Type == ShapeType.Ellipse)
        {
            ds.FillEllipse(new Vector2((float)(b.X + b.Width / 2), (float)(b.Y + b.Height / 2)),
                (float)(b.Width / 2), (float)(b.Height / 2), fill);
            ds.DrawEllipse(new Vector2((float)(b.X + b.Width / 2), (float)(b.Y + b.Height / 2)),
                (float)(b.Width / 2), (float)(b.Height / 2), stroke, sw);
        }
        else
        {
            ds.FillRectangle(b, fill);
            ds.DrawRectangle(b, stroke, sw);
        }
    }

    private void DrawLineHighRes(CanvasDrawingSession ds, LineElement line, float scale)
    {
        ds.DrawLine(new Vector2((float)line.X1, (float)line.Y1),
            new Vector2((float)line.X2, (float)line.Y2),
            Colors.Black, (float)line.StrokeWidth * scale);
    }

    private void DrawImageHighRes(CanvasDrawingSession ds, ImageElement image, float scale)
    {
        var b = image.Bounds.ToWinRect();
        ds.FillRectangle(b, Colors.LightGray);
    }
}
