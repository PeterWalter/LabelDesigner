using LabelDesigner.App.ViewModels;
using LabelDesigner.App.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.UI;

namespace LabelDesigner.App.Controls;

public sealed partial class RulerControl : UserControl
{
    private bool _isDraggingGuide;
    private double _guideDragPreviewPos;

    public event EventHandler<double>? GuideCreated;

    public bool IsVertical { get; set; }

    public CanvasViewport Viewport
    {
        get => (CanvasViewport)GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    public static readonly DependencyProperty ViewportProperty =
        DependencyProperty.Register(
            nameof(Viewport),
            typeof(CanvasViewport),
            typeof(RulerControl),
            new PropertyMetadata(null, OnViewportChanged));

    public double PixelsPerMm
    {
        get => (double)GetValue(PixelsPerMmProperty);
        set => SetValue(PixelsPerMmProperty, value);
    }

    public static readonly DependencyProperty PixelsPerMmProperty =
        DependencyProperty.Register(
            nameof(PixelsPerMm),
            typeof(double),
            typeof(RulerControl),
            new PropertyMetadata(DpiService.PixelsPerMm, OnPixelsPerMmChanged));

    private static void OnPixelsPerMmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerControl control)
            control.RulerCanvas.Invalidate();
    }

    private static void OnViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerControl control && e.OldValue is CanvasViewport oldVp)
            oldVp.PropertyChanged -= control.OnViewportPropertyChanged;

        if (d is RulerControl c && e.NewValue is CanvasViewport newVp)
            newVp.PropertyChanged += c.OnViewportPropertyChanged;
    }

    private void OnViewportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RulerCanvas.Invalidate();

    public RulerControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppSettingsService.SettingsChanged += OnSettingsChanged;
            RulerCanvas.Invalidate();
        };
        Unloaded += (_, _) => AppSettingsService.SettingsChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged() => RulerCanvas.Invalidate();

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(Color.FromArgb(255, 240, 240, 240));

        double pixelsPerMm = PixelsPerMm;
        var vp = Viewport;
        double zoom = vp?.Zoom ?? 1.0;
        double tickInterval = zoom < 0.5 ? 100 : zoom < 1.0 ? 50 : zoom < 2.0 ? 25 : 10;
        double labelInterval = 100;
        float canvasLen = IsVertical ? (float)sender.ActualHeight : (float)sender.ActualWidth;
        float rulerDim = IsVertical ? (float)sender.ActualWidth : (float)sender.ActualHeight;

        double worldStart = IsVertical
            ? (vp?.ScreenToWorld(new Point(0, 0)).Y ?? 0)
            : (vp?.ScreenToWorld(new Point(0, 0)).X ?? 0);
        double worldEnd = IsVertical
            ? (vp?.ScreenToWorld(new Point(0, canvasLen)).Y ?? 0)
            : (vp?.ScreenToWorld(new Point(canvasLen, 0)).X ?? 0);

        if (worldEnd < worldStart)
            (worldStart, worldEnd) = (worldEnd, worldStart);

        double alignedStart = Math.Floor(worldStart / tickInterval) * tickInterval;
        double pageOrigin = IsVertical ? (vp?.PageOriginY ?? 0) : (vp?.PageOriginX ?? 0);

        for (double wp = alignedStart; wp <= worldEnd + tickInterval; wp += Math.Max(tickInterval, 5))
        {
            double sp = IsVertical
                ? (vp?.WorldToScreen(new Point(0, wp)).Y ?? 0)
                : (vp?.WorldToScreen(new Point(wp, 0)).X ?? 0);
            if (sp < -10 || sp > canvasLen + 10) continue;

            bool major = Math.Abs(wp % labelInterval) < Math.Max(tickInterval, 5) * 0.5;
            bool medium = Math.Abs(wp % 50) < Math.Max(tickInterval, 5) * 0.5 && !major;
            float ts = major ? 12f : medium ? 8f : 5f;
            Color tc = major ? Colors.DarkGray : Colors.Gray;

            if (IsVertical)
            {
                ds.DrawLine(rulerDim - ts, (float)sp, rulerDim, (float)sp, tc, 1);
                if (major)
                {
                    var label = FormatMeasurementLabel((wp - pageOrigin) / pixelsPerMm);
                    ds.DrawText(label, 2, (float)sp - 5.5f, Colors.DarkGray, new CanvasTextFormat { FontSize = 9 });
                }
            }
            else
            {
                ds.DrawLine((float)sp, rulerDim - ts, (float)sp, rulerDim, tc, 1);
                if (major)
                {
                    var label = FormatMeasurementLabel((wp - pageOrigin) / pixelsPerMm);
                    ds.DrawText(label, (float)sp - 5, rulerDim - 14, Colors.DarkGray, new CanvasTextFormat { FontSize = 9 });
                }
            }
        }

        if (_isDraggingGuide && vp != null)
        {
            double screenPos = IsVertical
                ? vp.WorldToScreen(new Point(0, _guideDragPreviewPos)).Y
                : vp.WorldToScreen(new Point(_guideDragPreviewPos, 0)).X;
            var previewColor = Color.FromArgb(200, 0, 160, 240);
            if (IsVertical)
                ds.DrawLine(0, (float)screenPos, rulerDim, (float)screenPos, previewColor, 2);
            else
                ds.DrawLine((float)screenPos, 0, (float)screenPos, rulerDim, previewColor, 2);
        }

        if (IsVertical)
            ds.DrawLine(rulerDim - 1, 0, rulerDim - 1, canvasLen, Colors.LightGray, 1);
        else
            ds.DrawLine(0, rulerDim - 1, canvasLen, rulerDim - 1, Colors.LightGray, 1);
    }

    private void OnRulerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingGuide = true;
        _guideDragPreviewPos = GetWorldPosition(e.GetCurrentPoint(RulerCanvas).Position);
        RulerCanvas.CapturePointer(e.Pointer);
        RulerCanvas.Invalidate();
    }

    private void OnRulerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingGuide)
            return;

        _guideDragPreviewPos = GetWorldPosition(e.GetCurrentPoint(RulerCanvas).Position);
        RulerCanvas.Invalidate();
    }

    private void OnRulerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingGuide)
            return;

        _guideDragPreviewPos = GetWorldPosition(e.GetCurrentPoint(RulerCanvas).Position);
        _isDraggingGuide = false;
        RulerCanvas.ReleasePointerCapture(e.Pointer);
        GuideCreated?.Invoke(this, _guideDragPreviewPos);
        RulerCanvas.Invalidate();
    }

    private double GetWorldPosition(Point point)
    {
        var worldPoint = Viewport?.ScreenToWorld(point) ?? point;
        return IsVertical ? worldPoint.Y : worldPoint.X;
    }

    private static string FormatMeasurementLabel(double millimeters)
    {
        return AppSettingsService.RulerUnit switch
        {
            MeasurementUnit.Millimeters => $"{millimeters:0} mm",
            MeasurementUnit.Centimeters => $"{(millimeters / 10.0):0.0} cm",
            MeasurementUnit.Inches => $"{(millimeters / 25.4):0.00} in",
            _ => $"{millimeters:0} mm"
        };
    }
}
