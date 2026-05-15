using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercent))]
    private double _zoomPercentValue = 100;

    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;

    public int ZoomPercent => (int)Math.Round(Zoom * 100);

    partial void OnZoomChanged(double value)
    {
        var clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (Math.Abs(clamped - value) > 0.001) Zoom = clamped;
        ZoomPercentValue = Zoom * 100;
    }

    public void ZoomToFit(double canvasWidth, double canvasHeight, double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0 || pageHeight <= 0 || canvasWidth <= 0 || canvasHeight <= 0) return;
        double margin = 40;
        double fitX = (canvasWidth - margin * 2) / pageWidth;
        double fitY = (canvasHeight - margin * 2) / pageHeight;
        Zoom = Math.Min(fitX, fitY);
        OffsetX = -(pageWidth * Zoom - canvasWidth) / 2;
        OffsetY = -(pageHeight * Zoom - canvasHeight) / 2;
    }

    public Windows.Foundation.Point ScreenToWorld(Windows.Foundation.Point screen) =>
        new((screen.X + OffsetX) / Zoom, (screen.Y + OffsetY) / Zoom);

    public Windows.Foundation.Point WorldToScreen(Windows.Foundation.Point world) =>
        new(world.X * Zoom - OffsetX, world.Y * Zoom - OffsetY);

    public PointD WorldToScreenD(PointD world) =>
        new(world.X * Zoom - OffsetX, world.Y * Zoom - OffsetY);
}
