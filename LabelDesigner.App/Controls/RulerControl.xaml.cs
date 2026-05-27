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
        ds.Clear(Color.FromArgb(255, 245, 245, 245));

        var vp = Viewport;
        double ppm = PixelsPerMm;
        float canvasLen = IsVertical ? (float)sender.ActualHeight : (float)sender.ActualWidth;
        float rulerDim = IsVertical ? (float)sender.ActualWidth : (float)sender.ActualHeight;

        // Visible range in world pixels
        double worldStart = IsVertical
            ? (vp?.ScreenToWorld(new Point(0, 0)).Y ?? 0)
            : (vp?.ScreenToWorld(new Point(0, 0)).X ?? 0);
        double worldEnd = IsVertical
            ? (vp?.ScreenToWorld(new Point(0, canvasLen)).Y ?? 0)
            : (vp?.ScreenToWorld(new Point(canvasLen, 0)).X ?? 0);
        if (worldEnd < worldStart)
            (worldStart, worldEnd) = (worldEnd, worldStart);

        double pageOriginPx = IsVertical ? (vp?.PageOriginY ?? 0) : (vp?.PageOriginX ?? 0);

        // Convert visible range to mm relative to page origin
        double startMm = (worldStart - pageOriginPx) / ppm;
        double endMm = (worldEnd - pageOriginPx) / ppm;
        double rangeMm = endMm - startMm;

        // Choose tick intervals (in mm) based on visible range
        double minorMm, majorMm, labelMm;
        if (rangeMm < 30)       { minorMm = 1;  majorMm = 5;  labelMm = 10; }
        else if (rangeMm < 80)  { minorMm = 1;  majorMm = 10; labelMm = 10; }
        else if (rangeMm < 200) { minorMm = 2;  majorMm = 10; labelMm = 20; }
        else if (rangeMm < 500) { minorMm = 5;  majorMm = 25; labelMm = 50; }
        else                    { minorMm = 10; majorMm = 50; labelMm = 100; }

        double alignedStart = Math.Floor(startMm / minorMm) * minorMm;
        var textFormat = new CanvasTextFormat { FontSize = 8, FontFamily = "Segoe UI" };

        for (double mm = alignedStart; mm <= endMm + minorMm; mm += minorMm)
        {
            double worldPx = mm * ppm + pageOriginPx;
            double sp = IsVertical
                ? (vp?.WorldToScreen(new Point(0, worldPx)).Y ?? 0)
                : (vp?.WorldToScreen(new Point(worldPx, 0)).X ?? 0);
            if (sp < -5 || sp > canvasLen + 5) continue;

            double absMm = Math.Abs(mm % labelMm);
            bool isLabel  = absMm < minorMm * 0.5 || absMm > labelMm - minorMm * 0.5;
            double absMajor = Math.Abs(mm % majorMm);
            bool isMajor  = absMajor < minorMm * 0.5 || absMajor > majorMm - minorMm * 0.5;
            double absMid = Math.Abs(mm % (majorMm / 2.0));
            bool isMid    = !isMajor && (absMid < minorMm * 0.5 || absMid > majorMm / 2.0 - minorMm * 0.5);

            float tickLen = isLabel ? rulerDim * 0.7f : isMajor ? rulerDim * 0.55f : isMid ? rulerDim * 0.4f : rulerDim * 0.25f;
            Color tc = isMajor || isLabel ? Color.FromArgb(255, 90, 90, 90) : Color.FromArgb(255, 160, 160, 160);

            if (IsVertical)
            {
                // Ticks from right edge inward
                ds.DrawLine(rulerDim - tickLen, (float)sp, rulerDim - 1, (float)sp, tc, 1);
                if (isLabel)
                {
                    var label = FormatMeasurementLabel(mm);
                    ds.DrawText(label, 2, (float)sp + 1, Color.FromArgb(255, 70, 70, 70), textFormat);
                }
            }
            else
            {
                // Ticks from top edge downward
                ds.DrawLine((float)sp, 0, (float)sp, tickLen, tc, 1);
                if (isLabel)
                {
                    var label = FormatMeasurementLabel(mm);
                    ds.DrawText(label, (float)sp + 2, 1, Color.FromArgb(255, 70, 70, 70), textFormat);
                }
            }
        }

        // Guide drag preview
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

        // Border line on the canvas-facing edge
        if (IsVertical)
            ds.DrawLine(rulerDim - 1, 0, rulerDim - 1, canvasLen, Color.FromArgb(255, 180, 180, 180), 1);
        else
            ds.DrawLine(0, rulerDim - 1, canvasLen, rulerDim - 1, Color.FromArgb(255, 180, 180, 180), 1);
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
            MeasurementUnit.Millimeters => $"{millimeters:0}",
            MeasurementUnit.Centimeters => $"{(millimeters / 10.0):0.#}",
            MeasurementUnit.Inches => $"{(millimeters / 25.4):0.##}",
            _ => $"{millimeters:0}"
        };
    }
}
