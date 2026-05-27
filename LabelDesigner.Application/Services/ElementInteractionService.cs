using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Application.Services;

public class ElementInteractionService : IElementInteractionService
{
    private readonly ISnapService _snapService;
    private bool _isDragging;
    private ResizeHandle _activeHandle = ResizeHandle.None;
    private PointD _startPoint;
    private RectD _originalBounds;
    private double _rotationStartAngle;

    public ElementInteractionService(ISnapService snapService)
    {
        _snapService = snapService;
    }

    public bool IsDragging => _isDragging;
    public bool SnapEnabled { get; set; } = true;

    public ResizeHandle GetHoverHandle(DesignElement? selected, PointD point, double zoom)
    {
        if (selected == null)
        {
            return ResizeHandle.None;
        }

        var rotationHandleRect = GetRotationHandleRect(selected.Bounds, zoom);
        if (rotationHandleRect.Contains(point))
        {
            return ResizeHandle.Rotate;
        }

        var bounds = selected.Bounds;
        const double cornerSize = 8;

        bool Near(double x, double y)
            => Math.Abs(point.X - x) < cornerSize && Math.Abs(point.Y - y) < cornerSize;

        if (Near(bounds.X, bounds.Y)) return ResizeHandle.TopLeft;
        if (Near(bounds.X + bounds.Width, bounds.Y)) return ResizeHandle.TopRight;
        if (Near(bounds.X + bounds.Width, bounds.Y + bounds.Height)) return ResizeHandle.BottomRight;
        if (Near(bounds.X, bounds.Y + bounds.Height)) return ResizeHandle.BottomLeft;

        const double edgeTolerance = 10;

        if (Math.Abs(point.Y - bounds.Y) <= edgeTolerance && point.X >= bounds.X && point.X <= bounds.X + bounds.Width)
            return ResizeHandle.Top;
        if (Math.Abs(point.X - (bounds.X + bounds.Width)) <= edgeTolerance && point.Y >= bounds.Y && point.Y <= bounds.Y + bounds.Height)
            return ResizeHandle.Right;
        if (Math.Abs(point.Y - (bounds.Y + bounds.Height)) <= edgeTolerance && point.X >= bounds.X && point.X <= bounds.X + bounds.Width)
            return ResizeHandle.Bottom;
        if (Math.Abs(point.X - bounds.X) <= edgeTolerance && point.Y >= bounds.Y && point.Y <= bounds.Y + bounds.Height)
            return ResizeHandle.Left;

        if (bounds.Contains(point))
            return ResizeHandle.Move;

        return ResizeHandle.None;
    }

    public void BeginDrag(PointD startPoint, DesignElement selected, ResizeHandle handle)
    {
        _isDragging = true;
        _activeHandle = handle;
        _startPoint = startPoint;
        _originalBounds = selected.Bounds;
        _rotationStartAngle = selected.Rotation;
    }

    public InteractionUpdate UpdateDrag(PointD currentPoint, DesignElement selected, IEnumerable<RectD> otherBounds, RectD pageRect, double pixelsPerMm)
    {
        if (!_isDragging)
        {
            return new InteractionUpdate(null, null, Array.Empty<GuideLine>());
        }

        var dx = currentPoint.X - _startPoint.X;
        var dy = currentPoint.Y - _startPoint.Y;

        if (_activeHandle == ResizeHandle.Rotate)
        {
            var centerX = selected.Bounds.CenterX;
            var centerY = selected.Bounds.CenterY;
            var cursorAngle = Math.Atan2(currentPoint.Y - centerY, currentPoint.X - centerX) * 180.0 / Math.PI;
            var startAngle = Math.Atan2(_startPoint.Y - centerY, _startPoint.X - centerX) * 180.0 / Math.PI;
            var rotation = _rotationStartAngle + (cursorAngle - startAngle);
            return new InteractionUpdate(null, rotation % 360, Array.Empty<GuideLine>());
        }

        if (_activeHandle == ResizeHandle.None || _activeHandle == ResizeHandle.Move)
        {
            var movedBounds = _originalBounds.Translate(dx, dy);
            IReadOnlyList<GuideLine> guides = Array.Empty<GuideLine>();
            var snappedBounds = movedBounds;
            if (SnapEnabled)
            {
                snappedBounds = _snapService.Snap(movedBounds, otherBounds, pixelsPerMm, out guides);
            }
            var clampedBounds = snappedBounds.ClampToBounds(pageRect);
            return new InteractionUpdate(clampedBounds, null, guides);
        }

        var resizedBounds = Resize(_activeHandle, _originalBounds, dx, dy)
            .EnsureMinimumSize(20, 20)
            .ClampToBounds(pageRect);

        if (SnapEnabled)
        {
            resizedBounds = SnapResizeToGrid(resizedBounds, _activeHandle)
                .ClampToBounds(pageRect);
        }

        return new InteractionUpdate(resizedBounds, null, Array.Empty<GuideLine>());
    }

