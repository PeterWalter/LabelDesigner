using System.Numerics;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Common;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure.Rendering;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace LabelDesigner.Infrastructure;

public class RenderService : IRenderService
{
    private readonly IBarcodeService _barcode;

    public RenderService(IBarcodeService barcode)
    {
        _barcode = barcode;
    }

    public void ClearBitmapCache() => ElementRenderer.ClearBarcodeCache();

    public void RenderScene(
        CanvasDrawingSession ds,
        SceneDocument document,
        IEnumerable<Guid> selectedIds,
        IEnumerable<Guid> hoveredIds,
        float zoom,
        RectD viewport,
        bool showGrid = true)
    {
        ds.Clear(Colors.White);

        var selectedSet = new HashSet<Guid>(selectedIds);
        var hoveredSet = new HashSet<Guid>(hoveredIds);
        var originalTransform = ds.Transform;
        ds.Transform = Matrix3x2.CreateTranslation(-(float)viewport.X, -(float)viewport.Y)
                     * Matrix3x2.CreateScale(zoom);

        float pageW = (float)document.Page.WidthMm * 3.78f;
        float pageH = (float)document.Page.HeightMm * 3.78f;
        ds.FillRectangle(0, 0, pageW, pageH, Colors.White);

        if (showGrid && zoom > 0.5f)
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
                ElementRenderer.DrawElement(ds, el, lookup, _barcode);
                if (selectedSet.Contains(el.Id))
                    DrawSelectionHandles(ds, el, zoom);
                else if (hoveredSet.Contains(el.Id) && !selectedSet.Contains(el.Id))
                    DrawHoverOutline(ds, el, zoom);
                ds.Transform = local;
            }
        }
        ds.Transform = originalTransform;
    }

    private static void DrawSelectionHandles(CanvasDrawingSession ds, DesignElement el, float zoom)
    {
        var rect = el.Bounds.ToWinRect();
        float zf = Math.Max(zoom, 0.25f);
        float s = 6f / zf, es = 4f / zf, rotR = 5f / zf, rotOff = 20f / zf;

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

        float rotCX = (float)(rect.Left + rect.Width / 2);
        float rotCY = (float)(rect.Top - rotOff);
        ds.DrawLine(new Vector2(rotCX, (float)rect.Top), new Vector2(rotCX, rotCY), Colors.Blue, 1);
        ds.FillCircle(new Vector2(rotCX, rotCY), rotR, Colors.White);
        ds.DrawCircle(new Vector2(rotCX, rotCY), rotR, Colors.Blue);
        ds.DrawRectangle(rect, Colors.Blue, 1, new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash });
    }

    private static void DrawHoverOutline(CanvasDrawingSession ds, DesignElement el, float zoom)
    {
        var rect = el.Bounds.ToWinRect();
        ds.DrawRectangle(rect, Color.FromArgb(180, 128, 200, 255), 1.5f);
        float dot = 3f / Math.Max(zoom, 0.25f);
        foreach (var p in new[] {
            new Vector2((float)rect.Left, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Top),
            new Vector2((float)rect.Right, (float)rect.Bottom),
            new Vector2((float)rect.Left, (float)rect.Bottom),
        })
            ds.FillEllipse(p, dot, dot, Color.FromArgb(160, 128, 200, 255));
    }

    internal static Color ParseColor(string hex, Color fallback) =>
        ElementRenderer.ParseColor(hex, fallback);
}
