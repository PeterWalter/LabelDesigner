using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.App.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Dispatching;
using Windows.Foundation;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LabelDesigner.App.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    private double? _cachedPixelsPerMm;
    private readonly object _pixelsPerMmLock = new();

    private double GetPixelsPerMm()
    {
        // Check cache first (fast path)
        if (_cachedPixelsPerMm.HasValue)
            return _cachedPixelsPerMm.Value;

        lock (_pixelsPerMmLock)
        {
            // Double-check after acquiring lock
            if (_cachedPixelsPerMm.HasValue)
                return _cachedPixelsPerMm.Value;

            const double basePpMm = 96.0 / 25.4; // 3.78 at 100% DPI
            double dpiScale = 1.0;

            try
            {
                // Only attempt DPI detection if we're on the UI thread with a DispatcherQueue
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                if (dispatcher != null)
                {
                    var displayInfo = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();
                    dpiScale = displayInfo.RawPixelsPerViewPixel;
                }
            }
            catch (COMException)
            {
                // GetForCurrentView() failed—either not on UI thread or CoreWindow not ready
                // Fall through and use dpiScale = 1.0 (base 96 DPI)
            }
            catch (Exception)
            {
                // Any other exception—also use default
            }

            _cachedPixelsPerMm = basePpMm * dpiScale;
            return _cachedPixelsPerMm.Value;
        }
    }

    public double PixelsPerMm => GetPixelsPerMm();

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

    // Event handlers stored for cleanup
    private PropertyChangedEventHandler? _viewportPropertyChanged;
    private PropertyChangedEventHandler? _selectedPropertyChanged;
    private Action? _settingsChanged;
    private Action? _documentReset;

    public PropertiesViewModel Properties => _properties;
    public LayerPanelViewModel Layers { get; }

    public CanvasViewport Viewport { get; } = new();
    public ISceneGraphService Scene => _scene;
    public IRenderService RenderService => _renderService;
    public IReadOnlyList<LabelStockPreset> LabelStockPresets { get; }

    public double Margin = 40;
    public PageSize CurrentPageSize { get; private set; } = PageSize.A4;

    private bool _isShiftHeld;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    public partial int CursorWorldX { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    public partial int CursorWorldY { get; set; }

    public int ElementCount => _scene.CurrentDocument.AllElements.Count;
    public string ZoomText => $"Zoom: {Viewport.ZoomPercent}%";
    public string CursorText => $"Cursor: {FormatMeasurement(CursorWorldX)}, {FormatMeasurement(CursorWorldY)}";
    public string RulerUnitText => $"Unit: {GetUnitSuffix()}";
    public string ElementsText => $"Elements: {ElementCount}";
    public string SnapStateText => AppSettingsService.ShowSnapGrid ? "Snap: ON" : "Snap: OFF";
    public IReadOnlyList<string> RecentFiles => AppSettingsService.RecentFiles;

    public RectD PageBounds { get; set; } = new(0, 0, 800, 1100);
    public List<GuideLine> Guides { get; } = new();
    public InteractionState InteractionState { get; private set; } = InteractionState.Idle;
    public RectD? MarqueeSelectionRect { get; private set; }

    [ObservableProperty]
    public partial ToolMode ActiveTool { get; set; } = ToolMode.Select;

    private DocumentDefaults Defaults => _scene.CurrentDocument.Defaults;
    private PointD? _marqueeStartPoint;
    private ResizeHandle _activeHandle = ResizeHandle.None;

    public double PageWidthMm
    {
        get => _scene.CurrentDocument.Page.WidthMm;
        set
        {
            var normalized = Math.Max(1, value);
            if (Math.Abs(_scene.CurrentDocument.Page.WidthMm - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.WidthMm = normalized;
            UpdatePageBounds();
        }
    }

    public double PageHeightMm
    {
        get => _scene.CurrentDocument.Page.HeightMm;
        set
        {
            var normalized = Math.Max(1, value);
            if (Math.Abs(_scene.CurrentDocument.Page.HeightMm - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.HeightMm = normalized;
            UpdatePageBounds();
        }
    }

    public bool IsLandscape
    {
        get => _scene.CurrentDocument.Page.WidthMm >= _scene.CurrentDocument.Page.HeightMm;
        set
        {
            if (IsLandscape == value)
                return;

            (_scene.CurrentDocument.Page.WidthMm, _scene.CurrentDocument.Page.HeightMm) =
                (_scene.CurrentDocument.Page.HeightMm, _scene.CurrentDocument.Page.WidthMm);
            UpdatePageBounds();
        }
    }

    public int DefaultBarcodeTextPositionIndex
    {
        get => (int)Defaults.BarcodeTextPosition;
        set
        {
            var position = (BarcodeTextPosition)Math.Clamp(value, 0, Enum.GetValues<BarcodeTextPosition>().Length - 1);
            if (Defaults.BarcodeTextPosition == position)
                return;

            Defaults.BarcodeTextPosition = position;
            OnPropertyChanged(nameof(DefaultBarcodeTextPositionIndex));
        }
    }

    public string DefaultBarcodeTextFontFamily
    {
        get => Defaults.BarcodeTextFontFamily;
        set
        {
            if (Defaults.BarcodeTextFontFamily == value)
                return;

            Defaults.BarcodeTextFontFamily = value;
            OnPropertyChanged(nameof(DefaultBarcodeTextFontFamily));
        }
    }

    public double DefaultBarcodeTextFontSize
    {
        get => Defaults.BarcodeTextFontSize;
        set
        {
            if (Math.Abs(Defaults.BarcodeTextFontSize - value) < 0.001)
                return;

            Defaults.BarcodeTextFontSize = value;
            OnPropertyChanged(nameof(DefaultBarcodeTextFontSize));
        }
    }

    public string DefaultBarcodeTextColor
    {
        get => Defaults.BarcodeTextColor;
        set
        {
            if (Defaults.BarcodeTextColor == value)
                return;

            Defaults.BarcodeTextColor = value;
            OnPropertyChanged(nameof(DefaultBarcodeTextColor));
        }
    }

    public string DefaultTextFontFamily
    {
        get => Defaults.TextFontFamily;
        set
        {
            if (Defaults.TextFontFamily == value)
                return;

            Defaults.TextFontFamily = value;
            OnPropertyChanged(nameof(DefaultTextFontFamily));
        }
    }

    public double DefaultTextFontSize
    {
        get => Defaults.TextFontSize;
        set
        {
            if (Math.Abs(Defaults.TextFontSize - value) < 0.001)
                return;

            Defaults.TextFontSize = value;
            OnPropertyChanged(nameof(DefaultTextFontSize));
        }
    }

    public string DefaultTextColor
    {
        get => Defaults.TextColor;
        set
        {
            if (Defaults.TextColor == value)
                return;

            Defaults.TextColor = value;
            OnPropertyChanged(nameof(DefaultTextColor));
        }
    }

    public bool DefaultTextBold
    {
        get => Defaults.TextBold;
        set
        {
            if (Defaults.TextBold == value)
                return;

            Defaults.TextBold = value;
            OnPropertyChanged(nameof(DefaultTextBold));
        }
    }

    public bool DefaultTextItalic
    {
        get => Defaults.TextItalic;
        set
        {
            if (Defaults.TextItalic == value)
                return;

            Defaults.TextItalic = value;
            OnPropertyChanged(nameof(DefaultTextItalic));
        }
    }

    public bool DefaultTextUnderline
    {
        get => Defaults.TextUnderline;
        set
        {
            if (Defaults.TextUnderline == value)
                return;

            Defaults.TextUnderline = value;
            OnPropertyChanged(nameof(DefaultTextUnderline));
        }
    }

    public int DefaultTextAlignmentIndex
    {
        get => (int)Defaults.TextAlignment;
        set
        {
            var alignment = (TextAlignmentType)Math.Clamp(value, 0, Enum.GetValues<TextAlignmentType>().Length - 1);
            if (Defaults.TextAlignment == alignment)
                return;

            Defaults.TextAlignment = alignment;
            OnPropertyChanged(nameof(DefaultTextAlignmentIndex));
        }
    }

    public bool DefaultTextMultiline
    {
        get => Defaults.TextMultiline;
        set
        {
            if (Defaults.TextMultiline == value)
                return;

            Defaults.TextMultiline = value;
            OnPropertyChanged(nameof(DefaultTextMultiline));
        }
    }

    public double DefaultTextLineSpacing
    {
        get => Defaults.TextLineSpacing;
        set
        {
            if (Math.Abs(Defaults.TextLineSpacing - value) < 0.001)
                return;

            Defaults.TextLineSpacing = value;
            OnPropertyChanged(nameof(DefaultTextLineSpacing));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBarcodeSelected))]
    [NotifyPropertyChangedFor(nameof(IsTextSelected))]
    [NotifyPropertyChangedFor(nameof(IsElementSelected))]
    public partial DesignElement? Selected { get; set; }

    [ObservableProperty]
    public partial string? SelectedLabelStockPresetId { get; set; }

    [ObservableProperty]
    public partial string? SelectedRecentFile { get; set; }

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

        // Create handler delegates that can be unsubscribed later
        _viewportPropertyChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(Viewport.ZoomPercent))
                OnPropertyChanged(nameof(ZoomText));
        };
        Viewport.PropertyChanged += _viewportPropertyChanged;

        _selectedPropertyChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(Selected))
            {
                _properties.TrackElement(Selected);
                Layers.Refresh(Selected?.Id);
                RequestRedraw?.Invoke();
            }
        };
        this.PropertyChanged += _selectedPropertyChanged;

        _settingsChanged = () => OnSettingsChanged();
        AppSettingsService.SettingsChanged += _settingsChanged;

        Layers.RequestRedraw = () => RequestRedraw?.Invoke();

        _documentReset = () => OnDocumentReset();
        _scene.DocumentReset += _documentReset;

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
        UpdatePageBounds();
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
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".ldlabel");
        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".ldtemplate");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        await OpenDocumentFromPath(file.Path);
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
        AppSettingsService.AddRecentFile(_currentFilePath);
    }

    [RelayCommand]
    private async Task SaveAsDocument()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeChoices.Add("LabelDesigner Document", new[] { ".ldlabel" });
        picker.SuggestedFileName = "Untitled.ldlabel";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            await _persistence.SaveAsync(_scene.CurrentDocument, file.Path);
            _currentFilePath = file.Path;
            AppSettingsService.AddRecentFile(file.Path);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Error", $"Could not save document: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenSelectedRecent()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentFile))
            return;

        if (!File.Exists(SelectedRecentFile))
        {
            AppSettingsService.RemoveRecentFile(SelectedRecentFile);
            SelectedRecentFile = null;
            return;
        }

        await OpenDocumentFromPath(SelectedRecentFile);
    }

    [RelayCommand]
    private void SelectTool()
    {
        ActiveTool = ToolMode.Select;
        _placementMode = PlacementMode.None;
        RequestRedraw?.Invoke();
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

    private void UpdatePageBounds()
    {
        PageBounds = new RectD(0, 0, _scene.CurrentDocument.Page.WidthMm * PixelsPerMm, _scene.CurrentDocument.Page.HeightMm * PixelsPerMm);
        Viewport.PageOriginX = 0;
        Viewport.PageOriginY = 0;
        OnPropertyChanged(nameof(PageBounds));
        OnPropertyChanged(nameof(PageWidthMm));
        OnPropertyChanged(nameof(PageHeightMm));
        OnPropertyChanged(nameof(IsLandscape));
        RequestRedraw?.Invoke();
    }

    private void SetInteractionState(InteractionState state)
    {
        if (InteractionState == state)
            return;

        InteractionState = state;
        OnPropertyChanged(nameof(InteractionState));
        RequestRedraw?.Invoke();
    }

    private static RectD NormalizeRect(PointD start, PointD current)
    {
        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);
        return new RectD(x, y, width, height);
    }

    private void BeginMarqueeSelection(PointD start)
    {
        _marqueeStartPoint = start;
        MarqueeSelectionRect = new RectD(start.X, start.Y, 0, 0);
        OnPropertyChanged(nameof(MarqueeSelectionRect));
        SetInteractionState(InteractionState.MarqueeSelection);
    }

    private void UpdateMarqueeSelection(PointD current)
    {
        if (_marqueeStartPoint == null)
            return;

        MarqueeSelectionRect = NormalizeRect(_marqueeStartPoint.Value, current);
        OnPropertyChanged(nameof(MarqueeSelectionRect));
    }

    private void ClearMarqueeSelection()
    {
        _marqueeStartPoint = null;
        MarqueeSelectionRect = null;
        OnPropertyChanged(nameof(MarqueeSelectionRect));
    }

    private void FinalizeMarqueeSelection()
    {
        if (MarqueeSelectionRect is not { } rect || rect.Width <= 0 || rect.Height <= 0)
            return;

        var hits = _scene.GetElementsInRect(rect).ToList();
        _scene.ClearSelection();
        foreach (var hit in hits)
            _scene.Select(hit.Id);

        Selected = hits.Count == 1 ? hits[0] : null;
        if (hits.Count == 0)
            _properties.TrackElement(null);

        NotifyElementsChanged();
        RequestRedraw?.Invoke();
    }

    private void OnDocumentReset()
    {
        Selected = null;
        _properties.TrackElement(null);
        Layers.Refresh(null);
        OnPropertyChanged(nameof(ElementCount));
        OnPropertyChanged(nameof(ElementsText));
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(CursorText));
        OnPropertyChanged(nameof(RulerUnitText));
        OnPropertyChanged(nameof(SnapStateText));
        OnPropertyChanged(nameof(RecentFiles));
        OnPropertyChanged(nameof(PageWidthMm));
        OnPropertyChanged(nameof(PageHeightMm));
        OnPropertyChanged(nameof(IsLandscape));
        OnPropertyChanged(nameof(DefaultBarcodeTextPositionIndex));
        OnPropertyChanged(nameof(DefaultBarcodeTextFontFamily));
        OnPropertyChanged(nameof(DefaultBarcodeTextFontSize));
        OnPropertyChanged(nameof(DefaultBarcodeTextColor));
        OnPropertyChanged(nameof(DefaultTextFontFamily));
        OnPropertyChanged(nameof(DefaultTextFontSize));
        OnPropertyChanged(nameof(DefaultTextColor));
        OnPropertyChanged(nameof(DefaultTextBold));
        OnPropertyChanged(nameof(DefaultTextItalic));
        OnPropertyChanged(nameof(DefaultTextUnderline));
        OnPropertyChanged(nameof(DefaultTextAlignmentIndex));
        OnPropertyChanged(nameof(DefaultTextMultiline));
        OnPropertyChanged(nameof(DefaultTextLineSpacing));
        UpdatePageBounds();
        ClearMarqueeSelection();
        SetInteractionState(InteractionState.Idle);
    }

    public void SelectElementAt(PointD worldPoint)
    {
        var hit = _scene.HitTest(worldPoint);
        if (hit == null)
        {
            Selected = null;
            _scene.ClearSelection();
            return;
        }

        _scene.ClearSelection();
        _scene.Select(hit.Id);
        Selected = hit;
    }

    public void CancelPlacement()
    {
        _placementMode = PlacementMode.None;
        _pendingElement = null;
        _activeHandle = ResizeHandle.None;
        ClearMarqueeSelection();
        SetInteractionState(InteractionState.Idle);
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
        ActiveTool = ToolMode.PlaceBarcode;
        EnterPlacementMode(new BarcodeElement
        {
            Bounds = new RectD(0, 0, 200, 100),
            Value = "TYPE HERE",
            TextPosition = Defaults.BarcodeTextPosition,
            TextFontFamily = Defaults.BarcodeTextFontFamily,
            TextFontSize = Defaults.BarcodeTextFontSize,
            TextColor = Defaults.BarcodeTextColor
        });
    }

    [RelayCommand]
    private void AddText()
    {
        ActiveTool = ToolMode.PlaceText;
        EnterPlacementMode(new TextElement
        {
            Bounds = new RectD(0, 0, 150, 30),
            Text = "Double-click to edit",
            FontFamily = Defaults.TextFontFamily,
            FontSize = Defaults.TextFontSize,
            ForeColor = Defaults.TextColor,
            Bold = Defaults.TextBold,
            Italic = Defaults.TextItalic,
            Underline = Defaults.TextUnderline,
            TextAlignment = Defaults.TextAlignment,
            IsMultiline = Defaults.TextMultiline,
            LineSpacing = Defaults.TextLineSpacing
        });
    }

    [RelayCommand]
    private async Task SaveTemplate()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Template.ldtemplate";
        savePicker.FileTypeChoices.Add("Label Template", new[] { ".ldtemplate", ".ldlabel", ".json" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            await _persistence.SaveAsync(_scene.CurrentDocument, file.Path);
            _currentFilePath = file.Path;
            AppSettingsService.AddRecentFile(file.Path);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Error", $"Could not save template: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task Print()
    {
        await _printService.PrintAsync(_scene.CurrentDocument);
    }

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Label.pdf";
        savePicker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            await _pdfExportService.ExportAsync(_scene.CurrentDocument, file.Path,
                new Core.Interfaces.PdfExportOptions());
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Export Error", $"Could not export PDF: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadDataSource()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            _ = await _dataSource.LoadAsync(file.Path);

            _scene.CurrentDocument.DataSource = new DataSourceConfig
            {
                Type = Path.GetExtension(file.Path).TrimStart('.'),
                Path = file.Path
            };
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Data Source Error", $"Could not load data source: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PrintWithData()
    {
        var ds = _scene.CurrentDocument.DataSource;
        if (ds == null) { await Print(); return; }

        try
        {
            var records = await _dataSource.LoadAsync(ds.Path);
            if (records.Count == 0) { await Print(); return; }

            var originalDoc = _scene.CurrentDocument;
            foreach (var record in records)
            {
                var boundDoc = _dataBinding.ApplyRecord(originalDoc, record);
                await _printService.PrintAsync(boundDoc);
            }
        }
        catch (FileNotFoundException ex)
        {
            ShowErrorDialog("Data Source Not Found", $"Could not find data source file: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            ShowErrorDialog("Invalid Data Format", $"The data source file is invalid: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Loading Data", $"An error occurred while loading the data source: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PreviewPrint()
    {
        var bitmap = await _rasterizer.RenderDocumentToBitmapAsync(_scene.CurrentDocument, 150);
        var source = new SoftwareBitmapSource();
        await source.SetBitmapAsync(bitmap);

        var image = new Image
        {
            Source = source,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            MaxWidth = 900,
            MaxHeight = 700
        };

        var xamlRoot = App.MainWindow?.Content.XamlRoot;
        if (xamlRoot == null)
            return;

        var dialog = new ContentDialog
        {
            Title = "Print Preview",
            Content = new ScrollViewer { Content = image },
            CloseButtonText = "Close",
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync();
    }

    [RelayCommand]
    private async Task ExportPng()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Label.png";
        savePicker.FileTypeChoices.Add("PNG Image", new[] { ".png" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var bitmap = await _rasterizer.RenderDocumentToBitmapAsync(_scene.CurrentDocument, 200);

            using var fileStream = await file.OpenStreamForWriteAsync();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                fileStream.AsRandomAccessStream());

            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Export Error", $"Could not export PNG: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ZoomToFit()
    {
        double pageW = _scene.CurrentDocument.Page.WidthMm * PixelsPerMm;
        double pageH = _scene.CurrentDocument.Page.HeightMm * PixelsPerMm;
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
        var newId = id ?? Guid.NewGuid();
        
        if (source is BarcodeElement b)
            return new BarcodeElement 
            { 
                Id = newId, 
                Value = b.Value, 
                TextPosition = b.TextPosition, 
                TextFontFamily = b.TextFontFamily,
                TextFontSize = b.TextFontSize,
                TextColor = b.TextColor,
                Bounds = b.Bounds,
                Name = b.Name,
                Rotation = b.Rotation,
                Opacity = b.Opacity,
                Locked = b.Locked
            };
        
        if (source is TextElement t)
            return new TextElement 
            { 
                Id = newId, 
                Text = t.Text, 
                FontSize = t.FontSize,
                FontFamily = t.FontFamily,
                Bold = t.Bold,
                Italic = t.Italic,
                Underline = t.Underline,
                ForeColor = t.ForeColor,
                LineSpacing = t.LineSpacing,
                TextAlignment = t.TextAlignment,
                IsMultiline = t.IsMultiline,
                Bounds = t.Bounds,
                Name = t.Name,
                Rotation = t.Rotation,
                Opacity = t.Opacity,
                Locked = t.Locked
            };
        
        if (source is ShapeElement s)
            return new ShapeElement 
            { 
                Id = newId, 
                Type = s.Type, 
                Fill = s.Fill, 
                Stroke = s.Stroke, 
                StrokeWidth = s.StrokeWidth, 
                Bounds = s.Bounds,
                Name = s.Name,
                Rotation = s.Rotation,
                Opacity = s.Opacity,
                Locked = s.Locked
            };
        
        if (source is LineElement l)
            return new LineElement 
            { 
                Id = newId, 
                X1 = l.X1, 
                Y1 = l.Y1, 
                X2 = l.X2, 
                Y2 = l.Y2, 
                Stroke = l.Stroke, 
                StrokeWidth = l.StrokeWidth, 
                Bounds = l.Bounds,
                Name = l.Name,
                Rotation = l.Rotation,
                Opacity = l.Opacity,
                Locked = l.Locked
            };
        
        if (source is ImageElement i)
            return new ImageElement 
            { 
                Id = newId, 
                SourcePath = i.SourcePath, 
                Stretch = i.Stretch, 
                Bounds = i.Bounds,
                Name = i.Name,
                Rotation = i.Rotation,
                Opacity = i.Opacity,
                Locked = i.Locked
            };
        
        if (source is SvgElement sv)
            return new SvgElement 
            { 
                Id = newId, 
                SourcePath = sv.SourcePath, 
                Stretch = sv.Stretch, 
                Bounds = sv.Bounds,
                Name = sv.Name,
                Rotation = sv.Rotation,
                Opacity = sv.Opacity,
                Locked = sv.Locked,
                CachedBitmap = sv.CachedBitmap
            };
        
        if (source is ContainerElement c)
        {
            var container = new ContainerElement { Id = newId, Name = c.Name };
            // For now, don't clone children recursively - just clone the container shell
            // In a full implementation, you'd need to recursively clone all descendants
            return container;
        }
        
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
    private void SetPageA4() { SetPage(PageSize.A4, false); }

    [RelayCommand]
    private void SetPageA5() { SetPage(PageSize.A5, false); }

    [RelayCommand]
    private void SetPageA3() { SetPage(PageSize.A3, false); }

    [RelayCommand]
    private void SetPageLabel4x5()
    {
        _scene.CurrentDocument.Page.WidthMm = 101.6;
        _scene.CurrentDocument.Page.HeightMm = 127.0;
        _scene.CurrentDocument.Page.Dpi = 300;
        UpdatePageBounds();
    }

    [RelayCommand]
    private void SetPageLabel6x4()
    {
        _scene.CurrentDocument.Page.WidthMm = 152.4;
        _scene.CurrentDocument.Page.HeightMm = 101.6;
        _scene.CurrentDocument.Page.Dpi = 300;
        UpdatePageBounds();
    }

    [RelayCommand]
    private void SetPageLabel8x3()
    {
        _scene.CurrentDocument.Page.WidthMm = 203.2;
        _scene.CurrentDocument.Page.HeightMm = 76.2;
        _scene.CurrentDocument.Page.Dpi = 300;
        UpdatePageBounds();
    }

    [RelayCommand]
    private void AddShape()
    {
        ActiveTool = ToolMode.PlaceShape;
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
        ActiveTool = ToolMode.PlaceLine;
        _placementMode = PlacementMode.LineClickDrag;
    }

    [RelayCommand]
    private async Task AddImage()
    {
        ActiveTool = ToolMode.PlaceImage;
        await PickAndPlaceImage(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg" });
    }

    [RelayCommand]
    private async Task AddSvg()
    {
        ActiveTool = ToolMode.PlaceSvg;
        await PickAndPlaceSvg(new[] { ".svg" });
    }

    private async Task PickAndPlaceSvg(IEnumerable<string> fileTypes)
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        foreach (var fileType in fileTypes)
            picker.FileTypeFilter.Add(fileType);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var element = new SvgElement
        {
            Bounds = new RectD(0, 0, 150, 150),
            SourcePath = file.Path
        };

        EnterPlacementMode(element);
    }

    private async Task PickAndPlaceImage(IEnumerable<string> fileTypes)
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
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
    private void MoveLeft()
    {
        _scene.MoveSelected(-5, 0);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void MoveRight()
    {
        _scene.MoveSelected(5, 0);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void MoveUp()
    {
        _scene.MoveSelected(0, -5);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void MoveDown()
    {
        _scene.MoveSelected(0, 5);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void ScaleUp()
    {
        ApplyScale(1.1);
    }

    [RelayCommand]
    private void ScaleDown()
    {
        ApplyScale(0.9);
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
    private void SetTextTop()
    {
        if (Selected is BarcodeElement b)
        {
            b.TextPosition = BarcodeTextPosition.Top;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    private void SetTextBottom()
    {
        if (Selected is BarcodeElement b)
        {
            b.TextPosition = BarcodeTextPosition.Bottom;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    private void SetTextLeft()
    {
        if (Selected is BarcodeElement b)
        {
            b.TextPosition = BarcodeTextPosition.Left;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    private void SetTextRight()
    {
        if (Selected is BarcodeElement b)
        {
            b.TextPosition = BarcodeTextPosition.Right;
            RequestRedraw?.Invoke();
        }
    }

    private void ApplyScale(double factor)
    {
        if (Selected == null || Selected.Locked)
            return;

        var oldScaleX = Selected.ScaleX;
        var oldScaleY = Selected.ScaleY;
        var newScaleX = Math.Clamp(oldScaleX * factor, 0.1, 10.0);
        var newScaleY = Math.Clamp(oldScaleY * factor, 0.1, 10.0);

        _undoRedo.Execute(new ScaleElementCommand(
            Selected,
            oldScaleX,
            oldScaleY,
            newScaleX,
            newScaleY,
            RequestRedraw));
    }

    private sealed class ScaleElementCommand : IUndoableCommand
    {
        private readonly DesignElement _element;
        private readonly double _oldScaleX;
        private readonly double _oldScaleY;
        private readonly double _newScaleX;
        private readonly double _newScaleY;
        private readonly Action? _onChanged;

        public ScaleElementCommand(
            DesignElement element,
            double oldScaleX,
            double oldScaleY,
            double newScaleX,
            double newScaleY,
            Action? onChanged)
        {
            _element = element;
            _oldScaleX = oldScaleX;
            _oldScaleY = oldScaleY;
            _newScaleX = newScaleX;
            _newScaleY = newScaleY;
            _onChanged = onChanged;
        }

        public string Description => "Scale element";

        public void Execute()
        {
            _element.ScaleX = _newScaleX;
            _element.ScaleY = _newScaleY;
            _onChanged?.Invoke();
        }

        public void Undo()
        {
            _element.ScaleX = _oldScaleX;
            _element.ScaleY = _oldScaleY;
            _onChanged?.Invoke();
        }
    }

    [RelayCommand]
    private void IncreaseFont()
    {
        if (Selected is TextElement t)
        {
            t.FontSize += 2;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    private void DecreaseFont()
    {
        if (Selected is TextElement t)
        {
            t.FontSize -= 2;
            RequestRedraw?.Invoke();
        }
    }

    [RelayCommand]
    public void ToggleOrientation()
    {
        IsLandscape = !IsLandscape;
    }

    public void SetPage(PageSize size, bool landscape)
    {
        CurrentPageSize = size;

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
        UpdatePageBounds();
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
            var pw = page.WidthMm * PixelsPerMm;
            var ph = page.HeightMm * PixelsPerMm;
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

        // Only allow selection and dragging when in Select tool mode
        if (ActiveTool != ToolMode.Select)
        {
            return;
        }

        if (Selected != null)
        {
            var selectedHandle = _interaction.GetHoverHandle(Selected, pD, Viewport.Zoom);
            _activeHandle = selectedHandle;

            if (selectedHandle == ResizeHandle.Rotate)
                SetInteractionState(InteractionState.Rotating);
            else if (selectedHandle == ResizeHandle.Move)
                SetInteractionState(InteractionState.Selecting);
            else if (selectedHandle != ResizeHandle.None)
                SetInteractionState(InteractionState.Resizing);
            else
                SetInteractionState(InteractionState.Selecting);

            if (selectedHandle != ResizeHandle.None)
            {
                _interaction.BeginDrag(pD, Selected, selectedHandle);
                return;
            }
        }

        var hit = _scene.HitTest(pD);

        if (_isShiftHeld && hit != null)
        {
            SetInteractionState(InteractionState.Selecting);
            _scene.ToggleSelect(hit.Id);
            Selected = _scene.SelectedIds.Count == 1 ? _scene.SingleSelected : null;
            RequestRedraw?.Invoke();
            if (Selected != null)
            {
                _activeHandle = ResizeHandle.Move;
                SetInteractionState(InteractionState.Dragging);
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
            _activeHandle = ResizeHandle.None;
            RequestRedraw?.Invoke();
            BeginMarqueeSelection(pD);
            return;
        }

        SetInteractionState(InteractionState.Selecting);
        _scene.ClearSelection();
        _scene.Select(hit.Id);
        Selected = hit;
        _properties.TrackElement(hit);
        RequestRedraw?.Invoke();
        var handle = _interaction.GetHoverHandle(hit, pD, Viewport.Zoom);
        _activeHandle = handle;
        if (handle == ResizeHandle.Rotate)
            SetInteractionState(InteractionState.Rotating);
        else if (handle == ResizeHandle.Move)
            SetInteractionState(InteractionState.Selecting);
        else if (handle != ResizeHandle.None)
            SetInteractionState(InteractionState.Resizing);
        _interaction.BeginDrag(pD, hit, handle);
    }

    public void PointerMoved(Windows.Foundation.Point screenPoint)
    {
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);

        CursorWorldX = (int)pD.X;
        CursorWorldY = (int)pD.Y;

        if (InteractionState == InteractionState.MarqueeSelection)
        {
            UpdateMarqueeSelection(pD);
            RequestRedraw?.Invoke();
            return;
        }

        if (_interaction.IsDragging && Selected != null)
        {
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

            if (_activeHandle == ResizeHandle.Rotate)
                SetInteractionState(InteractionState.Rotating);
            else if (_activeHandle == ResizeHandle.Move || _activeHandle == ResizeHandle.None)
                SetInteractionState(InteractionState.Dragging);
            else
                SetInteractionState(InteractionState.Resizing);

            Guides.Clear();
            Guides.AddRange(update.Guides);
            RequestRedraw?.Invoke();
            return;
        }

        var hit = _scene.HitTest(pD);
        SetInteractionState(hit != null ? InteractionState.Hover : InteractionState.Idle);
        RequestRedraw?.Invoke();
    }

    public void PointerReleased()
    {
        var wasMarqueeSelection = InteractionState == InteractionState.MarqueeSelection;
        _interaction.EndDrag();
        Guides.Clear();
        if (wasMarqueeSelection)
            FinalizeMarqueeSelection();

        ClearMarqueeSelection();
        _activeHandle = ResizeHandle.None;
        if (InteractionState is InteractionState.Dragging or InteractionState.Resizing or InteractionState.Rotating or InteractionState.Selecting or InteractionState.MarqueeSelection)
            SetInteractionState(InteractionState.Idle);
        RequestRedraw?.Invoke();
    }

    private RectD GetPageRect()
    {
        return new RectD(0, 0,
            _scene.CurrentDocument.Page.WidthMm * PixelsPerMm,
            _scene.CurrentDocument.Page.HeightMm * PixelsPerMm);
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
        OnPropertyChanged(nameof(RecentFiles));
        RequestRedraw?.Invoke();
    }

    private async Task OpenDocumentFromPath(string filePath)
    {
        try
        {
            var doc = await _persistence.LoadAsync(filePath);
            _scene.Load(doc);
            _currentFilePath = filePath;
            AppSettingsService.AddRecentFile(filePath);
            SelectedRecentFile = filePath;
        }
        catch (FileNotFoundException ex)
        {
            ShowErrorDialog("File Not Found", $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowErrorDialog("Permission Denied", $"Access denied to '{Path.GetFileName(filePath)}': {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            ShowErrorDialog("Invalid File Format", $"The file is corrupted or invalid: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Opening File", $"An error occurred while opening the file: {ex.Message}");
        }
    }

    private string FormatMeasurement(double pixels)
    {
        var mm = pixels / PixelsPerMm;
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

    private void ShowErrorDialog(string title, string message)
    {
        var mainWindow = App.MainWindow;
        if (mainWindow == null) return;

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = mainWindow.Content.XamlRoot
        };
        _ = dialog.ShowAsync();
    }

    private bool TryGetWindowHandle(out IntPtr hwnd)
    {
        try
        {
            var mainWindow = App.MainWindow;
            if (mainWindow == null)
            {
                hwnd = IntPtr.Zero;
                return false;
            }
            hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
            return hwnd != IntPtr.Zero;
        }
        catch
        {
            hwnd = IntPtr.Zero;
            return false;
        }
    }

    /// <summary>Unsubscribe from all event handlers to prevent memory leaks. Call when view model is being disposed.</summary>
    public void Dispose()
    {
        if (_viewportPropertyChanged != null)
            Viewport.PropertyChanged -= _viewportPropertyChanged;

        if (_selectedPropertyChanged != null)
            this.PropertyChanged -= _selectedPropertyChanged;

        if (_settingsChanged != null)
            AppSettingsService.SettingsChanged -= _settingsChanged;

        if (_documentReset != null)
            _scene.DocumentReset -= _documentReset;

        _viewportPropertyChanged = null;
        _selectedPropertyChanged = null;
        _settingsChanged = null;
        _documentReset = null;
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