    public void EndDrag()
    {
        _isDragging = false;
        _activeHandle = ResizeHandle.None;
    }

    private static RectD GetRotationHandleRect(RectD bounds, double zoom)
    {
        var zoomFactor = Math.Max(zoom, 0.25);
        var rotationOffset = 20.0 / zoomFactor;
        return new RectD(bounds.CenterX - 8, bounds.Y - rotationOffset - 8, 16, 16);
    }

    private static RectD Resize(ResizeHandle handle, RectD bounds, double dx, double dy)
    {
        return handle switch
        {
            ResizeHandle.TopLeft => new RectD(bounds.X + dx, bounds.Y + dy, bounds.Width - dx, bounds.Height - dy),
            ResizeHandle.Top => new RectD(bounds.X, bounds.Y + dy, bounds.Width, bounds.Height - dy),
            ResizeHandle.TopRight => new RectD(bounds.X, bounds.Y + dy, bounds.Width + dx, bounds.Height - dy),
            ResizeHandle.Right => new RectD(bounds.X, bounds.Y, bounds.Width + dx, bounds.Height),
            ResizeHandle.BottomRight => new RectD(bounds.X, bounds.Y, bounds.Width + dx, bounds.Height + dy),
            ResizeHandle.Bottom => new RectD(bounds.X, bounds.Y, bounds.Width, bounds.Height + dy),
            ResizeHandle.BottomLeft => new RectD(bounds.X + dx, bounds.Y, bounds.Width - dx, bounds.Height + dy),
            ResizeHandle.Left => new RectD(bounds.X + dx, bounds.Y, bounds.Width - dx, bounds.Height),
            _ => bounds
        };
    }

    private RectD SnapResizeToGrid(RectD bounds, ResizeHandle handle)
    {
        var grid = _snapService.GridSize;
        if (grid <= 0)
            return bounds;

        double x = bounds.X;
        double y = bounds.Y;
        double right = bounds.Right;
        double bottom = bounds.Bottom;

        static double Snap(double value, double gridSize) => Math.Round(value / gridSize) * gridSize;

        switch (handle)
        {
            case ResizeHandle.Left:
                x = Snap(bounds.Left, grid);
                break;
            case ResizeHandle.Top:
                y = Snap(bounds.Top, grid);
                break;
            case ResizeHandle.Right:
                right = Snap(bounds.Right, grid);
                break;
            case ResizeHandle.Bottom:
                bottom = Snap(bounds.Bottom, grid);
                break;
            case ResizeHandle.TopLeft:
                x = Snap(bounds.Left, grid);
                y = Snap(bounds.Top, grid);
                break;
            case ResizeHandle.TopRight:
                y = Snap(bounds.Top, grid);
                right = Snap(bounds.Right, grid);
                break;
            case ResizeHandle.BottomLeft:
                x = Snap(bounds.Left, grid);
                bottom = Snap(bounds.Bottom, grid);
                break;
            case ResizeHandle.BottomRight:
                right = Snap(bounds.Right, grid);
                bottom = Snap(bounds.Bottom, grid);
                break;
        }

        return new RectD(x, y, Math.Max(20, right - x), Math.Max(20, bottom - y));
    }
}
