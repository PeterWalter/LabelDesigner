using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Windows.Foundation;

namespace LabelDesigner.App.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    private readonly ISceneGraphService _scene;
    private readonly IUndoRedoService _undoRedo;
    private readonly ILabelPersistenceService _persistence;
    private readonly PropertiesViewModel _properties;

    public PropertiesViewModel Properties => _properties;

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
    private double _rotationStartAngle;

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
    private DesignElement? _clipboard;

    public Action? RequestRedraw { get; set; }

    private bool _placeNextClick;
    private DesignElement? _pendingElement;

    public DesignerViewModel(
        ISceneGraphService scene,
        IUndoRedoService undoRedo,
        ILabelPersistenceService persistence,
        PropertiesViewModel properties)
    {
        _scene = scene;
        _undoRedo = undoRedo;
        _persistence = persistence;
        _properties = properties;

        SetPage(PageSize.A4, false);
        Viewport.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Viewport.ZoomPercent))
                OnPropertyChanged(nameof(ZoomText));
        };

        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Selected))
                _properties.TrackElement(Selected);
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
    public void Undo()
    {
        _undoRedo.Undo();
        Selected = _scene.SingleSelected;
    }

    [RelayCommand]
    public void Redo()
    {
        _undoRedo.Redo();
        Selected = _scene.SingleSelected;
    }

    private void NotifyElementsChanged() => OnPropertyChanged(nameof(ElementsText));

    private void EnterPlacementMode(DesignElement prototype)
    {
        _pendingElement = prototype;
        _placeNextClick = true;
    }

    [RelayCommand]
    private void AddBarcode()
    {
        EnterPlacementMode(new BarcodeElement
        {
            Bounds = new RectD(0, 0, 200, 100),
            Value = "TYPE HERE",
            TextPosition = BarcodeTextPosition.Bottom
        });
    }

    [RelayCommand]
    private void AddText()
    {
        EnterPlacementMode(new TextElement
        {
            Bounds = new RectD(0, 0, 150, 30),
            Text = "Double-click to edit"
        });
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

        var printService = App.Services!.GetRequiredService<Core.Interfaces.IPrintService>();
        var bitmap = await ((Infrastructure.Export.PrintService)printService).RenderDocumentToBitmapAsync(_scene.CurrentDocument, 200);

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
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void CopySelected()
    {
        _clipboard = Selected;
    }

    [RelayCommand]
    public void PasteElement()
    {
        if (_clipboard == null) return;
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        var clone = CloneElement(_clipboard);
        if (clone != null)
        {
            clone.Bounds = new RectD(
                _clipboard.Bounds.X + 20,
                _clipboard.Bounds.Y + 20,
                _clipboard.Bounds.Width,
                _clipboard.Bounds.Height);
            _scene.AddElement(clone, layerId);
        }
    }

    private static DesignElement? CloneElement(DesignElement source)
    {
        if (source is BarcodeElement b) return new BarcodeElement { Value = b.Value, TextPosition = b.TextPosition, Bounds = b.Bounds };
        if (source is TextElement t) return new TextElement { Text = t.Text, FontSize = t.FontSize, Bounds = t.Bounds };
        if (source is ShapeElement s) return new ShapeElement { Type = s.Type, Fill = s.Fill, Stroke = s.Stroke, StrokeWidth = s.StrokeWidth, Bounds = s.Bounds };
        if (source is LineElement l) return new LineElement { X1 = l.X1, Y1 = l.Y1, X2 = l.X2, Y2 = l.Y2, Stroke = l.Stroke, StrokeWidth = l.StrokeWidth };
        if (source is ImageElement i) return new ImageElement { SourcePath = i.SourcePath, Stretch = i.Stretch, Bounds = i.Bounds };
        return null;
    }

    [RelayCommand]
    private void RotateElement()
    {
        if (Selected != null && !Selected.Locked)
        {
            Selected.Rotation = (Selected.Rotation + 90) % 360;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Viewport.Zoom = Math.Clamp(Viewport.Zoom + 0.1, CanvasViewport.MinZoom, CanvasViewport.MaxZoom);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Viewport.Zoom = Math.Clamp(Viewport.Zoom - 0.1, CanvasViewport.MinZoom, CanvasViewport.MaxZoom);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void SetPageA4() { SetPage(PageSize.A4, false); RequestRedraw?.Invoke(); }

    [RelayCommand]
    private void SetPageA5() { SetPage(PageSize.A5, false); RequestRedraw?.Invoke(); }

    [RelayCommand]
    private void SetPageA3() { SetPage(PageSize.A3, false); RequestRedraw?.Invoke(); }

    [RelayCommand]
    private void SetPageLabel4x5()
    {
        _scene.CurrentDocument.Page.WidthMm = 101.6;
        _scene.CurrentDocument.Page.HeightMm = 127.0;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, 101.6 * 3.78, 127.0 * 3.78);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void SetPageLabel6x4()
    {
        _scene.CurrentDocument.Page.WidthMm = 152.4;
        _scene.CurrentDocument.Page.HeightMm = 101.6;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, 152.4 * 3.78, 101.6 * 3.78);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void SetPageLabel8x3()
    {
        _scene.CurrentDocument.Page.WidthMm = 203.2;
        _scene.CurrentDocument.Page.HeightMm = 76.2;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, 203.2 * 3.78, 76.2 * 3.78);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void AddShape()
    {
        EnterPlacementMode(new ShapeElement
        {
            Bounds = new RectD(0, 0, 120, 80),
            Fill = "#E0E0E0",
            Stroke = "#000000",
            StrokeWidth = 1
        });
    }

    [RelayCommand]
    private void AddLine()
    {
        EnterPlacementMode(new LineElement
        {
            X1 = 0, Y1 = 0, X2 = 150, Y2 = 0,
            Stroke = "#000000",
            StrokeWidth = 2
        });
    }

    [RelayCommand]
    private void AddImage()
    {
        EnterPlacementMode(new ImageElement
        {
            Bounds = new RectD(0, 0, 120, 120)
        });
    }

    [RelayCommand]
    public void DeleteSelected()
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
        _scene.CurrentDocument.Page.WidthMm = w / 3.78;
        _scene.CurrentDocument.Page.HeightMm = h / 3.78;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, w, h);
        RequestRedraw?.Invoke();
    }

    public void SetShiftState(bool held) => _isShiftHeld = held;

    public void PointerPressed(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);
        _startPointD = pD;

        // Placement mode: place the pending element at cursor
        if (_placeNextClick && _pendingElement != null)
        {
            var page = _scene.CurrentDocument.Page;
            var pw = page.WidthMm * 3.78;
            var ph = page.HeightMm * 3.78;
            var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;

            // Center the element at click position
            var halfW = _pendingElement is LineElement ? 0 : (_pendingElement.Bounds.Width / 2);
            var halfH = _pendingElement is LineElement ? 0 : (_pendingElement.Bounds.Height / 2);

            // Clamp within page bounds
            var x = Math.Max(0, Math.Min(pD.X - halfW, pw - _pendingElement.Bounds.Width));
            var y = Math.Max(0, Math.Min(pD.Y - halfH, ph - _pendingElement.Bounds.Height));

            if (_pendingElement is LineElement ln)
            {
                ln.X1 = x;
                ln.Y1 = Math.Max(0, Math.Min(pD.Y - 10, ph));
                ln.X2 = Math.Min(x + 150, pw);
                ln.Y2 = ln.Y1;
                _pendingElement.Bounds = new RectD(x, ln.Y1 - 5, Math.Min(x + 150, pw) - x, 10);
            }
            else
            {
                _pendingElement.Bounds = new RectD(x, y, _pendingElement.Bounds.Width, _pendingElement.Bounds.Height);
            }

            _scene.AddElement(_pendingElement, layerId);
            _pendingElement = null;
            _placeNextClick = false;
            NotifyElementsChanged();
            RequestRedraw?.Invoke();
            return;
        }

        _isDragging = true;

        if (Selected != null)
        {
            var rotRect = GetRotationHandleRect(Selected);
            if (pD.X >= rotRect.X && pD.X <= rotRect.X + rotRect.Width &&
                pD.Y >= rotRect.Y && pD.Y <= rotRect.Y + rotRect.Height)
            {
                _activeHandle = ResizeHandle.Rotate;
                _originalBounds = Selected.Bounds;
                _rotationStartAngle = Selected.Rotation;
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
            _properties.TrackElement(null);
            return;
        }

        _scene.ClearSelection();
        _scene.Select(hit.Id);
        Selected = hit;
        _properties.TrackElement(hit);
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
            var centerX = Selected!.Bounds.X + Selected.Bounds.Width / 2;
            var centerY = Selected.Bounds.Y + Selected.Bounds.Height / 2;
            double cursorAngle = Math.Atan2(pD.Y - centerY, pD.X - centerX) * 180.0 / Math.PI;
            double newAngle = _rotationStartAngle + (cursorAngle - Math.Atan2(_startPointD.Y - centerY, _startPointD.X - centerX) * 180.0 / Math.PI);
            Selected.Rotation = newAngle % 360;
        }
        else if (_activeHandle == ResizeHandle.None || _activeHandle == ResizeHandle.Move)
        {
            var newBounds = SnapToGrid(_originalBounds.Translate(dx, dy));
            // Constrain to page bounds
            var pageW = _scene.CurrentDocument.Page.WidthMm * 3.78;
            var pageH = _scene.CurrentDocument.Page.HeightMm * 3.78;
            if (newBounds.X < 0) newBounds.X = 0;
            if (newBounds.Y < 0) newBounds.Y = 0;
            if (newBounds.X + newBounds.Width > pageW) newBounds.X = pageW - newBounds.Width;
            if (newBounds.Y + newBounds.Height > pageH) newBounds.Y = pageH - newBounds.Height;
            if (newBounds.X < 0) newBounds.X = 0;
            if (newBounds.Y < 0) newBounds.Y = 0;
            Selected.Bounds = newBounds;
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
