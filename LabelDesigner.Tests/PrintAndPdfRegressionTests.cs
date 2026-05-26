using System.Runtime.InteropServices.WindowsRuntime;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Barcode;
using LabelDesigner.Infrastructure.Export;
using LabelDesigner.Infrastructure.Interfaces;

namespace LabelDesigner.Tests;

public class PrintAndPdfRegressionTests
{
    private static SceneDocument BuildDocumentWithContent()
    {
        var layerId = Guid.NewGuid();
        var doc = new SceneDocument
        {
            Page = new PageNode
            {
                WidthMm = 100,
                HeightMm = 60,
                Dpi = 300,
                Margins = new Margins(0, 0, 0, 0)
            }
        };

        var layer = new LayerNode
        {
            Id = layerId,
            Name = "Main",
            Visible = true
        };
        doc.Layers.Add(layer);

        var shape = new ShapeElement
        {
            Bounds = new RectD(8, 8, 30, 20),
            Type = ShapeType.Rectangle,
            Fill = "#000000",
            Stroke = "#000000",
            StrokeWidth = 1
        };
        shape.ParentId = layerId;
        doc.AllElements.Add(shape);
        layer.ElementIds.Add(shape.Id);

        var bottomShape = new ShapeElement
        {
            Bounds = new RectD(8, 52, 80, 6),
            Type = ShapeType.Rectangle,
            Fill = "#000000",
            Stroke = "#000000",
            StrokeWidth = 1
        };
        bottomShape.ParentId = layerId;
        doc.AllElements.Add(bottomShape);
        layer.ElementIds.Add(bottomShape.Id);

        var barcode = new BarcodeElement
        {
            Bounds = new RectD(45, 8, 40, 20),
            Value = "ABC123456",
            Symbology = BarcodeSymbology.Code128,
            TextPosition = BarcodeTextPosition.None
        };
        barcode.ParentId = layerId;
        doc.AllElements.Add(barcode);
        layer.ElementIds.Add(barcode.Id);

        return doc;
    }

    [Fact]
    public async Task RenderDocumentToBitmapAsync_renders_non_white_content()
    {
        IBarcodeService barcode = new BarcodeService();
        ISvgService svg = new SvgService();
        IDocumentRasterizer rasterizer = new PrintService(barcode, svg);

        var doc = BuildDocumentWithContent();
        var bitmap = await rasterizer.RenderDocumentToBitmapAsync(doc, 150f);

        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyToBuffer(pixels.AsBuffer());

        var hasInk = false;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (r < 250 || g < 250 || b < 250)
            {
                hasInk = true;
                break;
            }
        }

        Assert.True(hasInk);
    }

    [Fact]
    public async Task PdfExport_writes_non_empty_file_with_bottom_content_document()
    {
        var service = new PdfExportService(new BarcodeService());
        var doc = BuildDocumentWithContent();
        var outputPath = Path.Combine(Path.GetTempPath(), $"labeldesigner-regression-{Guid.NewGuid():N}.pdf");

        try
        {
            await service.ExportAsync(doc, outputPath, new PdfExportOptions());

            Assert.True(File.Exists(outputPath));
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 500);

            var bytes = await File.ReadAllBytesAsync(outputPath);
            var text = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.Contains("/Subtype /Image", text);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }
}

