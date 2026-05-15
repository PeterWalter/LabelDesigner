using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Services;
using Microsoft.UI;
using Windows.Foundation;

namespace LabelDesigner.App.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    private readonly ISceneGraphService _scene;
    private readonly IUndoRedoService _undoRedo;
    private readonly ILabelPersistenceService _persistence;

    public CanvasViewport Viewport { get; } = new();
    public ISceneGraphService Scene => _scene;

    public double Margin = 40;
    public PageSize CurrentPageSize { get; private set; } = PageSize.A4;
    public bool IsLandscape { get; private set; } = false;

    private readonly SnapService _snap = new();
    private bool _isDragging;
    private ResizeHandle _activeHandle = ResizeHandle.None;
    private PointD _startPointD;
    private RectD _originalBounds;

    public RectD PageBounds { get; set; } = new(50, 50, 800, 1100);
    public List<GuideLine> Guides { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarcodeSelected))]
    [NotifyPropertyChangedFor(nameof(IsTextSelected))]
    [NotifyPropertyChangedFor(nameof(IsElementSelected))]
    private DesignElement? selected;

    public bool IsElementSelected => Selected != null;
    public bool IsBarcodeSelected => Selected is BarcodeElement;
    public bool IsTextSelected => Selected is TextElement;

    private string? _currentFilePath;

    public DesignerViewModel(
        ISceneGraphService scene,
        IUndoRedoService undoRedo,
        ILabelPersistenceService persistence)
    {
        _scene = scene;
        _undoRedo = undoRedo;
        _persistence = persistence;

        SetPage(PageSize.A4, false);

        var layer = _scene.AddLayer("Default");

        _scene.AddElement(new BarcodeElement
        {
            Bounds = new RectD(100, 100, 200, 100),
            Value = "ABC123456",
            TextPosition = BarcodeTextPosition.Top
        }, layer.Id);

        _scene.AddElement(new TextElement
        {
            Bounds = new RectD(200, 300, 200, 50),
            Text = "Hello World"
        }, layer.Id);
    }

    // ─── File Commands ──────────────────────────────

    [RelayCommand]
    private async Task NewDocument()
    {
        _scene.Clear();
        _scene.AddLayer("Default");
        _currentFilePath = null;
    }

    [RelayCommand]
    private async Task OpenDocument()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        //var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Host.Services.GetRequiredService<MainWindow>());
       // WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        picker.FileTypeFilter.Add(".ldlabel");
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var doc = await _persistence.LoadAsync(file.Path);
        _scene.Load(doc);
        _currentFilePath = file.Path;
    }

    [RelayCommand]
    private async Task SaveDocument()
    {
        if (_currentFilePath == null)
        {
            await SaveAsDocument();
            return;
        }
        await _persistence.SaveAsync(_scene.CurrentDocument, _currentFilePath);
    }

    [RelayCommand]
    private async Task SaveAsDocument()
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        picker.FileTypeChoices.Add("LabelDesigner Document", new[] { ".ldlabel" });
        picker.SuggestedFileName = "Untitled.ldlabel";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        await _persistence.SaveAsync(_scene.CurrentDocument, file.Path);
        _currentFilePath = file.Path;
    }

    // ─── Undo/Redo ──────────────────────────────────

    [RelayCommand]
    private void Undo()
    {
        _undoRedo.Undo();
        Selected = _scene.SingleSelected;
    }

    [RelayCommand]
    private void Redo()
    {
        _undoRedo.Redo();
        Selected = _scene.SingleSelected;
    }

    // ─── Element Commands ──────────────────────────

    [RelayCommand]
    private void AddBarcode()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new BarcodeElement
        {
            Bounds = new RectD(50, 50, 200, 100)
        }, layerId);
    }

    [RelayCommand]
    private void AddText()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new TextElement
        {
            Bounds = new RectD(50, 50, 200, 30)
        }, layerId);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected != null)
            _scene.RemoveElement(Selected.Id);
    }

    // ─── Barcode Text Position ─────────────────────

    [RelayCommand]
    private void SetTextTop() { if (Selected is BarcodeElement b) b.TextPosition = BarcodeTextPosition.Top; }

    [RelayCommand]
    private void SetTextBottom() { if (Selected is BarcodeElement b) b.TextPosition = BarcodeTextPosition.Bottom; }

    [RelayCommand]
    private void SetTextLeft() { if (Selected is BarcodeElement b) b.TextPosition = BarcodeTextPosition.Left; }

    [RelayCommand]
    private void SetTextRight() { if (Selected is BarcodeElement b) b.TextPosition = BarcodeTextPosition.Right; }

    [RelayCommand]
    private void IncreaseFont() { if (Selected is TextElement t) t.FontSize += 2; }

    [RelayCommand]
    private void DecreaseFont() { if (Selected is TextElement t) t.FontSize -= 2; }

    // ─── Page ──────────────────────────────────────

    public void ToggleOrientation()
    {
        PageBounds = new RectD(PageBounds.X, PageBounds.Y, PageBounds.Height, PageBounds.Width);
    }

    public void SetPage(PageSize size, bool landscape)
    {
        CurrentPageSize = size;
        IsLandscape = landscape;

        (double w, double h) = size switch
        {
            PageSize.A5 => (420, 595),
            PageSize.A4 => (595, 842),
            PageSize.A3 => (842, 1191),
            _ => (595, 842)
        };

        if (landscape)
            (w, h) = (h, w);

        PageBounds = new RectD(50, 50, w, h);
    }

    // ─── Pointer Handling ──────────────────────────

    public void PointerPressed(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);
        _startPointD = pD;
        _isDragging = true;

        var hit = _scene.HitTest(pD);

        if (hit == null)
        {
            Selected = null;
            _scene.ClearSelection();
            _activeHandle = ResizeHandle.None;
            _isDragging = false;
            return;
        }

        _scene.ClearSelection();
        _scene.Select(hit.Id);
        Selected = hit;
        _activeHandle = GetHoverHandle(pD);
        _originalBounds = hit.Bounds;
    }

    public void PointerMoved(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);

        if (!_isDragging || Selected == null)
            return;

        var dx = pD.X - _startPointD.X;
        var dy = pD.Y - _startPointD.Y;

        if (_activeHandle == ResizeHandle.None || _activeHandle == ResizeHandle.Move)
        {
            Selected.Bounds = SnapToGrid(_originalBounds.Translate(dx, dy));
        }
        else
        {
            Resize(dx, dy);
        }
    }

    public void PointerReleased()
    {
        _isDragging = false;
        _activeHandle = ResizeHandle.None;
    }

    // ─── Resize ────────────────────────────────────

    private RectD SnapToGrid(RectD rect)
    {
        int grid = 20;
        return new RectD(
            Math.Round(rect.X / grid) * grid,
            Math.Round(rect.Y / grid) * grid,
            rect.Width,
            rect.Height);
    }

    private void Resize(double dx, double dy)
    {
        var b = _originalBounds;
        double minSize = 20;

        switch (_activeHandle)
        {
            case ResizeHandle.TopLeft:
                b = new RectD(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy);
                break;
            case ResizeHandle.Top:
                b = new RectD(b.X, b.Y + dy, b.Width, b.Height - dy);
                break;
            case ResizeHandle.TopRight:
                b = new RectD(b.X, b.Y + dy, b.Width + dx, b.Height - dy);
                break;
            case ResizeHandle.Right:
                b = new RectD(b.X, b.Y, b.Width + dx, b.Height);
                break;
            case ResizeHandle.BottomRight:
                b = new RectD(b.X, b.Y, b.Width + dx, b.Height + dy);
                break;
            case ResizeHandle.Bottom:
                b = new RectD(b.X, b.Y, b.Width, b.Height + dy);
                break;
            case ResizeHandle.BottomLeft:
                b = new RectD(b.X + dx, b.Y, b.Width - dx, b.Height + dy);
                break;
            case ResizeHandle.Left:
                b = new RectD(b.X + dx, b.Y, b.Width - dx, b.Height);
                break;
        }

        if (b.Width < minSize) b.Width = minSize;
        if (b.Height < minSize) b.Height = minSize;

        Selected!.Bounds = b;
    }

    public ResizeHandle GetHoverHandle(PointD pD)
    {
        if (Selected == null) return ResizeHandle.None;

        var b = Selected.Bounds;
        const double size = 8;

        bool Near(double x, double y)
            => Math.Abs(pD.X - x) < size && Math.Abs(pD.Y - y) < size;

        if (Near(b.X, b.Y)) return ResizeHandle.TopLeft;
        if (Near(b.X + b.Width, b.Y)) return ResizeHandle.TopRight;
        if (Near(b.X + b.Width, b.Y + b.Height)) return ResizeHandle.BottomRight;
        if (Near(b.X, b.Y + b.Height)) return ResizeHandle.BottomLeft;

        const double edgeTolerance = 10;

        if (Math.Abs(pD.Y - b.Y) <= edgeTolerance && pD.X >= b.X && pD.X <= b.X + b.Width)
            return ResizeHandle.Top;
        if (Math.Abs(pD.X - (b.X + b.Width)) <= edgeTolerance && pD.Y >= b.Y && pD.Y <= b.Y + b.Height)
            return ResizeHandle.Right;
        if (Math.Abs(pD.Y - (b.Y + b.Height)) <= edgeTolerance && pD.X >= b.X && pD.X <= b.X + b.Width)
            return ResizeHandle.Bottom;
        if (Math.Abs(pD.X - b.X) <= edgeTolerance && pD.Y >= b.Y && pD.Y <= b.Y + b.Height)
            return ResizeHandle.Left;

        if (pD.X >= b.X && pD.X <= b.X + b.Width && pD.Y >= b.Y && pD.Y <= b.Y + b.Height)
            return ResizeHandle.Move;

        return ResizeHandle.None;
    }
}
