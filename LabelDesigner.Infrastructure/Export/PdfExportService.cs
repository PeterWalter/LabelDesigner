using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Infrastructure.Interfaces;
using System.Runtime.InteropServices.WindowsRuntime;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using ZXing;
using ZXingBarcodeFormat = ZXing.BarcodeFormat;

namespace LabelDesigner.Infrastructure.Export;

public class PdfExportService : IPdfExportService
{
    private readonly IBarcodeService _barcode;

    public PdfExportService(IBarcodeService barcode)
    {
        _barcode = barcode;
    }

    public async Task ExportAsync(SceneDocument document, string outputPath, PdfExportOptions options)
    {
        // Convert canvas screen-pixels to PDF points: 72pt/in ÷ (PixelsPerMm × 25.4 mm/in)
        float pointsPerPixel = 72f / (float)(options.PixelsPerMm * 25.4);

        using var doc = new PdfDocument();
        doc.PageSettings.Size = new Syncfusion.Drawing.SizeF(
            MillimetersToPoints(document.Page.WidthMm),
            MillimetersToPoints(document.Page.HeightMm));
        doc.PageSettings.Margins.All = 0;

        var page = doc.Pages.Add();
        var graphics = page.Graphics;

        var lookup = BuildElementLookup(document);

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;
            foreach (var id in layer.ElementIds)
            {
                if (!lookup.TryGetValue(id, out var el) || !el.Visible) continue;
                DrawElementToPdf(graphics, el, lookup, doc, pointsPerPixel);
            }
        }

