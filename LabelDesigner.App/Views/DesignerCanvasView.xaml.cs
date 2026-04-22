using LabelDesigner.App.ViewModels;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.Infrastructure;        // ✔ must exist now
using LabelDesigner.Infrastructure.Services;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using LabelDesigner.Core.Enums;

namespace LabelDesigner.App.Views;

public sealed partial class DesignerCanvasView : UserControl
{
    private ResizeHandle _currentCursorHandle = ResizeHandle.None;
    public DesignerViewModel VM =>
        ((MainViewModel)App.Host.Services.GetService(typeof(MainViewModel))).Designer;

    private readonly IRenderService _renderer =
        App.Host.Services.GetService(typeof(IRenderService)) as IRenderService;

    public DesignerCanvasView()
    {
        InitializeComponent();
    }

    //private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    //{
    // _renderer.Render(
    //args.DrawingSession,
    //VM.Elements,
    //VM.Selected,
    //VM.Guides);
    //}
    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;

        ds.Clear(Microsoft.UI.Colors.White);

        // DEBUG TEXT
        ds.DrawText("Canvas Working", 100, 100, Microsoft.UI.Colors.Black);

        if (VM == null) return;

        _renderer.Render(
            ds,
            VM.Elements,
            VM.Selected,
            VM.Guides);
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

        (sender as CanvasControl)?.Invalidate();
    }

    private void UpdateCursor(ResizeHandle handle)
    {
        if (_currentCursorHandle == handle)
            return; // 🔥 prevents flicker

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

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint((UIElement)sender).Position;

        var handle = VM?.GetHoverHandle(point) ?? ResizeHandle.None;

        UpdateCursor(handle);

        VM?.PointerMoved(point);

        (sender as CanvasControl)?.Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _currentCursorHandle = ResizeHandle.None;
        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    }
}