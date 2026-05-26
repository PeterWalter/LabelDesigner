using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

namespace LabelDesigner.Infrastructure.Export;

public class PdfExportService : IPdfExportService
{
    private const float PointsPerPixel = 72f / 96f;

    public async Task ExportAsync(SceneDocument document, string outputPath, PdfExportOptions options)
    {
        using var doc = new PdfDocument();
        doc.PageSettings.Size = new Syncfusion.Drawing.SizeF(
            MillimetersToPoints(document.Page.WidthMm),
            MillimetersToPoints(document.Page.HeightMm));
        doc.PageSettings.Margins.All = 0;

        var page = doc.Pages.Add();
        var graphics = page.Graphics;

        var lookup = document.AllElements.ToDictionary(e => e.Id);

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;
            foreach (var id in layer.ElementIds)
            {
                if (!lookup.TryGetValue(id, out var el) || !el.Visible) continue;
                DrawElementToPdf(graphics, el, lookup, doc);
            }
        }

        await Task.Run(() => doc.Save(outputPath));
    }

    private void DrawElementToPdf(PdfGraphics graphics, DesignElement el,
        Dictionary<Guid, DesignElement> lookup, PdfDocument doc)
    {
        var bounds = el.Bounds;

        if (el is BarcodeElement barcode)
        {
            // Raster fallback for barcodes
            DrawBarcodeToPdf(graphics, barcode);
        }
        else if (el is TextElement text)
        {
            var style = (text.Bold && text.Italic) ? PdfFontStyle.Bold | PdfFontStyle.Italic
                      : text.Bold   ? PdfFontStyle.Bold
                      : text.Italic ? PdfFontStyle.Italic
                      : PdfFontStyle.Regular;

            var font = new PdfStandardFont(PdfFontFamily.Helvetica, (float)text.FontSize * PointsPerPixel, style);
            graphics.DrawString(text.Text, font,
                PdfBrushes.Black, new Syncfusion.Drawing.PointF((float)bounds.X * PointsPerPixel, (float)bounds.Y * PointsPerPixel));
        }
        else if (el is ShapeElement shape)
        {
            DrawShapeToPdf(graphics, shape);
        }
        else if (el is LineElement line)
        {
            graphics.DrawLine(PdfPens.Black,
                (float)line.X1 * PointsPerPixel,
                (float)line.Y1 * PointsPerPixel,
                (float)line.X2 * PointsPerPixel,
                (float)line.Y2 * PointsPerPixel);
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
                    DrawElementToPdf(graphics, child, lookup, doc);
            }
        }
    }

    private void DrawBarcodeToPdf(PdfGraphics graphics, BarcodeElement b)
    {
        graphics.DrawRectangle(PdfBrushes.White, new Syncfusion.Drawing.RectangleF(
            (float)b.Bounds.X * PointsPerPixel,
            (float)b.Bounds.Y * PointsPerPixel,
            (float)b.Bounds.Width * PointsPerPixel,
            (float)b.Bounds.Height * PointsPerPixel));
        graphics.DrawString(b.DisplayText, new PdfStandardFont(PdfFontFamily.Helvetica, 10 * PointsPerPixel),
            PdfBrushes.Black, new Syncfusion.Drawing.PointF((float)b.Bounds.X * PointsPerPixel, (float)b.Bounds.Y * PointsPerPixel));
    }

    private void DrawShapeToPdf(PdfGraphics graphics, ShapeElement shape)
    {
        var b = shape.Bounds;
        var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(255, 200, 200, 200));
        var pen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(0, 0, 0), (float)shape.StrokeWidth * PointsPerPixel);

        if (shape.Type == ShapeType.Ellipse)
        {
            graphics.DrawEllipse(brush, (float)b.X * PointsPerPixel, (float)b.Y * PointsPerPixel, (float)b.Width * PointsPerPixel, (float)b.Height * PointsPerPixel);
            graphics.DrawEllipse(pen, (float)b.X * PointsPerPixel, (float)b.Y * PointsPerPixel, (float)b.Width * PointsPerPixel, (float)b.Height * PointsPerPixel);
        }
        else
        {
            graphics.DrawRectangle(brush, (float)b.X * PointsPerPixel, (float)b.Y * PointsPerPixel, (float)b.Width * PointsPerPixel, (float)b.Height * PointsPerPixel);
            graphics.DrawRectangle(pen, (float)b.X * PointsPerPixel, (float)b.Y * PointsPerPixel, (float)b.Width * PointsPerPixel, (float)b.Height * PointsPerPixel);
        }
    }

    private void DrawImageToPdf(PdfGraphics graphics, ImageElement image)
    {
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.White);
                graphics.DrawRectangle(brush, (float)image.Bounds.X * PointsPerPixel, (float)image.Bounds.Y * PointsPerPixel,
                    (float)image.Bounds.Width * PointsPerPixel, (float)image.Bounds.Height * PointsPerPixel);
                  
                var pen = new PdfPen(Syncfusion.Drawing.Color.LightGray, 1);
                graphics.DrawRectangle(pen, (float)image.Bounds.X * PointsPerPixel, (float)image.Bounds.Y * PointsPerPixel,
                    (float)image.Bounds.Width * PointsPerPixel, (float)image.Bounds.Height * PointsPerPixel);
                  
                // Draw placeholder text with filename
                var fileName = System.IO.Path.GetFileName(image.SourcePath);
                graphics.DrawString(fileName, new PdfStandardFont(PdfFontFamily.Helvetica, 8 * PointsPerPixel),
                    PdfBrushes.Gray, new Syncfusion.Drawing.PointF(((float)image.Bounds.X + 2) * PointsPerPixel, ((float)image.Bounds.Y + 2) * PointsPerPixel));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering image to PDF '{image.SourcePath}': {ex.Message}");
        }
    }

    private static float MillimetersToPoints(double mm) => (float)(mm * 2.83465);
}
