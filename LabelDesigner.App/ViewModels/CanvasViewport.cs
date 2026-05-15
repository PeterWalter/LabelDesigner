
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.App.ViewModels;

public partial class CanvasViewport : ObservableObject
{
    [ObservableProperty]
    private double _offsetX;

    [ObservableProperty]
    private double _offsetY;

    [ObservableProperty]
    private double _zoom = 1.0;

    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;

    public Windows.Foundation.Point ScreenToWorld(Windows.Foundation.Point screen) =>
        new((screen.X + OffsetX) / Zoom, (screen.Y + OffsetY) / Zoom);

    public Windows.Foundation.Point WorldToScreen(Windows.Foundation.Point world) =>
        new(world.X * Zoom - OffsetX, world.Y * Zoom - OffsetY);

    public PointD WorldToScreenD(PointD world) =>
        new(world.X * Zoom - OffsetX, world.Y * Zoom - OffsetY);
}
