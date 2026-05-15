using LabelDesigner.App.ViewModels;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LabelDesigner.App.Views;

public sealed partial class DesignerCanvasView : UserControl
{
    private ResizeHandle _currentCursorHandle = ResizeHandle.None;

    public DesignerViewModel VM =>
        App.Services.GetRequiredService<DesignerViewModel>();

    private readonly IRenderService _renderer =
        App.Services.GetRequiredService<IRenderService>();

    public DesignerCanvasView()
    {
        InitializeComponent();
        this.PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (VM == null) return;

        var ds = args.DrawingSession;

        var viewport = new RectD(
            VM.Viewport.OffsetX,
            VM.Viewport.OffsetY,
            sender.ActualWidth,
            sender.ActualHeight);

        _renderer.RenderScene(
            ds,
            VM.Scene.CurrentDocument,
            VM.Scene.SelectedIds,
            (float)VM.Viewport.Zoom,
            viewport);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint((UIElement)sender).Position;
        VM?.PointerPressed(point);
        (sender as CanvasControl)?.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        VM?.PointerReleased();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint((UIElement)sender).Position;

        var worldPoint = VM?.Viewport.ScreenToWorld(point) ?? point;
        var pD = new PointD(worldPoint.X, worldPoint.Y);
        var handle = VM?.GetHoverHandle(pD) ?? ResizeHandle.None;

        UpdateCursor(handle);

        VM?.PointerMoved(point);

        (sender as CanvasControl)?.Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _currentCursorHandle = ResizeHandle.None;
        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint((UIElement)sender).Properties;
        int delta = props.MouseWheelDelta;

        bool ctrlHeld = InputKeyboardSource.GetKeyStateForCurrentThread(
            Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrlHeld)
        {
            var cursorPos = e.GetCurrentPoint((UIElement)sender).Position;
            var worldBefore = VM.Viewport.ScreenToWorld(cursorPos);

            double newZoom = VM.Viewport.Zoom + (delta > 0 ? 0.1 : -0.1);
            newZoom = Math.Clamp(newZoom, CanvasViewport.MinZoom, CanvasViewport.MaxZoom);
            VM.Viewport.Zoom = newZoom;

            var worldAfter = VM.Viewport.ScreenToWorld(cursorPos);
            VM.Viewport.OffsetX += (worldAfter.X - worldBefore.X) * VM.Viewport.Zoom;
            VM.Viewport.OffsetY += (worldAfter.Y - worldBefore.Y) * VM.Viewport.Zoom;
        }
        else
        {
            VM.Viewport.OffsetY -= delta / 2;
        }

        (sender as CanvasControl)?.Invalidate();
    }

    private void UpdateCursor(ResizeHandle handle)
    {
        if (_currentCursorHandle == handle)
            return;

        _currentCursorHandle = handle;

        InputSystemCursorShape shape = InputSystemCursorShape.Arrow;

        switch (handle)
        {
            case ResizeHandle.TopLeft:
            case ResizeHandle.BottomRight:
                shape = InputSystemCursorShape.SizeNorthwestSoutheast;
                break;
            case ResizeHandle.TopRight:
            case ResizeHandle.BottomLeft:
                shape = InputSystemCursorShape.SizeNortheastSouthwest;
                break;
            case ResizeHandle.Top:
            case ResizeHandle.Bottom:
                shape = InputSystemCursorShape.SizeNorthSouth;
                break;
            case ResizeHandle.Left:
            case ResizeHandle.Right:
                shape = InputSystemCursorShape.SizeWestEast;
                break;
            case ResizeHandle.Move:
                shape = InputSystemCursorShape.SizeAll;
                break;
        }

        this.ProtectedCursor = InputSystemCursor.Create(shape);
    }
}