        await Task.Run(() => doc.Save(outputPath));
    }

    private void DrawElementToPdf(PdfGraphics graphics, DesignElement el,
        Dictionary<Guid, DesignElement> lookup, PdfDocument doc, float ptp)
    {
        var bounds = el.Bounds;

        if (el is BarcodeElement barcode)
        {
            DrawBarcodeToPdf(graphics, barcode, ptp);
        }
        else if (el is TextElement text)
        {
            var style = (text.Bold && text.Italic) ? PdfFontStyle.Bold | PdfFontStyle.Italic
                      : text.Bold   ? PdfFontStyle.Bold
                      : text.Italic ? PdfFontStyle.Italic
                      : PdfFontStyle.Regular;

            var font = new PdfStandardFont(PdfFontFamily.Helvetica, (float)text.FontSize * ptp, style);
            graphics.DrawString(text.Text, font,
                PdfBrushes.Black, new Syncfusion.Drawing.PointF((float)bounds.X * ptp, (float)bounds.Y * ptp));
        }
        else if (el is ShapeElement shape)
        {
            DrawShapeToPdf(graphics, shape, ptp);
        }
        else if (el is LineElement line)
        {
            graphics.DrawLine(PdfPens.Black,
                (float)line.X1 * ptp,
                (float)line.Y1 * ptp,
                (float)line.X2 * ptp,
                (float)line.Y2 * ptp);
        }
        else if (el is ImageElement image)
        {
            DrawImageToPdf(graphics, image, ptp);
        }
        else if (el is SvgElement svg)
        {
            var stretchEnum = svg.Stretch == "UniformToFill" ? ImageStretch.UniformToFill : ImageStretch.Uniform;
            DrawImageToPdf(graphics, new ImageElement { SourcePath = svg.SourcePath, Bounds = svg.Bounds, Stretch = stretchEnum }, ptp);
        }
        else if (el is ContainerElement container)
        {
            foreach (var childId in container.ChildIds)
            {
                if (lookup.TryGetValue(childId, out var child) && child.Visible)
                    DrawElementToPdf(graphics, child, lookup, doc, ptp);
            }
        }
    }

    private void DrawBarcodeToPdf(PdfGraphics graphics, BarcodeElement b, float ptp)
    {
        var widthPx = Math.Max(1, (int)Math.Round(b.Bounds.Width));
        var heightPx = Math.Max(1, (int)Math.Round(b.Bounds.Height));
        var format = b.Symbology switch
        {
            BarcodeSymbology.Code39 => ZXingBarcodeFormat.CODE_39,
            BarcodeSymbology.QRCode => ZXingBarcodeFormat.QR_CODE,
            BarcodeSymbology.EAN13 => ZXingBarcodeFormat.EAN_13,
            BarcodeSymbology.EAN8 => ZXingBarcodeFormat.EAN_8,
            BarcodeSymbology.UPCA => ZXingBarcodeFormat.UPC_A,
            BarcodeSymbology.DataMatrix => ZXingBarcodeFormat.DATA_MATRIX,
            BarcodeSymbology.PDF417 => ZXingBarcodeFormat.PDF_417,
            BarcodeSymbology.Aztec => ZXingBarcodeFormat.AZTEC,
            BarcodeSymbology.ITF => ZXingBarcodeFormat.ITF,
            _ => ZXingBarcodeFormat.CODE_128
        };

        var bitmap = _barcode.Generate(b.Value, format, widthPx, heightPx);
        using var pngStream = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, pngStream).AsTask().GetAwaiter().GetResult();
        encoder.SetSoftwareBitmap(bitmap);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
        pngStream.Seek(0);

        using var managed = pngStream.AsStreamForRead();
        var barcodeImage = new PdfBitmap(managed);
        graphics.DrawImage(barcodeImage, new Syncfusion.Drawing.RectangleF(
            (float)b.Bounds.X * ptp,
            (float)b.Bounds.Y * ptp,
            (float)b.Bounds.Width * ptp,
            (float)b.Bounds.Height * ptp));

        if (b.TextPosition != BarcodeTextPosition.None)
        {
            var fontStyle = (b.TextBold, b.TextItalic) switch
            {
                (true, true)  => PdfFontStyle.Bold | PdfFontStyle.Italic,
                (true, false) => PdfFontStyle.Bold,
                (false, true) => PdfFontStyle.Italic,
                _             => PdfFontStyle.Regular
            };
            var font = new PdfStandardFont(PdfFontFamily.Helvetica, (float)b.TextFontSize * ptp, fontStyle);
            var textColor = ParsePdfColor(b.TextColor);
            var brush = new PdfSolidBrush(textColor);
            var textY = b.TextPosition switch
            {
                BarcodeTextPosition.Top => (float)b.Bounds.Y * ptp - (float)b.TextFontSize * ptp - 2,
                BarcodeTextPosition.Bottom => ((float)b.Bounds.Y + (float)b.Bounds.Height) * ptp + 2,
                _ => ((float)b.Bounds.Y + (float)b.Bounds.Height) * ptp + 2
            };
            graphics.DrawString(
                b.DisplayText,
                font,
                brush,
                new Syncfusion.Drawing.PointF((float)b.Bounds.X * ptp, textY));
        }
    }

    private static Syncfusion.Drawing.Color ParsePdfColor(string hex)
    {
        try
        {
            if (!string.IsNullOrEmpty(hex))
            {
                var h = hex.TrimStart('#');
                if (h.Length == 6)
                    return Syncfusion.Drawing.Color.FromArgb(255,
                        Convert.ToByte(h[..2], 16),
                        Convert.ToByte(h[2..4], 16),
                        Convert.ToByte(h[4..6], 16));
            }
        }
        catch { }
        return Syncfusion.Drawing.Color.Black;
    }

    private void DrawShapeToPdf(PdfGraphics graphics, ShapeElement shape, float ptp)
    {
        var b = shape.Bounds;
        var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(255, 200, 200, 200));
        var pen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(255, 0, 0, 0), (float)shape.StrokeWidth * ptp);

        if (shape.Type == ShapeType.Ellipse)
        {
            graphics.DrawEllipse(brush, (float)b.X * ptp, (float)b.Y * ptp, (float)b.Width * ptp, (float)b.Height * ptp);
            graphics.DrawEllipse(pen, (float)b.X * ptp, (float)b.Y * ptp, (float)b.Width * ptp, (float)b.Height * ptp);
        }
        else
        {
            graphics.DrawRectangle(brush, (float)b.X * ptp, (float)b.Y * ptp, (float)b.Width * ptp, (float)b.Height * ptp);
            graphics.DrawRectangle(pen, (float)b.X * ptp, (float)b.Y * ptp, (float)b.Width * ptp, (float)b.Height * ptp);
        }
    }

    private void DrawImageToPdf(PdfGraphics graphics, ImageElement image, float ptp)
    {
        try
        {
            if (!string.IsNullOrEmpty(image.SourcePath) && System.IO.File.Exists(image.SourcePath))
            {
                var brush = new PdfSolidBrush(Syncfusion.Drawing.Color.White);
                graphics.DrawRectangle(brush, (float)image.Bounds.X * ptp, (float)image.Bounds.Y * ptp,
                    (float)image.Bounds.Width * ptp, (float)image.Bounds.Height * ptp);

                var pen = new PdfPen(Syncfusion.Drawing.Color.LightGray, 1);
                graphics.DrawRectangle(pen, (float)image.Bounds.X * ptp, (float)image.Bounds.Y * ptp,
                    (float)image.Bounds.Width * ptp, (float)image.Bounds.Height * ptp);

                var fileName = System.IO.Path.GetFileName(image.SourcePath);
                graphics.DrawString(fileName, new PdfStandardFont(PdfFontFamily.Helvetica, 8 * ptp),
                    PdfBrushes.Gray, new Syncfusion.Drawing.PointF(((float)image.Bounds.X + 2) * ptp, ((float)image.Bounds.Y + 2) * ptp));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rendering image to PDF '{image.SourcePath}': {ex.Message}");
        }
    }

    private static float MillimetersToPoints(double mm) => (float)(mm * 2.83465);

    private static Dictionary<Guid, DesignElement> BuildElementLookup(SceneDocument document)
    {
        var lookup = new Dictionary<Guid, DesignElement>();
        foreach (var element in document.AllElements)
        {
            if (!lookup.ContainsKey(element.Id))
                lookup[element.Id] = element;
        }

        return lookup;
    }
}
