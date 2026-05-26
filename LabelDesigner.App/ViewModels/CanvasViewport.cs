using CommunityToolkit.Mvvm.ComponentModel;
using LabelDesigner.Core.ValueObjects;
using System.Numerics;

namespace LabelDesigner.App.ViewModels;

public partial class CanvasViewport : ObservableObject
{
    [ObservableProperty]
    public partial double OffsetX { get; set; }

    [ObservableProperty]
    public partial double OffsetY { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; } = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomPercent))]
    public partial double ZoomPercentValue { get; set; } = 100;

    public const double MinZoom = 0.05;
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
        // Screen = (World - Offset) * Zoom. Offset is in world units.
        // Convert centered screen margins back into world-space by dividing by Zoom.
        var safeZoom = Math.Max(Zoom, MinZoom);
        OffsetX = (pageWidth * Zoom - canvasWidth) / (2 * safeZoom);
        OffsetY = (pageHeight * Zoom - canvasHeight) / (2 * safeZoom);
    }

    private Matrix3x2 GetViewportTransform() =>
        Matrix3x2.CreateTranslation(-(float)OffsetX, -(float)OffsetY) *
        Matrix3x2.CreateScale((float)Zoom);

    public Windows.Foundation.Point ScreenToWorld(Windows.Foundation.Point screen)
    {
        var transform = GetViewportTransform();
        if (!Matrix3x2.Invert(transform, out var inverse))
            return screen;

        var world = Vector2.Transform(new Vector2((float)screen.X, (float)screen.Y), inverse);
        return new Windows.Foundation.Point(world.X, world.Y);
    }

    public Windows.Foundation.Point WorldToScreen(Windows.Foundation.Point world)
    {
        var screen = Vector2.Transform(new Vector2((float)world.X, (float)world.Y), GetViewportTransform());
        return new Windows.Foundation.Point(screen.X, screen.Y);
    }

    public PointD WorldToScreenD(PointD world)
    {
        var screen = Vector2.Transform(new Vector2((float)world.X, (float)world.Y), GetViewportTransform());
        return new PointD(screen.X, screen.Y);
    }

    [ObservableProperty]
    public partial double PageOriginX { get; set; } = 0;

    [ObservableProperty]
    public partial double PageOriginY { get; set; } = 0;
}
