using LabelDesigner.App.ViewModels;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

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
        ds.Clear(Color.FromArgb(255, 240, 240, 240));

        double pixelsPerMm = 3.78;
        var vp = Viewport;
        double offset = IsVertical ? (vp?.OffsetY ?? 0) : (vp?.OffsetX ?? 0);
        double zoom = vp?.Zoom ?? 1.0;
        double tickInterval = zoom < 0.5 ? 100 : zoom < 1.0 ? 50 : zoom < 2.0 ? 25 : 10;
        double labelInterval = 100;
        float canvasLen = IsVertical ? (float)sender.ActualHeight : (float)sender.ActualWidth;
        float rulerDim = IsVertical ? (float)sender.ActualWidth : (float)sender.ActualHeight;

        for (double wp = 0; wp < 5000; wp += Math.Max(tickInterval, 5))
        {
            double sp = wp * zoom - offset;
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
                    var mm = (wp / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, 2, (float)sp - 5.5f, Colors.DarkGray, new CanvasTextFormat { FontSize = 9 });
                }
            }
            else
            {
                ds.DrawLine((float)sp, rulerDim - ts, (float)sp, rulerDim, tc, 1);
                if (major)
                {
                    var mm = (wp / pixelsPerMm).ToString("0");
                    ds.DrawText(mm, (float)sp - 5, rulerDim - 14, Colors.DarkGray, new CanvasTextFormat { FontSize = 9 });
                }
            }
        }

        if (IsVertical)
            ds.DrawLine(rulerDim - 1, 0, rulerDim - 1, canvasLen, Colors.LightGray, 1);
        else
            ds.DrawLine(0, rulerDim - 1, canvasLen, rulerDim - 1, Colors.LightGray, 1);
    }
}
