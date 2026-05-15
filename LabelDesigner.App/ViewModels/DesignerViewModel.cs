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
    private bool _isShiftHeld;
    private ResizeHandle _activeHandle = ResizeHandle.None;
    private PointD _startPointD;
    private RectD _originalBounds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    private int _cursorWorldX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    private int _cursorWorldY;

    public int ElementCount => _scene.CurrentDocument.AllElements.Count;
    public string ZoomText => $"Zoom: {Viewport.ZoomPercent}%";
    public string CursorText => $"Cursor: {CursorWorldX}, {CursorWorldY}";
    public string ElementsText => $"Elements: {ElementCount}";

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
        Viewport.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Viewport.ZoomPercent))
                OnPropertyChanged(nameof(ZoomText));
        };

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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeChoices.Add("LabelDesigner Document", new[] { ".ldlabel" });
        picker.SuggestedFileName = "Untitled.ldlabel";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        await _persistence.SaveAsync(_scene.CurrentDocument, file.Path);
        _currentFilePath = file.Path;
    }

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

    private void NotifyElementsChanged() => OnPropertyChanged(nameof(ElementsText));

    [RelayCommand]
    private void AddBarcode()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new BarcodeElement { Bounds = new RectD(50, 50, 200, 100) }, layerId);
        NotifyElementsChanged();
    }

    [RelayCommand]
    private void AddText()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new TextElement { Bounds = new RectD(50, 50, 200, 30) }, layerId);
        NotifyElementsChanged();
    }

    [RelayCommand]
    private async Task Print()
    {
        var print = App.Services!.GetRequiredService<Core.Interfaces.IPrintService>();
        await print.PrintAsync(_scene.CurrentDocument);
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        var pdf = App.Services!.GetRequiredService<Core.Interfaces.IPdfExportService>();
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Label.ldlabel";
        savePicker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        await pdf.ExportAsync(_scene.CurrentDocument, file.Path,
            new Core.Interfaces.PdfExportOptions());
    }

    [RelayCommand]
    private async Task ExportPng()
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Label.png";
        savePicker.FileTypeChoices.Add("PNG Image", new[] { ".png" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        // Render at 200 DPI to a SoftwareBitmap, then encode as PNG
        var printService = App.Services!.GetRequiredService<Core.Interfaces.IPrintService>();
        var bitmap = ((Infrastructure.Export.PrintService)printService).RenderDocumentToBitmap(_scene.CurrentDocument, 200);

        using var fileStream = await file.OpenStreamForWriteAsync();
        var encoder = Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
            fileStream.AsRandomAccessStream()).GetAwaiter().GetResult();

        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        double pageW = _scene.CurrentDocument.Page.WidthMm * 3.78;
        double pageH = _scene.CurrentDocument.Page.HeightMm * 3.78;
        Viewport.ZoomToFit(1200, 800, pageW, pageH);
    }

    [RelayCommand]
    private void RotateElement()
    {
        if (Selected != null && !Selected.Locked)
            _scene.RotateSelected(90);
    }

    [RelayCommand]
    private void AddShape()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new ShapeElement
        {
            Bounds = new RectD(60, 60, 120, 80),
            Fill = "#E0E0E0",
            Stroke = "#000000",
            StrokeWidth = 1
        }, layerId);
        NotifyElementsChanged();
    }

    [RelayCommand]
    private void AddLine()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new LineElement
        {
            X1 = 50, Y1 = 50, X2 = 200, Y2 = 50,
            Stroke = "#000000",
            StrokeWidth = 2
        }, layerId);
        NotifyElementsChanged();
    }

    [RelayCommand]
    private void AddImage()
    {
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        _scene.AddElement(new ImageElement { Bounds = new RectD(50, 50, 120, 120) }, layerId);
        NotifyElementsChanged();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected != null)
            _scene.RemoveElement(Selected.Id);
        else
            foreach (var id in _scene.SelectedIds.ToList())
                _scene.RemoveElement(id);
        NotifyElementsChanged();
    }

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

        if (landscape) (w, h) = (h, w);
        PageBounds = new RectD(50, 50, w, h);
    }

    public void SetShiftState(bool held) => _isShiftHeld = held;

    public void PointerPressed(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);
        _startPointD = pD;
        _isDragging = true;

        if (Selected != null)
        {
            var rotRect = GetRotationHandleRect(Selected);
            if (pD.X >= rotRect.X && pD.X <= rotRect.X + rotRect.Width &&
                pD.Y >= rotRect.Y && pD.Y <= rotRect.Y + rotRect.Height)
            {
                _activeHandle = ResizeHandle.Rotate;
                _originalBounds = Selected.Bounds;
                return;
            }
        }

        var hit = _scene.HitTest(pD);

        if (_isShiftHeld && hit != null)
        {
            _scene.ToggleSelect(hit.Id);
            Selected = _scene.SelectedIds.Count == 1 ? _scene.SingleSelected : null;
            _activeHandle = ResizeHandle.Move;
            return;
        }

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

        CursorWorldX = (int)pD.X;
        CursorWorldY = (int)pD.Y;

        if (!_isDragging || Selected == null)
            return;

        var dx = pD.X - _startPointD.X;
        var dy = pD.Y - _startPointD.Y;

        if (_activeHandle == ResizeHandle.Rotate)
        {
            double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            _scene.RotateSelected(angle - Selected!.Rotation);
        }
        else if (_activeHandle == ResizeHandle.None || _activeHandle == ResizeHandle.Move)
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

    private RectD SnapToGrid(RectD rect)
    {
        int grid = 20;
        return new RectD(
            Math.Round(rect.X / grid) * grid,
            Math.Round(rect.Y / grid) * grid,
            rect.Width, rect.Height);
    }

    private void Resize(double dx, double dy)
    {
        var b = _originalBounds;
        double minSize = 20;

        switch (_activeHandle)
        {
            case ResizeHandle.TopLeft: b = new RectD(b.X + dx, b.Y + dy, b.Width - dx, b.Height - dy); break;
            case ResizeHandle.Top: b = new RectD(b.X, b.Y + dy, b.Width, b.Height - dy); break;
            case ResizeHandle.TopRight: b = new RectD(b.X, b.Y + dy, b.Width + dx, b.Height - dy); break;
            case ResizeHandle.Right: b = new RectD(b.X, b.Y, b.Width + dx, b.Height); break;
            case ResizeHandle.BottomRight: b = new RectD(b.X, b.Y, b.Width + dx, b.Height + dy); break;
            case ResizeHandle.Bottom: b = new RectD(b.X, b.Y, b.Width, b.Height + dy); break;
            case ResizeHandle.BottomLeft: b = new RectD(b.X + dx, b.Y, b.Width - dx, b.Height + dy); break;
            case ResizeHandle.Left: b = new RectD(b.X + dx, b.Y, b.Width - dx, b.Height); break;
        }

        if (b.Width < minSize) b.Width = minSize;
        if (b.Height < minSize) b.Height = minSize;
        Selected!.Bounds = b;
    }

    private RectD GetRotationHandleRect(DesignElement el)
    {
        var b = el.Bounds;
        float zf = Math.Max((float)Viewport.Zoom, 0.25f);
        float rotOff = 20f / zf;
        return new RectD(b.X + b.Width / 2 - 8, b.Y - rotOff - 8, 16, 16);
    }

    public ResizeHandle GetHoverHandle(PointD pD)
    {
        if (Selected == null) return ResizeHandle.None;

        var rotRect = GetRotationHandleRect(Selected);
        if (pD.X >= rotRect.X && pD.X <= rotRect.X + rotRect.Width &&
            pD.Y >= rotRect.Y && pD.Y <= rotRect.Y + rotRect.Height)
            return ResizeHandle.Rotate;

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
