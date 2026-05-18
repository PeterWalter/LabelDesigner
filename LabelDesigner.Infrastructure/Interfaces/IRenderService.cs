using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using Microsoft.Graphics.Canvas;

namespace LabelDesigner.Infrastructure.Interfaces;

public interface IRenderService
{
    void RenderScene(
        CanvasDrawingSession ds,
        SceneDocument document,
        IEnumerable<Guid> selectedIds,
        IEnumerable<Guid> hoveredIds,
        float zoom,
        RectD viewport,
        double pixelsPerMm = 3.78,
        bool showGrid = true);

    /// <summary>Disposes and clears all cached GPU/CPU bitmaps. Call when a new document is loaded.</summary>
    void ClearBitmapCache();
}
