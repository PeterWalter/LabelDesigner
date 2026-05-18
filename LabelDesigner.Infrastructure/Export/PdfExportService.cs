using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

namespace LabelDesigner.Infrastructure.Export;

public class PdfExportService : IPdfExportService
{
    public async Task ExportAsync(SceneDocument document, string outputPath, PdfExportOptions options)
    {
        using var doc = new PdfDocument();
        doc.PageSettings.Size = new Syncfusion.Drawing.SizeF(
            MillimetersToPoints(document.Page.WidthMm),
            MillimetersToPoints(document.Page.HeightMm));
        doc.PageSettings.Margins.All = 0;

        var page = doc.Pages.Add();
        var graphics = page.Graphics;

        float dpi = options.Dpi;
        float scale = dpi / 96.0f;

        var lookup = document.AllElements.ToDictionary(e => e.Id);

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;
            foreach (var id in layer.ElementIds)
            {
                if (!lookup.TryGetValue(id, out var el) || !el.Visible) continue;
                DrawElementToPdf(graphics, el, lookup, scale, doc);
            }
        }

        await Task.Run(() => doc.Save(outputPath));
    }

    private void DrawElementToPdf(PdfGraphics graphics, DesignElement el,
        Dictionary<Guid, DesignElement> lookup, float scale, PdfDocument doc)
    {
        var bounds = el.Bounds;

        if (el is BarcodeElement barcode)
        {
            // Raster fallback for barcodes
            DrawBarcodeToPdf(graphics, barcode, scale);
        }
        else if (el is TextElement text)
        {
            var style = (text.Bold && text.Italic) ? PdfFontStyle.Bold | PdfFontStyle.Italic
                      : text.Bold   ? PdfFontStyle.Bold
                      : text.Italic ? PdfFontStyle.Italic
                      : PdfFontStyle.Regular;

            var font = new PdfStandardFont(PdfFontFamily.Helvetica, (float)text.FontSize * scale, style);
            graphics.DrawString(text.Text, font,
                PdfBrushes.Black, new Syncfusion.Drawing.PointF((float)bounds.X, (float)bounds.Y));
        }
        else if (el is ShapeElement shape)
        {
            DrawShapeToPdf(graphics, shape, scale);
        }
        else if (el is LineElement line)
        {
            graphics.DrawLine(PdfPens.Black, (float)line.X1, (float)line.Y1, (float)line.X2, (float)line.Y2);
        }
        else if (el is ImageElement image)
        {
            DrawImageToPdf(graphics, image);
        }
        else if (el is SvgElement svg)
        {
            var stretchEnum = svg.Stretch == "UniformToFill" ? ImageStretch.UniformToFill : ImageStretch.Uniform;
            DrawImageToPdf(graphics, new ImageElement { SourcePath = svg.SourcePath, Bounds = svg.Bounds, Stretch = stretchEnum });
        }
        else if (el is ContainerElement container)
        {
            foreach (var childId in container.ChildIds)
            {
                if (lookup.TryGetValue(childId, out var child) && child.Visible)
                    DrawElementToPdf(graphics, child, lookup, scale, doc);
            }
        }
    }

    private void DrawBarcodeToPdf(PdfGraphics graphics, BarcodeElement b, float scale)
    {
        graphics.DrawRectangle(PdfBrushes.White, new Syncfusion.Drawing.RectangleF(
            (float)b.Bounds.X, (float)b.Bounds.Y,
            (float)b.Bounds.Width, (float)b.Bounds.Height));
        graphics.DrawString(b.DisplayText, new PdfStandardFont(PdfFontFamily.Helvetica, 10),
            PdfBrushes.Black, new Syncfusion.Drawing.PointF((float)b.Bounds.X, (float)b.Bounds.Y));
    }

    private void DrawShapeToPdf(PdfGraphics graphics, ShapeElement shape, float scale)
    {
        var b = shape.Bounds;
        var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(255, 200, 200, 200));
        var pen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(0, 0, 0), (float)shape.StrokeWidth * scale);

        if (shape.Type == ShapeType.Ellipse)
        {
            graphics.DrawEllipse(brush, (float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
            graphics.DrawEllipse(pen, (float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
        }
        else
        {
            graphics.DrawRectangle(brush, (float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
            graphics.DrawRectangle(pen, (float)b.X, (float)b.Y, (float)b.Width, (float)b.Height);
        }
    }

    private void DrawImageToPdf(PdfGraphics graphics, ImageElement image)
    {
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.White);
                graphics.DrawRectangle(brush, (float)image.Bounds.X, (float)image.Bounds.Y,
                    (float)image.Bounds.Width, (float)image.Bounds.Height);
                  
                var pen = new PdfPen(Syncfusion.Drawing.Color.LightGray, 1);
                graphics.DrawRectangle(pen, (float)image.Bounds.X, (float)image.Bounds.Y,
                    (float)image.Bounds.Width, (float)image.Bounds.Height);
                  
                // Draw placeholder text with filename
                var fileName = System.IO.Path.GetFileName(image.SourcePath);
                graphics.DrawString(fileName, new PdfStandardFont(PdfFontFamily.Helvetica, 8),
                    PdfBrushes.Gray, new Syncfusion.Drawing.PointF((float)image.Bounds.X + 2, (float)image.Bounds.Y + 2));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering image to PDF '{image.SourcePath}': {ex.Message}");
        }
    }

    private static float MillimetersToPoints(double mm) => (float)(mm * 2.83465);
}
