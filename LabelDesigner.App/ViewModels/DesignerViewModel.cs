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
    public LayerPanelViewModel Layers { get; }

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

    private PlacementMode _placementMode = PlacementMode.None;
    private DesignElement? _pendingElement;

    public bool IsInLinePlacementMode => _placementMode == PlacementMode.LineClickDrag;
    public bool IsInPlacementMode => _placementMode != PlacementMode.None;

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
        Layers = new LayerPanelViewModel(scene);

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

    public void NotifyElementsChanged()
    {
        OnPropertyChanged(nameof(ElementsText));
        Layers.Refresh();
    }

    public void CancelPlacement()
    {
        _placementMode = PlacementMode.None;
        _pendingElement = null;
    }

    [RelayCommand]
    private void GroupSelected()
    {
        var ids = _scene.SelectedIds.ToList();
        if (ids.Count < 2) return;
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        if (layerId == null) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var id in ids)
        {
            var el = _scene.GetElement(id);
            if (el == null) continue;
            if (el.Bounds.X < minX) minX = el.Bounds.X;
            if (el.Bounds.Y < minY) minY = el.Bounds.Y;
            if (el.Bounds.X + el.Bounds.Width > maxX) maxX = el.Bounds.X + el.Bounds.Width;
            if (el.Bounds.Y + el.Bounds.Height > maxY) maxY = el.Bounds.Y + el.Bounds.Height;
        }

        var container = new ContainerElement { Bounds = new RectD(minX, minY, maxX - minX, maxY - minY), Name = "Group" };
        foreach (var id in ids)
        {
            var el = _scene.GetElement(id);
            if (el == null) continue;
            el.Bounds = new RectD(el.Bounds.X - minX, el.Bounds.Y - minY, el.Bounds.Width, el.Bounds.Height);
            container.ChildIds.Add(id);
            // Remove from layer's ElementIds but keep in AllElements
            if (layerId != null && _scene.CurrentDocument.Layers.FirstOrDefault(l => l.Id == layerId.Value) != null)
            {
                var layer = _scene.CurrentDocument.Layers.First(l => l.Id == layerId.Value);
                layer.ElementIds.Remove(id);
            }
        }
        _scene.AddElement(container, layerId);
        _scene.ClearSelection();
        _scene.Select(container.Id);
        Selected = container;
        NotifyElementsChanged(); RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void UngroupSelected()
    {
        var container = Selected as ContainerElement;
        if (container == null) return;
        var layerId = container.ParentId ?? _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        if (layerId == null) return;

        double offsetX = container.Bounds.X;
        double offsetY = container.Bounds.Y;

        foreach (var childId in container.ChildIds.ToList())
        {
            var child = _scene.GetElement(childId);
            if (child == null) continue;
            child.Bounds = new RectD(child.Bounds.X + offsetX, child.Bounds.Y + offsetY,
                child.Bounds.Width, child.Bounds.Height);
            // Add back to layer
            if (_scene.CurrentDocument.Layers.FirstOrDefault(l => l.Id == layerId.Value) != null)
            {
                var layer = _scene.CurrentDocument.Layers.First(l => l.Id == layerId.Value);
                layer.ElementIds.Add(childId);
            }
        }

        _scene.RemoveElement(container.Id);
        _scene.ClearSelection();
        Selected = null;
        NotifyElementsChanged(); RequestRedraw?.Invoke();
    }

    private void EnterPlacementMode(DesignElement prototype)
    {
        _pendingElement = prototype;
        _placementMode = PlacementMode.PlaceOnce;
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
    private async Task LoadDataSource()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd2 = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd2);
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var dataSource = App.Services!.GetRequiredService<Core.Interfaces.IDataSourceService>();
        var data = await dataSource.LoadAsync(file.Path);

        _scene.CurrentDocument.DataSource = new DataSourceConfig
        {
            Type = Path.GetExtension(file.Path).TrimStart('.'),
            Path = file.Path
        };
    }

    [RelayCommand]
    private async Task PrintWithData()
    {
        var ds = _scene.CurrentDocument.DataSource;
        if (ds == null) { await Print(); return; }

        var dataSource = App.Services!.GetRequiredService<Core.Interfaces.IDataSourceService>();
        var records = await dataSource.LoadAsync(ds.Path);
        if (records.Count == 0) { await Print(); return; }

        var originalDoc = _scene.CurrentDocument;
        foreach (var record in records)
        {
            var boundDoc = ApplyDataBinding(originalDoc, record);
            var print = App.Services!.GetRequiredService<Core.Interfaces.IPrintService>();
            await print.PrintAsync(boundDoc);
        }
    }

    private static SceneDocument ApplyDataBinding(SceneDocument doc, IReadOnlyDictionary<string, string> record)
    {
        var clone = new SceneDocument { Version = doc.Version, Page = doc.Page, DataSource = doc.DataSource };
        foreach (var layer in doc.Layers)
            clone.Layers.Add(new LayerNode { Name = layer.Name, Visible = layer.Visible, Locked = layer.Locked });
        foreach (var el in doc.AllElements)
        {
            var boundEl = BindElement(el, record);
            clone.AllElements.Add(boundEl);
            if (boundEl.ParentId.HasValue)
            {
                var parentLayer = clone.Layers.FirstOrDefault(l => l.Id == boundEl.ParentId);
                parentLayer?.ElementIds.Add(boundEl.Id);
            }
        }
        return clone;
    }

    private static DesignElement BindElement(DesignElement el, IReadOnlyDictionary<string, string> record)
    {
        if (el is BarcodeElement bc)
        {
            var bound = new BarcodeElement { Bounds = bc.Bounds, TextPosition = bc.TextPosition };
            bound.Value = ResolveTemplate(bc.Value, record);
            return bound;
        }
        if (el is TextElement txt)
        {
            var bound = new TextElement { Bounds = txt.Bounds, FontSize = txt.FontSize };
            bound.Text = ResolveTemplate(txt.Text, record);
            return bound;
        }
        return CloneElement(el) ?? el;
    }

    private static string ResolveTemplate(string template, IReadOnlyDictionary<string, string> record)
    {
        int start;
        while ((start = template.IndexOf("{{")) >= 0)
        {
            int end = template.IndexOf("}}", start);
            if (end < 0) break;
            var field = template.Substring(start + 2, end - start - 2).Trim();
            var value = record.TryGetValue(field, out var v) ? v : "";
            template = template.Substring(0, start) + value + template.Substring(end + 2);
        }
        return template;
    }

    [RelayCommand]
    private async Task PreviewPrint()
    {
        var print = App.Services!.GetRequiredService<Core.Interfaces.IPrintService>();
        await print.ShowPrintPreviewAsync(_scene.CurrentDocument);
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
        if (Selected == null && _scene.SelectedIds.Count == 0) return;
        var src = Selected ?? _scene.GetElement(_scene.SelectedIds.First());
        if (src == null) return;
        _clipboard = CloneElement(src);
    }

    [RelayCommand]
    public void PasteElement()
    {
        if (_clipboard == null) return;
        var layerId = _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
        var clone = CloneElement(_clipboard);
        if (clone != null)
        {
            clone = DeepCloneWithNewId(clone);
            clone.Bounds = new RectD(
                clone.Bounds.X + 20,
                clone.Bounds.Y + 20,
                clone.Bounds.Width,
                clone.Bounds.Height);
            _scene.AddElement(clone, layerId);
            _scene.ClearSelection();
            _scene.Select(clone.Id);
            Selected = clone;
            NotifyElementsChanged();
            RequestRedraw?.Invoke();
        }
    }

    private static DesignElement DeepCloneWithNewId(DesignElement src)
    {
        var clone = CloneElement(src);
        if (clone != null) clone.Id = Guid.NewGuid();
        return clone ?? src;
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
        // Line uses click-drag mode: first click sets start, release sets end
        _placementMode = PlacementMode.LineClickDrag;
    }

    [RelayCommand]
    private async Task AddImage()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".svg");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var element = new ImageElement
        {
            Bounds = new RectD(0, 0, 150, 150),
            SourcePath = file.Path
        };

        EnterPlacementMode(element);
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

    [RelayCommand]
    public void ToggleOrientation()
    {
        IsLandscape = !IsLandscape;
        var w = _scene.CurrentDocument.Page.WidthMm;
        var h = _scene.CurrentDocument.Page.HeightMm;
        _scene.CurrentDocument.Page.WidthMm = h;
        _scene.CurrentDocument.Page.HeightMm = w;
        PageBounds = new RectD(PageBounds.X, PageBounds.Y, PageBounds.Height, PageBounds.Width);
        RequestRedraw?.Invoke();
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
        if (_placementMode == PlacementMode.PlaceOnce && _pendingElement != null)
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
            _placementMode = PlacementMode.None;
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

        // Clamp to page bounds
        var pageW2 = _scene.CurrentDocument.Page.WidthMm * 3.78;
        var pageH2 = _scene.CurrentDocument.Page.HeightMm * 3.78;
        if (b.X < 0) b.X = 0;
        if (b.Y < 0) b.Y = 0;
        if (b.X + b.Width > pageW2) { if (b.Width >= pageW2) { b.X = 0; b.Width = pageW2; } else b.X = pageW2 - b.Width; }
        if (b.Y + b.Height > pageH2) { if (b.Height >= pageH2) { b.Y = 0; b.Height = pageH2; } else b.Y = pageH2 - b.Height; }
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
