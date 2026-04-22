using System.Numerics;
using LabelDesigner.Core;
using LabelDesigner.Core.Models;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using static System.Net.Mime.MediaTypeNames;

namespace LabelDesigner.Infrastructure;

public class RenderService : IRenderService
{
    private readonly IBarcodeService _barcode;

    public RenderService(IBarcodeService barcode)
    {
        _barcode = barcode;
    }

    public void Render(CanvasDrawingSession ds,
                       IEnumerable<DesignElement> elements,
                       DesignElement? selected,
                       IEnumerable<GuideLine>? guides)
    {
        // GRID
        int gridSize = 20;

        for (int x = 0; x < 2000; x += gridSize)
        {
            ds.DrawLine(x, 0, x, 2000, Colors.LightGray);
        }

        for (int y = 0; y < 2000; y += gridSize)
        {
            ds.DrawLine(0, y, 2000, y, Colors.LightGray);
        }
        // Clear canvas
        ds.Clear(Colors.White);

        foreach (var el in elements)
        {
            if (el is BarcodeElement b)
            {
                // ✅ Generate bitmap
                var bmp = _barcode.Generate(
                    b.Value,
                    ZXing.BarcodeFormat.CODE_128,
                    (int)b.Bounds.Width,
                    (int)b.Bounds.Height);

                if (bmp != null)
                {
                    try
                    {
                        var device = ds.Device;

                        var img = CanvasBitmap.CreateFromSoftwareBitmap(device, bmp);

                        ds.DrawImage(img, el.Bounds.ToWinRect());
                    }
                    catch
                    {
                        // fallback if bitmap fails
                        ds.DrawRectangle(
                            (float)el.Bounds.X,
                            (float)el.Bounds.Y,
                            (float)el.Bounds.Width,
                            (float)el.Bounds.Height,
                            Colors.Red,
                            2);
                    }
                }
            }

            // ✅ Draw selection
            if (selected != null && el == selected)
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
            if (el is TextElement txt)
            {
                ds.DrawText(
                    txt.Text,
                    (float)txt.Bounds.X,
                    (float)txt.Bounds.Y,
                    Colors.Black);
            }
        }

        // ✅ Draw guides
        if (guides != null)
        {
            foreach (var g in guides)
            {
                ds.DrawLine(
                    new Vector2((float)g.X1, (float)g.Y1),
                    new Vector2((float)g.X2, (float)g.Y2),
                    Colors.Red,
                    1);
            }
        }

        // ✅ Debug text (optional)
        ds.DrawText("Rendering OK", 20, 20, Colors.Green);

       
    }

}