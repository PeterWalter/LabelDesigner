using LabelDesigner.App.ViewModels;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabelDesigner.App.Controls;

public sealed partial class RulerControl : UserControl
{
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

    private static void OnViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RulerControl control && e.OldValue is CanvasViewport oldVp)
            oldVp.PropertyChanged -= control.OnViewportPropertyChanged;

        if (d is RulerControl c && e.NewValue is CanvasViewport newVp)
            newVp.PropertyChanged += c.OnViewportPropertyChanged;
    }

    private void OnViewportPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        InvalidateCanvas();
    }

    private void InvalidateCanvas()
    {
        // The CanvasControl is the only child; invalidate it
        if (Content is CanvasControl canvas)
            canvas.Invalidate();
    }

    public RulerControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Content is CanvasControl canvas)
                canvas.Invalidate();
        };
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        ds.Clear(Colors.White);

        double pixelsPerMm = 3.78;
        var vp = Viewport;

        double offset = IsVertical ? (vp?.OffsetY ?? 0) : (vp?.OffsetX ?? 0);
        double zoom = vp?.Zoom ?? 1.0;

        double tickInterval = zoom < 0.5 ? 40 : zoom < 1.5 ? 20 : 10;

        float canvasLength = IsVertical ? (float)sender.ActualHeight : (float)sender.ActualWidth;

        for (double worldPx = 0; worldPx < 5000; worldPx += tickInterval)
        {
            // Screen-space position
            double screenPos = IsVertical
                ? worldPx * zoom - offset
                : worldPx * zoom - offset;

            if (screenPos < 0 || screenPos > canvasLength) continue;

            bool major = (worldPx % 100) < tickInterval;
            bool medium = (worldPx % 50) < tickInterval;
            float tickSize = major ? 15f : medium ? 10f : 5f;

            if (IsVertical)
            {
                ds.DrawLine(0, (float)screenPos, tickSize, (float)screenPos, Colors.Black);
                if (major)
                {
                    var mm = (worldPx / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, 20, (float)screenPos - 6, Colors.Black);
                }
            }
            else
            {
                ds.DrawLine((float)screenPos, 0, (float)screenPos, tickSize, Colors.Black);
                if (major)
                {
                    var mm = (worldPx / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, (float)screenPos - 6, 15, Colors.Black);
                }
            }
        }

        ds.DrawText("mm", 2, 2, Colors.Gray);
    }
}
