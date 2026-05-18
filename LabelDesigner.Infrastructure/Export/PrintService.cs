using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using System.Numerics;
using Windows.Graphics.Imaging;

namespace LabelDesigner.Infrastructure.Export;

public class PrintService : IPrintService, IDocumentRasterizer
{
    private readonly IBarcodeService _barcode;
    private readonly ISvgService _svg;

    public PrintService(IBarcodeService barcode, ISvgService svg)
    {
        _barcode = barcode;
        _svg = svg;
    }

    public async Task PrintAsync(SceneDocument document)
    {
        await RenderDocumentToBitmapAsync(document, 300);
    }

    public async Task ShowPrintPreviewAsync(SceneDocument document)
    {
        await Task.CompletedTask;
    }

    public async Task<SoftwareBitmap> RenderDocumentToBitmapAsync(SceneDocument document, float dpi)
    {
        double pageWidthPx  = document.Page.WidthMm  * dpi / 25.4;
        double pageHeightPx = document.Page.HeightMm * dpi / 25.4;

        using var device = CanvasDevice.GetSharedDevice();
        using var renderTarget = new CanvasRenderTarget(device,
            (int)pageWidthPx, (int)pageHeightPx, dpi);

        using var ds = renderTarget.CreateDrawingSession();
        ds.Clear(Colors.White);
        float scale = dpi / 96.0f;
        ds.Transform = Matrix3x2.CreateScale(scale);

        var lookup = document.AllElements.ToDictionary(e => e.Id);
        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;
            foreach (var id in layer.ElementIds)
            {
                if (!lookup.TryGetValue(id, out var el) || !el.Visible) continue;
                ElementRenderer.DrawElement(ds, el, lookup, _barcode, _svg, scale);
            }
        }

        var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Bmp);
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}
