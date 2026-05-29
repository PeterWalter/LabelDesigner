using System.Numerics;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
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
    private readonly ISvgService _svg;

    public RenderService(IBarcodeService barcode, ISvgService svg)
    {
        _barcode = barcode;
        _svg = svg;
    }

    public void ClearBitmapCache() => ElementRenderer.ClearBarcodeCache();

    public void RenderScene(
        CanvasDrawingSession ds,
        SceneDocument document,
        IEnumerable<Guid> selectedIds,
        IEnumerable<Guid> hoveredIds,
        float zoom,
        RectD viewport,
        double pixelsPerMm,
        bool showGrid = true,
        IEnumerable<GuideLine>? guides = null)
    {
        ds.Clear(Colors.White);

        var selectedSet = new HashSet<Guid>(selectedIds);
        var hoveredSet = new HashSet<Guid>(hoveredIds);
        var originalTransform = ds.Transform;
        ds.Transform = Matrix3x2.CreateTranslation(-(float)viewport.X, -(float)viewport.Y)
                     * Matrix3x2.CreateScale(zoom);

        float pageW = NormalizePageDimension((float)document.Page.WidthMm * (float)pixelsPerMm);
        float pageH = NormalizePageDimension((float)document.Page.HeightMm * (float)pixelsPerMm);
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
        var marginLeft = NormalizeCoordinate((float)m.Left * (float)pixelsPerMm);
        var marginTop = NormalizeCoordinate((float)m.Top * (float)pixelsPerMm);
        var marginRight = NormalizeCoordinate((float)m.Right * (float)pixelsPerMm);
        var marginBottom = NormalizeCoordinate((float)m.Bottom * (float)pixelsPerMm);
        var marginWidth = Math.Max(1f, pageW - marginLeft - marginRight);
        var marginHeight = Math.Max(1f, pageH - marginTop - marginBottom);
        ds.DrawRectangle(marginLeft, marginTop, marginWidth, marginHeight, Colors.Gray, 1);

        ds.DrawRectangle(0, 0, pageW, pageH, Colors.Black, 2);

        var lookup = BuildElementLookup(document);

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
                try
                {
                    ds.Transform = el.GetLocalTransform() * local;
                    ElementRenderer.DrawElement(ds, el, lookup, _barcode, _svg);
                    if (selectedSet.Contains(el.Id))
                        DrawSelectionHandles(ds, el, zoom);
                    else if (hoveredSet.Contains(el.Id) && !selectedSet.Contains(el.Id))
                        DrawHoverOutline(ds, el, zoom);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Render skipped for element '{el.Id}': {ex.Message}");
                }
                finally
                {
                    ds.Transform = local;
                }
            }
        }

        if (guides != null)
        {
            var guideColor = Color.FromArgb(180, 0, 160, 240);
            var guideStroke = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
            foreach (var guide in guides)
            {
                float guidePosPixels = (float)(guide.PositionMm * pixelsPerMm);
                if (guide.IsHorizontal)
                    ds.DrawLine(0, guidePosPixels, pageW, guidePosPixels, guideColor, 1f / zoom, guideStroke);
                else
                    ds.DrawLine(guidePosPixels, 0, guidePosPixels, pageH, guideColor, 1f / zoom, guideStroke);
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

    private static float NormalizePageDimension(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            return 100f;
        return Math.Clamp(value, 1f, 20000f);
    }

    private static float NormalizeCoordinate(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;
        return Math.Clamp(value, -100000f, 100000f);
    }
}
