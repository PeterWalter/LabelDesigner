using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.App.Services;
using Microsoft.UI;
using Windows.Foundation;

namespace LabelDesigner.App.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    private readonly ISceneGraphService _scene;
    private readonly IUndoRedoService _undoRedo;
    private readonly ILabelPersistenceService _persistence;
    private readonly IDataSourceService _dataSource;
    private readonly IDataBindingService _dataBinding;
    private readonly IElementInteractionService _interaction;
    private readonly IPrintService _printService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IDocumentRasterizer _rasterizer;
    private readonly IRenderService _renderService;
    private readonly PropertiesViewModel _properties;
    private readonly ILabelStockPresetService _labelStockPresetService;

    public PropertiesViewModel Properties => _properties;
    public LayerPanelViewModel Layers { get; }

    public CanvasViewport Viewport { get; } = new();
    public ISceneGraphService Scene => _scene;
    public IRenderService RenderService => _renderService;
    public IReadOnlyList<LabelStockPreset> LabelStockPresets { get; }

    public double Margin = 40;
    public PageSize CurrentPageSize { get; private set; } = PageSize.A4;
    public bool IsLandscape { get; private set; } = false;

    private bool _isShiftHeld;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    private int _cursorWorldX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    private int _cursorWorldY;

    public int ElementCount => _scene.CurrentDocument.AllElements.Count;
    public string ZoomText => $"Zoom: {Viewport.ZoomPercent}%";
    public string CursorText => $"Cursor: {FormatMeasurement(CursorWorldX)}, {FormatMeasurement(CursorWorldY)}";
    public string RulerUnitText => $"Unit: {GetUnitSuffix()}";
    public string ElementsText => $"Elements: {ElementCount}";
    public string SnapStateText => AppSettingsService.ShowSnapGrid ? "Snap: ON" : "Snap: OFF";

    public RectD PageBounds { get; set; } = new(50, 50, 800, 1100);
    public List<GuideLine> Guides { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarcodeSelected))]
    [NotifyPropertyChangedFor(nameof(IsTextSelected))]
    [NotifyPropertyChangedFor(nameof(IsElementSelected))]
    private DesignElement? selected;

    [ObservableProperty]
    private string? selectedLabelStockPresetId;

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
        IDataSourceService dataSource,
        IDataBindingService dataBinding,
        IElementInteractionService interaction,
        IPrintService printService,
        IPdfExportService pdfExportService,
        IDocumentRasterizer rasterizer,
        IRenderService renderService,
        ILabelStockPresetService labelStockPresetService,
        PropertiesViewModel properties)
    {
        _scene = scene;
        _undoRedo = undoRedo;
        _persistence = persistence;
        _dataSource = dataSource;
        _dataBinding = dataBinding;
        _interaction = interaction;
        _printService = printService;
        _pdfExportService = pdfExportService;
        _rasterizer = rasterizer;
        _renderService = renderService;
        _labelStockPresetService = labelStockPresetService;
        _properties = properties;
        LabelStockPresets = _labelStockPresetService.GetAll();
        _properties.RequestRedraw = () => RequestRedraw?.Invoke();
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
            {
                _properties.TrackElement(Selected);
                Layers.Refresh(Selected?.Id);
            }
        };
        AppSettingsService.SettingsChanged += OnSettingsChanged;
        Layers.RequestRedraw = () => RequestRedraw?.Invoke();

        var layer = _scene.AddLayer("Layer 1");

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

    partial void OnSelectedLabelStockPresetIdChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var preset = _labelStockPresetService.GetById(value);
        if (preset == null)
            return;

        _scene.CurrentDocument.Page.WidthMm = preset.LabelWidthMm;
        _scene.CurrentDocument.Page.HeightMm = preset.LabelHeightMm;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, preset.LabelWidthMm * 3.78, preset.LabelHeightMm * 3.78);
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private async Task NewDocument()
    {
        _scene.Clear();
        _scene.AddLayer("Layer 1");
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
        Layers.Refresh(Selected?.Id);
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
        await _printService.PrintAsync(_scene.CurrentDocument);
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Label.ldlabel";
        savePicker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        await _pdfExportService.ExportAsync(_scene.CurrentDocument, file.Path,
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

        _ = await _dataSource.LoadAsync(file.Path);

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

        var records = await _dataSource.LoadAsync(ds.Path);
        if (records.Count == 0) { await Print(); return; }

        var originalDoc = _scene.CurrentDocument;
        foreach (var record in records)
        {
            var boundDoc = _dataBinding.ApplyRecord(originalDoc, record);
            await _printService.PrintAsync(boundDoc);
        }
    }

    [RelayCommand]
    private async Task PreviewPrint()
    {
        await _printService.ShowPrintPreviewAsync(_scene.CurrentDocument);
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

        var bitmap = await _rasterizer.RenderDocumentToBitmapAsync(_scene.CurrentDocument, 200);

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
        return CloneElement(src, Guid.NewGuid()) ?? src;
    }

    private static DesignElement? CloneElement(DesignElement source, Guid? id = null)
    {
        if (source is BarcodeElement b) return new BarcodeElement { Id = id ?? Guid.NewGuid(), Value = b.Value, TextPosition = b.TextPosition, Bounds = b.Bounds };
        if (source is TextElement t) return new TextElement { Id = id ?? Guid.NewGuid(), Text = t.Text, FontSize = t.FontSize, Bounds = t.Bounds };
        if (source is ShapeElement s) return new ShapeElement { Id = id ?? Guid.NewGuid(), Type = s.Type, Fill = s.Fill, Stroke = s.Stroke, StrokeWidth = s.StrokeWidth, Bounds = s.Bounds };
        if (source is LineElement l) return new LineElement { Id = id ?? Guid.NewGuid(), X1 = l.X1, Y1 = l.Y1, X2 = l.X2, Y2 = l.Y2, Stroke = l.Stroke, StrokeWidth = l.StrokeWidth, Bounds = l.Bounds };
        if (source is ImageElement i) return new ImageElement { Id = id ?? Guid.NewGuid(), SourcePath = i.SourcePath, Stretch = i.Stretch, Bounds = i.Bounds };
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
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void SetPageLabel6x4()
    {
        _scene.CurrentDocument.Page.WidthMm = 152.4;
        _scene.CurrentDocument.Page.HeightMm = 101.6;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, 152.4 * 3.78, 101.6 * 3.78);
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void SetPageLabel8x3()
    {
        _scene.CurrentDocument.Page.WidthMm = 203.2;
        _scene.CurrentDocument.Page.HeightMm = 76.2;
        _scene.CurrentDocument.Page.Dpi = 300;
        PageBounds = new RectD(50, 50, 203.2 * 3.78, 76.2 * 3.78);
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
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
        await PickAndPlaceImage(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg" });
    }

    [RelayCommand]
    private async Task AddSvg()
    {
        await PickAndPlaceImage(new[] { ".svg" });
    }

    private async Task PickAndPlaceImage(IEnumerable<string> fileTypes)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (var fileType in fileTypes)
            picker.FileTypeFilter.Add(fileType);

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
    public void CutSelected()
    {
        CopySelected();
        DeleteSelected();
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void BringToFront()
    {
        var selectedElements = GetSelectedElements();
        if (selectedElements.Count == 0) return;
        if (_scene.CurrentDocument.AllElements.Count == 0) return;

        var maxZ = _scene.CurrentDocument.AllElements.Max(e => e.ZIndex);
        foreach (var element in selectedElements.OrderBy(e => e.ZIndex))
        {
            maxZ++;
            _scene.ReorderElement(element.Id, maxZ);
        }

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void SendToBack()
    {
        var selectedElements = GetSelectedElements();
        if (selectedElements.Count == 0) return;
        if (_scene.CurrentDocument.AllElements.Count == 0) return;

        var minZ = _scene.CurrentDocument.AllElements.Min(e => e.ZIndex);
        foreach (var element in selectedElements.OrderBy(e => e.ZIndex))
        {
            minZ--;
            _scene.ReorderElement(element.Id, minZ);
        }

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AlignLeft()
    {
        var elements = GetSelectedElements();
        if (elements.Count < 2) return;

        var targetLeft = elements.Min(e => e.Bounds.X);
        foreach (var element in elements)
            MoveElementTo(element, targetLeft, element.Bounds.Y);

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AlignCenter()
    {
        var elements = GetSelectedElements();
        if (elements.Count < 2) return;

        var targetCenter = (elements.Min(e => e.Bounds.X) + elements.Max(e => e.Bounds.X + e.Bounds.Width)) / 2.0;
        foreach (var element in elements)
            MoveElementTo(element, targetCenter - (element.Bounds.Width / 2.0), element.Bounds.Y);

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AlignRight()
    {
        var elements = GetSelectedElements();
        if (elements.Count < 2) return;

        var targetRight = elements.Max(e => e.Bounds.X + e.Bounds.Width);
        foreach (var element in elements)
            MoveElementTo(element, targetRight - element.Bounds.Width, element.Bounds.Y);

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AlignTop()
    {
        var elements = GetSelectedElements();
        if (elements.Count < 2) return;

        var targetTop = elements.Min(e => e.Bounds.Y);
        foreach (var element in elements)
            MoveElementTo(element, element.Bounds.X, targetTop);

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AlignBottom()
    {
        var elements = GetSelectedElements();
        if (elements.Count < 2) return;

        var targetBottom = elements.Max(e => e.Bounds.Y + e.Bounds.Height);
        foreach (var element in elements)
            MoveElementTo(element, element.Bounds.X, targetBottom - element.Bounds.Height);

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void DistributeHorizontal()
    {
        var elements = GetSelectedElements().OrderBy(e => e.Bounds.CenterX).ToList();
        if (elements.Count < 3) return;

        var firstCenter = elements.First().Bounds.CenterX;
        var lastCenter = elements.Last().Bounds.CenterX;
        var step = (lastCenter - firstCenter) / (elements.Count - 1);

        for (var i = 1; i < elements.Count - 1; i++)
        {
            var targetCenter = firstCenter + (step * i);
            MoveElementTo(elements[i], targetCenter - (elements[i].Bounds.Width / 2.0), elements[i].Bounds.Y);
        }

        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void DistributeVertical()
    {
        var elements = GetSelectedElements().OrderBy(e => e.Bounds.CenterY).ToList();
        if (elements.Count < 3) return;

        var firstCenter = elements.First().Bounds.CenterY;
        var lastCenter = elements.Last().Bounds.CenterY;
        var step = (lastCenter - firstCenter) / (elements.Count - 1);

        for (var i = 1; i < elements.Count - 1; i++)
        {
            var targetCenter = firstCenter + (step * i);
            MoveElementTo(elements[i], elements[i].Bounds.X, targetCenter - (elements[i].Bounds.Height / 2.0));
        }

        RequestRedraw?.Invoke();
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
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
        RequestRedraw?.Invoke();
    }

    public void SetPage(PageSize size, bool landscape)
    {
        CurrentPageSize = size;
        IsLandscape = landscape;

        (double wMm, double hMm) = size switch
        {
            PageSize.A5 => (148, 210),
            PageSize.A4 => (210, 297),
            PageSize.A3 => (297, 420),
            _ => (210, 297)
        };

        if (landscape) (wMm, hMm) = (hMm, wMm);
        _scene.CurrentDocument.Page.WidthMm = wMm;
        _scene.CurrentDocument.Page.HeightMm = hMm;
        _scene.CurrentDocument.Page.Dpi = 300;
        double wPx = wMm * 3.78;
        double hPx = hMm * 3.78;
        PageBounds = new RectD(50, 50, wPx, hPx);
        Viewport.PageOriginX = PageBounds.X;
        Viewport.PageOriginY = PageBounds.Y;
        RequestRedraw?.Invoke();
    }

    public bool IsDragging => _interaction.IsDragging;

    public void SetShiftState(bool held) => _isShiftHeld = held;

    public void PointerPressed(Windows.Foundation.Point screenPoint)
    {
        Guides.Clear();
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);

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

        if (Selected != null)
        {
            var selectedHandle = _interaction.GetHoverHandle(Selected, pD, Viewport.Zoom);
            if (selectedHandle == ResizeHandle.Rotate)
            {
                _interaction.BeginDrag(pD, Selected, selectedHandle);
                return;
            }
        }

        var hit = _scene.HitTest(pD);

        if (_isShiftHeld && hit != null)
        {
            _scene.ToggleSelect(hit.Id);
            Selected = _scene.SelectedIds.Count == 1 ? _scene.SingleSelected : null;
            if (Selected != null)
            {
                _interaction.BeginDrag(pD, Selected, ResizeHandle.Move);
            }
            return;
        }

        if (hit == null)
        {
            Selected = null;
            _scene.ClearSelection();
            _interaction.EndDrag();
            _properties.TrackElement(null);
            return;
        }

        _scene.ClearSelection();
        _scene.Select(hit.Id);
        Selected = hit;
        _properties.TrackElement(hit);
        var handle = _interaction.GetHoverHandle(hit, pD, Viewport.Zoom);
        _interaction.BeginDrag(pD, hit, handle);
    }

    public void PointerMoved(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);

        CursorWorldX = (int)pD.X;
        CursorWorldY = (int)pD.Y;

        if (!_interaction.IsDragging || Selected == null)
            return;

        var otherBounds = _scene.CurrentDocument.AllElements
            .Where(e => e.Id != Selected.Id)
            .Select(e => e.Bounds);
        var update = _interaction.UpdateDrag(pD, Selected, otherBounds, GetPageRect());

        if (update.Rotation.HasValue)
        {
            Selected.Rotation = update.Rotation.Value;
        }

        if (update.Bounds.HasValue)
        {
            Selected.Bounds = update.Bounds.Value;
        }

        Guides.Clear();
        Guides.AddRange(update.Guides);
    }

    public void PointerReleased()
    {
        _interaction.EndDrag();
        Guides.Clear();
    }

    private RectD GetPageRect()
    {
        return new RectD(0, 0,
            _scene.CurrentDocument.Page.WidthMm * 3.78,
            _scene.CurrentDocument.Page.HeightMm * 3.78);
    }

    public ResizeHandle GetHoverHandle(PointD pD)
    {
        return _interaction.GetHoverHandle(Selected, pD, Viewport.Zoom);
    }

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(CursorText));
        OnPropertyChanged(nameof(RulerUnitText));
        OnPropertyChanged(nameof(SnapStateText));
        RequestRedraw?.Invoke();
    }

    private static string FormatMeasurement(double pixels)
    {
        var mm = pixels / 3.78;
        return AppSettingsService.RulerUnit switch
        {
            MeasurementUnit.Millimeters => $"{mm:0} mm",
            MeasurementUnit.Centimeters => $"{(mm / 10.0):0.0} cm",
            MeasurementUnit.Inches => $"{(mm / 25.4):0.00} in",
            _ => $"{mm:0} mm"
        };
    }

    private static string GetUnitSuffix()
    {
        return AppSettingsService.RulerUnit switch
        {
            MeasurementUnit.Centimeters => "cm",
            MeasurementUnit.Inches => "in",
            _ => "mm"
        };
    }

    private List<DesignElement> GetSelectedElements()
    {
        var ids = _scene.SelectedIds.ToList();
        if (ids.Count == 0 && Selected != null) ids.Add(Selected.Id);

        return ids
            .Select(id => _scene.GetElement(id))
            .Where(el => el != null)
            .Cast<DesignElement>()
            .ToList();
    }

    private void MoveElementTo(DesignElement element, double x, double y)
    {
        var page = GetPageRect();
        var target = new RectD(x, y, element.Bounds.Width, element.Bounds.Height).ClampToBounds(page);
        var dx = target.X - element.Bounds.X;
        var dy = target.Y - element.Bounds.Y;

        if (element is LineElement line)
        {
            line.X1 += dx;
            line.X2 += dx;
            line.Y1 += dy;
            line.Y2 += dy;
            var minX = Math.Min(line.X1, line.X2);
            var minY = Math.Min(line.Y1, line.Y2);
            var maxX = Math.Max(line.X1, line.X2);
            var maxY = Math.Max(line.Y1, line.Y2);
            line.Bounds = new RectD(minX, minY, maxX - minX, maxY - minY);
            return;
        }

        element.Bounds = target;
    }
}
