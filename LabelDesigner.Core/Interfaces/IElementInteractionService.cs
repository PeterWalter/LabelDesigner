using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Interfaces;

public interface IElementInteractionService
{
    bool IsDragging { get; }

    ResizeHandle GetHoverHandle(DesignElement? selected, PointD point, double zoom);
    void BeginDrag(PointD startPoint, DesignElement selected, ResizeHandle handle);
    InteractionUpdate UpdateDrag(PointD currentPoint, DesignElement selected, IEnumerable<RectD> otherBounds, RectD pageRect);
    void EndDrag();
}

public readonly record struct InteractionUpdate(
    RectD? Bounds,
    double? Rotation,
    IReadOnlyList<GuideLine> Guides);
