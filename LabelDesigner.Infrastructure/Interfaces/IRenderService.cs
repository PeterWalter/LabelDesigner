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
        float zoom,
        RectD viewport);
}
