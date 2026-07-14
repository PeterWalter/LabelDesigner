using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Application.Data;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Interfaces;
using LabelDesigner.App.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;

namespace LabelDesigner.App.ViewModels;

public partial class DesignerViewModel : ObservableObject
{
    public double PixelsPerMm => DpiService.PixelsPerMm;

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

    // DPI at construction time (before window DPI is known). Used to rescale
    // default elements in RefreshDpiDependentState when window DPI is larger.
    private double _constructionPixelsPerMm;

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
    public int DataMergeModeIndex => GetDataMergeMode() == DataMergeMode.MultipleRecordsPerPage ? 1 : 0;

    public RectD PageBounds { get; set; } = new(0, 0, 800, 1100);
    public List<GuideLine> Guides { get; } = new();
    public InteractionState InteractionState { get; private set; } = InteractionState.Idle;
    public RectD? MarqueeSelectionRect { get; private set; }

    [ObservableProperty]
    public partial ToolMode ActiveTool { get; set; } = ToolMode.Select;

    private DocumentDefaults Defaults => _scene.CurrentDocument.Defaults;
    private PointD? _marqueeStartPoint;
    private ResizeHandle _activeHandle = ResizeHandle.None;
    private List<GuideLine> _savedUserGuides = new();

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

    public double MarginTop
    {
        get => _scene.CurrentDocument.Page.Margins.Top;
        set
        {
            var normalized = Math.Max(0, value);
            if (Math.Abs(_scene.CurrentDocument.Page.Margins.Top - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.Margins = _scene.CurrentDocument.Page.Margins with { Top = normalized };
            OnPropertyChanged(nameof(MarginTop));
            RequestRedraw?.Invoke();
        }
    }

    public double MarginRight
    {
        get => _scene.CurrentDocument.Page.Margins.Right;
        set
        {
            var normalized = Math.Max(0, value);
            if (Math.Abs(_scene.CurrentDocument.Page.Margins.Right - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.Margins = _scene.CurrentDocument.Page.Margins with { Right = normalized };
            OnPropertyChanged(nameof(MarginRight));
            RequestRedraw?.Invoke();
        }
    }

    public double MarginBottom
    {
        get => _scene.CurrentDocument.Page.Margins.Bottom;
        set
        {
            var normalized = Math.Max(0, value);
            if (Math.Abs(_scene.CurrentDocument.Page.Margins.Bottom - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.Margins = _scene.CurrentDocument.Page.Margins with { Bottom = normalized };
            OnPropertyChanged(nameof(MarginBottom));
            RequestRedraw?.Invoke();
        }
    }

    public double MarginLeft
    {
        get => _scene.CurrentDocument.Page.Margins.Left;
        set
        {
            var normalized = Math.Max(0, value);
            if (Math.Abs(_scene.CurrentDocument.Page.Margins.Left - normalized) < 0.001)
                return;

            _scene.CurrentDocument.Page.Margins = _scene.CurrentDocument.Page.Margins with { Left = normalized };
            OnPropertyChanged(nameof(MarginLeft));
            RequestRedraw?.Invoke();
        }
    }

    [ObservableProperty]
    public partial ObservableCollection<string> AvailablePrinters { get; set; } = new();

    [ObservableProperty]
    public partial string? SelectedPrinter { get; set; }

    public string PrinterListSummary => AvailablePrinters.Count == 0
        ? "No installed printers detected. Connect a printer and click Refresh."
        : $"Installed printers: {AvailablePrinters.Count}. Preview or Print opens the Windows print window where you choose the target printer.";

    public ObservableCollection<string> AvailableDataFields { get; } = new();
    public ObservableCollection<MergeBindingItem> MergeBindings { get; } = new();
    public ObservableCollection<ExpandoObject> DataMergeItemsSource { get; } = new();
    public ObservableCollection<string> AvailableWorksheetNames { get; } = new();
    public IReadOnlyList<DataMergeGridColumn> DataMergeColumns => _dataMergeColumns;
    public bool HasDataFields => AvailableDataFields.Count > 0;
    public bool HasMergeBindings => MergeBindings.Count > 0;
    public bool HasNoMergeBindings => !HasMergeBindings;
    public bool HasLoadedDataSourceButNoMergeBindings => HasLoadedDataSource && !HasMergeBindings;
    public bool HasLoadedDataSource => DataMergeItemsSource.Count > 0;
    public bool HasNoLoadedDataSource => !HasLoadedDataSource;
    public bool HasMultipleWorksheets => AvailableWorksheetNames.Count > 1;
    public bool HasSelectedDataField => !string.IsNullOrWhiteSpace(SelectedDataField);
    public bool CanBindSelectedDataFieldToBarcode => HasSelectedDataField && IsBarcodeSelected;
    public bool CanBindSelectedDataFieldToText => HasSelectedDataField && IsTextSelected;
    public bool HasSelectedMergeRecords => SelectedMergeRecordCount > 0;
    public string PrintDataButtonText => HasSelectedMergeRecords
        ? $"Print Selected ({SelectedMergeRecordCount})"
        : "Print All";
    public string SelectedDataFieldToken => string.IsNullOrWhiteSpace(SelectedDataField)
        ? "{{ColumnName}}"
        : $"{{{{{SelectedDataField}}}}}";
    public string DataMergeTargetSummary => Selected switch
    {
        BarcodeElement => "Selected target: Barcode value",
        TextElement => "Selected target: Text content",
        _ => "Selected target: none. Select a Barcode or Text element on the canvas."
    };
    public string DataMergeActionSummary => !HasLoadedDataSource
        ? "1. Load a data file. 2. Select a column. 3. Select a Barcode or Text element on the canvas. 4. Click the matching Bind button. 5. Use Preview or Print Data to review the merged output."
        : string.IsNullOrWhiteSpace(SelectedDataField)
            ? "Choose a data column, then select a Barcode or Text element to insert its merge token."
            : Selected switch
            {
                BarcodeElement => $"Click Bind to Barcode to set the barcode value to {SelectedDataFieldToken}.",
                TextElement => $"Click Bind to Text to set the text content to {SelectedDataFieldToken}.",
                _ => $"Choose a Barcode or Text element to bind {SelectedDataFieldToken}."
            };
    public int SelectedMergeRecordCount => _selectedMergeRows.Count;
    public string DataSourceSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_loadedDataSourcePath))
                return "No data source loaded";

            var summary = $"{Path.GetFileName(_loadedDataSourcePath)} ({_csvRecords.Count} row{(_csvRecords.Count == 1 ? string.Empty : "s")})";
            if (!string.IsNullOrWhiteSpace(SelectedWorksheetName))
                summary += $" • Sheet: {SelectedWorksheetName}";
            if (SelectedMergeRecordCount > 0 && SelectedMergeRecordCount != _csvRecords.Count)
                summary += $" • {SelectedMergeRecordCount} selected";
            return summary;
        }
    }

    // ── Merge preview ──────────────────────────────────────────────────────
    private List<IReadOnlyDictionary<string, string>> _csvRecords = new();
    private readonly List<DataMergeGridColumn> _dataMergeColumns = new();
    private readonly WorkbookSheetDataCache _worksheetDataCache = new();
    private SceneDocument? _previewDocument;
    private string? _loadedDataSourcePath;
    private string? _loadedWorksheetName;
    private readonly HashSet<int> _selectedMergeRows = new();
    private bool _isLoadingDataSource;
    private bool _isPrintingWithData;
    private bool _isApplyingWorksheetSelection;
    private int _worksheetSwitchVersion;

    [ObservableProperty]
    public partial int WorkspaceTabIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDataField))]
    [NotifyPropertyChangedFor(nameof(CanBindSelectedDataFieldToBarcode))]
    [NotifyPropertyChangedFor(nameof(CanBindSelectedDataFieldToText))]
    [NotifyPropertyChangedFor(nameof(SelectedDataFieldToken))]
    [NotifyPropertyChangedFor(nameof(DataMergeActionSummary))]
    public partial string? SelectedDataField { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewRecordText))]
    public partial ExpandoObject? SelectedDataMergeRow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataSourceSummary))]
    public partial string? SelectedWorksheetName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewRecordText))]
    [NotifyPropertyChangedFor(nameof(IsPreviewMode))]
    public partial int PreviewRecordIndex { get; set; } = -1;

    public int PreviewRecordCount => _csvRecords.Count;
    public bool IsPreviewMode => PreviewRecordIndex >= 0 && _csvRecords.Count > 0;
    public string PreviewRecordText => IsPreviewMode
        ? $"Merge Preview: Record {PreviewRecordIndex + 1} of {_csvRecords.Count}"
        : "";
    public SceneDocument MergePreviewDocument => _previewDocument ?? _scene.CurrentDocument;

    partial void OnPreviewRecordIndexChanged(int value)
    {
        RefreshPreviewDocument();
    }

    partial void OnWorkspaceTabIndexChanged(int value)
    {
        // Refresh merge bindings whenever the Data Merge tab becomes active
        if (value == 1)
            RefreshMergeBindings();
    }

    partial void OnSelectedDataMergeRowChanged(ExpandoObject? value)
    {
        PreviewRecordIndex = DataMergeGridModelBuilder.TryGetRecordIndex(value, out var index)
            && index >= 0
            && index < _csvRecords.Count
                ? index
                : -1;
    }

    partial void OnSelectedWorksheetNameChanged(string? value)
    {
        OnPropertyChanged(nameof(DataSourceSummary));

        if (_isApplyingWorksheetSelection
            || _isLoadingDataSource
            || string.IsNullOrWhiteSpace(value)
            || string.Equals(value, _loadedWorksheetName, StringComparison.OrdinalIgnoreCase))
            return;

        var dataSource = _scene.CurrentDocument.DataSource;
        if (dataSource == null || !IsExcelFilePath(dataSource.Path))
            return;

        _ = SwitchWorksheetAsync(dataSource.Path, value);
    }

    public void SetSelectedMergeRows(IReadOnlyList<ExpandoObject> selectedRows)
    {
        var previousCount = _selectedMergeRows.Count;
        _selectedMergeRows.Clear();

        foreach (var row in selectedRows)
        {
            if (DataMergeGridModelBuilder.TryGetRecordIndex(row, out var index)
                && index >= 0
                && index < _csvRecords.Count)
            {
                _selectedMergeRows.Add(index);
            }
        }

        if (previousCount != _selectedMergeRows.Count)
        {
            OnPropertyChanged(nameof(SelectedMergeRecordCount));
            OnPropertyChanged(nameof(HasSelectedMergeRecords));
            OnPropertyChanged(nameof(PrintDataButtonText));
            OnPropertyChanged(nameof(DataSourceSummary));
        }
    }

    [RelayCommand]
    private void NextPreviewRecord()
    {
        if (_csvRecords.Count == 0) return;
        PreviewRecordIndex = (PreviewRecordIndex + 1) % _csvRecords.Count;
    }

    [RelayCommand]
    private void PreviousPreviewRecord()
    {
        if (_csvRecords.Count == 0) return;
        PreviewRecordIndex = PreviewRecordIndex <= 0 ? _csvRecords.Count - 1 : PreviewRecordIndex - 1;
    }

    [RelayCommand]
    private void ExitPreviewMode()
    {
        PreviewRecordIndex = -1;
    }
    // ───────────────────────────────────────────────────────────────────────

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
    [NotifyPropertyChangedFor(nameof(CanBindSelectedDataFieldToBarcode))]
    [NotifyPropertyChangedFor(nameof(CanBindSelectedDataFieldToText))]
    [NotifyPropertyChangedFor(nameof(DataMergeTargetSummary))]
    [NotifyPropertyChangedFor(nameof(DataMergeActionSummary))]
    public partial DesignElement? Selected { get; set; }

    [ObservableProperty]
    public partial string? SelectedLabelStockPresetId { get; set; }

    [ObservableProperty]
    public partial string? SelectedRecentFile { get; set; }

    public bool IsElementSelected => Selected != null;
    public bool IsBarcodeSelected => Selected is BarcodeElement;
    public bool IsTextSelected => Selected is TextElement;

    [RelayCommand]
    public void AddHorizontalGuide(double positionPx)
    {
        double positionMm = positionPx / PixelsPerMm;
        Guides.Add(new GuideLine { IsHorizontal = true, PositionMm = positionMm });
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void AddVerticalGuide(double positionPx)
    {
        double positionMm = positionPx / PixelsPerMm;
        Guides.Add(new GuideLine { IsHorizontal = false, PositionMm = positionMm });
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void ClearGuides()
    {
        Guides.Clear();
        RequestRedraw?.Invoke();
    }

    private string? _currentFilePath;
    private bool _isDirty;
    private DesignElement? _clipboard;

    public Action? RequestRedraw { get; set; }

    public string DocumentTitle
    {
        get
        {
            var name = _currentFilePath != null
                ? System.IO.Path.GetFileNameWithoutExtension(_currentFilePath)
                : "Untitled";
            return _isDirty ? $"{name}* — LabelDesigner" : $"{name} — LabelDesigner";
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
        OnPropertyChanged(nameof(DocumentTitle));
    }

    private void ClearDirty()
    {
        _isDirty = false;
        OnPropertyChanged(nameof(DocumentTitle));
    }

    private PlacementMode _placementMode = PlacementMode.None;
    private DesignElement? _pendingElement;

    public bool IsInLinePlacementMode => _placementMode == PlacementMode.LineClickDrag;
    public bool IsInPlacementMode => _placementMode != PlacementMode.None;
    public Guid? ActiveLayerId => GetTargetLayerId();

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
        Layers.Refresh();
        Layers.SelectedLayer = Layers.Layers.FirstOrDefault(l => l.LayerId == layer.Id);
        _interaction.SnapEnabled = AppSettingsService.ShowSnapGrid;

        // Record the DPI in use at construction time (system DPI fallback). If
        // the actual window DPI is different (high-DPI monitor), RefreshDpiDependentState
        // will rescale these default elements to the correct window DPI.
        _constructionPixelsPerMm = DpiService.PixelsPerMm;

        _scene.AddElement(new BarcodeElement
        {
            Bounds = new RectD(10 * PixelsPerMm, 10 * PixelsPerMm, 50 * PixelsPerMm, 20 * PixelsPerMm),
            Value = "ABC123456",
            TextPosition = BarcodeTextPosition.Top
        }, layer.Id);

        _scene.AddElement(new TextElement
        {
            Bounds = new RectD(15 * PixelsPerMm, 35 * PixelsPerMm, 50 * PixelsPerMm, 10 * PixelsPerMm),
            Text = "Hello World"
        }, layer.Id);

        _ = LoadAvailablePrintersAsync();
    }

    private async Task LoadAvailablePrintersAsync()
    {
        try
        {
            const string printerSelector = "System.Devices.InterfaceClassGuid:=\"{0ecef634-6ef0-472a-8085-5ad023ecbccd}\"";
            var devices = await DeviceInformation.FindAllAsync(printerSelector);
            AvailablePrinters.Clear();
            foreach (var device in devices)
            {
                if (!string.IsNullOrWhiteSpace(device.Name))
                    AvailablePrinters.Add(device.Name);
            }

            if (AvailablePrinters.Count > 0)
                SelectedPrinter = AvailablePrinters[0];
        }
        catch
        {
            AvailablePrinters.Clear();
        }

        OnPropertyChanged(nameof(PrinterListSummary));
    }

    [RelayCommand]
    private async Task RefreshPrinters()
    {
        await LoadAvailablePrintersAsync();
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
    private void NewDocument()
    {
        _scene.Clear();
        _scene.AddLayer("Layer 1");
        _currentFilePath = null;
        ClearDirty();
    }

    [RelayCommand]
    private void OpenDocument()
    {
        _ = OpenDocumentAsync();
    }

    private async Task OpenDocumentAsync()
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
    private void SaveDocument()
    {
        _ = SaveDocumentAsync();
    }

    private async Task SaveDocumentAsync()
    {
        try
        {
            if (_currentFilePath == null)
            {
                await SaveAsDocumentAsync();
                return;
            }
            var saveDoc = await BuildMmSaveDocumentAsync();
            await _persistence.SaveAsync(saveDoc, _currentFilePath);
            AppSettingsService.AddRecentFile(_currentFilePath);
            ClearDirty();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Error", $"Could not save document: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveAsDocument()
    {
        _ = SaveAsDocumentAsync();
    }

    private async Task SaveAsDocumentAsync()
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
            var saveDoc = await BuildMmSaveDocumentAsync();
            await _persistence.SaveAsync(saveDoc, file.Path);
            _currentFilePath = file.Path;
            AppSettingsService.AddRecentFile(file.Path);
            ClearDirty();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Error", $"Could not save document: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSelectedRecent()
    {
        _ = OpenSelectedRecentAsync();
    }

    private async Task OpenSelectedRecentAsync()
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
        NotifyElementsChanged();
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    public void Redo()
    {
        _undoRedo.Redo();
        Selected = _scene.SingleSelected;
        NotifyElementsChanged();
        RequestRedraw?.Invoke();
    }

    public void NotifyElementsChanged()
    {
        OnPropertyChanged(nameof(ElementsText));
        Layers.Refresh(Selected?.Id);
        MarkDirty();
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
        OnPropertyChanged(nameof(MarginTop));
        OnPropertyChanged(nameof(MarginRight));
        OnPropertyChanged(nameof(MarginBottom));
        OnPropertyChanged(nameof(MarginLeft));
        RequestRedraw?.Invoke();
    }

    public void RefreshDpiDependentState()
    {
        double newPpm = DpiService.PixelsPerMm;
        double oldPpm = _constructionPixelsPerMm;

        // If window DPI differs from the DPI used when default elements were created,
        // rescale all element bounds so they remain at the intended mm positions.
        if (IsValidPixelsPerMm(newPpm) && IsValidPixelsPerMm(oldPpm) && Math.Abs(newPpm - oldPpm) > 0.01)
            RescaleElementBounds(_scene.CurrentDocument, newPpm / oldPpm);

        // Update so repeated calls (window moved to different monitor) rescale correctly.
        if (IsValidPixelsPerMm(newPpm))
            _constructionPixelsPerMm = newPpm;

        OnPropertyChanged(nameof(PixelsPerMm));
        OnPropertyChanged(nameof(CursorText));
        OnPropertyChanged(nameof(RulerUnitText));
        UpdatePageBounds();
        RequestRedraw?.Invoke();
    }

    // Builds a deep-cloned SceneDocument with all element bounds stored in mm
    // (device-independent). Used exclusively for serialization.
    private async Task<SceneDocument> BuildMmSaveDocumentAsync()
    {
        double ppm = PixelsPerMm;
        // JSON round-trip gives a clean deep clone with no live references
        var json = await _persistence.SaveToJsonAsync(_scene.CurrentDocument);
        var clone = await _persistence.LoadFromJsonAsync(json);
        clone.Version = "2.0";
        clone.Page.Dpi = (int)Math.Round(ppm * 25.4);
        foreach (var el in clone.AllElements)
        {
            el.Bounds = new RectD(
                el.Bounds.X / ppm, el.Bounds.Y / ppm,
                el.Bounds.Width / ppm, el.Bounds.Height / ppm);
            if (el is LineElement ln)
            {
                ln.X1 /= ppm; ln.Y1 /= ppm;
                ln.X2 /= ppm; ln.Y2 /= ppm;
            }
        }
        return clone;
    }

    // Converts a loaded document's element bounds from mm → screen pixels
    // using the current PixelsPerMm. Only applied to V2.0 documents.
    private static void ConvertMmBoundsToPixels(SceneDocument doc, double ppm)
    {
        if (!IsValidPixelsPerMm(ppm))
            return;

        RescaleElementBounds(doc, ppm);
    }

    // Multiplies all element bounds (and line endpoints) by the given scale factor.
    private static void RescaleElementBounds(SceneDocument doc, double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            return;

        foreach (var el in doc.AllElements)
        {
            el.Bounds = new RectD(
                el.Bounds.X * scale, el.Bounds.Y * scale,
                el.Bounds.Width * scale, el.Bounds.Height * scale);
            if (el is LineElement ln)
            {
                ln.X1 *= scale; ln.Y1 *= scale;
                ln.X2 *= scale; ln.Y2 *= scale;
            }
        }
    }

    private static bool IsValidPixelsPerMm(double value) =>
        !double.IsNaN(value) &&
        !double.IsInfinity(value) &&
        value > 0.01;

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
        Guides.Clear();
        OnPropertyChanged(nameof(ElementCount));
        OnPropertyChanged(nameof(ElementsText));
        OnPropertyChanged(nameof(ZoomText));
        OnPropertyChanged(nameof(CursorText));
        OnPropertyChanged(nameof(RulerUnitText));
        OnPropertyChanged(nameof(SnapStateText));
        OnPropertyChanged(nameof(RecentFiles));
        OnPropertyChanged(nameof(DataMergeModeIndex));
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
        ClearDataMergeState();
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
        var layerId = GetTargetLayerId();
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
        var layerId = container.ParentId ?? GetTargetLayerId();
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
            Bounds = new RectD(0, 0, 50 * PixelsPerMm, 20 * PixelsPerMm),
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
            Bounds = new RectD(0, 0, 40 * PixelsPerMm, 8 * PixelsPerMm),
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
    private void SaveTemplate()
    {
        _ = SaveTemplateAsync();
    }

    private async Task SaveTemplateAsync()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        savePicker.SuggestedFileName = "Template.ldtemplate";
        savePicker.FileTypeChoices.Add("Label Template", new[] { ".ldtemplate" });

        var file = await savePicker.PickSaveFileAsync();
        if (file == null) return;

        try
        {
            var saveDoc = await BuildMmSaveDocumentAsync();
            saveDoc.DataSource = null; // Templates should not be saved with merged datafile
            await _persistence.SaveAsync(saveDoc, file.Path);
            // Template save is an export — does NOT change the current document path
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Save Error", $"Could not save template: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NewFromTemplate()
    {
        _ = NewFromTemplateAsync();
    }

    private async Task NewFromTemplateAsync()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".ldtemplate");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        await OpenDocumentFromPath(file.Path);
    }

    [RelayCommand]
    private void Print()
    {
        _ = PrintAsync();
    }

    private async Task PrintAsync()
    {
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Print Error", "Could not access the application window.");
            return;
        }

        try
        {
            await Task.Yield();
            _printService.PixelsPerMm = DpiService.PixelsPerMm;

            // Apply data merge if a data source is loaded; otherwise print raw template
            var ds = CloneDataSourceConfig(_scene.CurrentDocument.DataSource);
            IReadOnlyList<SceneDocument> documents;
            string jobTitle;
            if (ds != null)
            {
                documents = await BuildMailMergePrintDocumentsAsync(ds);
                if (documents.Count == 0)
                    documents = BuildCurrentPrintDocuments();
                jobTitle = BuildMailMergeJobTitle(ds, documents.Count);
            }
            else
            {
                documents = BuildCurrentPrintDocuments();
                jobTitle = "Label";
            }

            await _printService.PrintAsync(documents, hwnd, jobTitle);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Print Error", $"Could not open the Windows print window: {ex.GetType().Name} — {ex.Message}");
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        _ = ExportPdfAsync();
    }

    private async Task ExportPdfAsync()
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
            var options = new Core.Interfaces.PdfExportOptions { PixelsPerMm = DpiService.PixelsPerMm };
            var ds = CloneDataSourceConfig(_scene.CurrentDocument.DataSource);
            if (ds != null)
            {
                // Export all merged records as a multi-page PDF
                var documents = await BuildMailMergePrintDocumentsAsync(ds);
                if (documents.Count == 0)
                    documents = BuildCurrentPrintDocuments();
                await _pdfExportService.ExportAsync(documents, file.Path, options);
            }
            else
            {
                await _pdfExportService.ExportAsync(_scene.CurrentDocument, file.Path, options);
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Export Error", $"Could not export PDF: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadDataSource()
    {
        _ = LoadDataSourceAsync();
    }

    private async Task LoadDataSourceAsync()
    {
        if (_isLoadingDataSource)
            return;

        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Error", "Could not access main window");
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xls");
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        _isLoadingDataSource = true;
        ++_worksheetSwitchVersion;
        try
        {
            var selectedWorksheetName = default(string);
            if (IsExcelFilePath(file.Path))
            {
                var worksheetNames = await _dataSource.GetWorksheetNamesAsync(file.Path);
                selectedWorksheetName = ResolveWorksheetName(worksheetNames, null);
                SetAvailableWorksheetNames(worksheetNames, selectedWorksheetName);
            }
            else
            {
                ClearWorksheetNames();
            }

            var gridModel = await GetOrLoadGridModelAsync(file.Path, selectedWorksheetName);

            var mergeMode = _scene.CurrentDocument.DataSource?.MergeMode ?? nameof(DataMergeMode.OneRecordPerPage);
            _scene.CurrentDocument.DataSource = new DataSourceConfig
            {
                Type = Path.GetExtension(file.Path).TrimStart('.'),
                Path = file.Path,
                WorksheetName = selectedWorksheetName,
                MergeMode = mergeMode
            };

            ApplyGridModel(gridModel, file.Path, selectedWorksheetName);
            OnPropertyChanged(nameof(DataMergeModeIndex));
        }
        catch (FileNotFoundException ex)
        {
            ShowErrorDialog("File Not Found", $"Could not find the data source file: {ex.Message}");
            ClearDataMergeState();
        }
        catch (System.Text.Json.JsonException ex)
        {
            ShowErrorDialog("Invalid Data Format", $"The file format is invalid: {ex.Message}");
            ClearDataMergeState();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Data Source Error", $"Could not load data source: {ex.GetType().Name} — {ex.Message}");
            ClearDataMergeState();
        }
        finally
        {
            _isLoadingDataSource = false;
        }
    }

    [RelayCommand]
    private void PrintWithData()
    {
        _ = PrintWithDataAsync();
    }

    private async Task PrintWithDataAsync()
    {
        if (_isPrintingWithData)
            return;

        _isPrintingWithData = true;
        if (!TryGetWindowHandle(out var hwnd))
        {
            ShowErrorDialog("Print Error", "Could not access the application window.");
            _isPrintingWithData = false;
            return;
        }

        var ds = CloneDataSourceConfig(_scene.CurrentDocument.DataSource);
        if (ds == null) 
        { 
            await PrintAsync();
            _isPrintingWithData = false;
            return; 
        }

        try
        {
            await Task.Yield();
            _printService.PixelsPerMm = DpiService.PixelsPerMm;
            var documents = await BuildMailMergePrintDocumentsAsync(ds);
            if (documents == null || documents.Count == 0)
            {
                ShowErrorDialog("No Documents", "No documents to print. Please verify your data source and merge settings.");
                return;
            }

            await _printService.PrintAsync(documents, hwnd, BuildMailMergeJobTitle(ds, documents.Count));
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
            ShowErrorDialog("Print Error", $"Could not open the Windows print window: {ex.GetType().Name} — {ex.Message}");
        }
        finally
        {
            _isPrintingWithData = false;
        }
    }

    [RelayCommand]
    private void PreviewPrint()
    {
        _ = PreviewPrintAsync();
    }

    private async Task PreviewPrintAsync()
    {
        // Yield to let Syncfusion finish its button-click processing before opening the dialog.
        await Task.Yield();

        try
        {
            var ds = CloneDataSourceConfig(_scene.CurrentDocument.DataSource);
            var documents = ds == null
                ? BuildCurrentPrintDocuments()
                : await BuildMailMergePrintDocumentsAsync(ds);

            var previewTitle = BuildMailMergeJobTitle(ds, documents.Count);
            var xamlRoot = App.MainWindow?.Content.XamlRoot;
            if (xamlRoot == null)
                throw new InvalidOperationException("Could not open the preview window.");

            var root = App.MainWindow?.Content as FrameworkElement;
            var preview = new Views.MergePreviewView
            {
                DataContext = root?.DataContext
            };
            preview.SetDocuments(documents);

            var dialog = new ContentDialog
            {
                Title = previewTitle,
                Content = preview,
                CloseButtonText = "Close",
                XamlRoot = xamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Preview Error", $"Could not open the Windows print preview: {ex.Message}");
        }
    }

    private IReadOnlyList<SceneDocument> BuildCurrentPrintDocuments()
    {
        return new[] { _scene.CurrentDocument };
    }

    private async Task<IReadOnlyList<SceneDocument>> BuildMailMergePrintDocumentsAsync(DataSourceConfig dataSource)
    {
        try
        {
            var selectionSnapshot = _selectedMergeRows.ToArray();
            var records = await GetActiveMergeRecordsAsync(dataSource);
            if (records == null || records.Count == 0)
                return Array.Empty<SceneDocument>();

            var selectedRecords = MergeRecordSelector.SelectActiveRecords(selectionSnapshot, records);
            if (selectionSnapshot.Length > 0 && selectedRecords.Count == 0)
                return Array.Empty<SceneDocument>();

            var activeRecords = selectedRecords.Count > 0 ? selectedRecords : records;

            var originalDoc = _scene?.CurrentDocument;
            if (originalDoc == null)
                return BuildCurrentPrintDocuments();

            if (GetDataMergeMode() == DataMergeMode.MultipleRecordsPerPage)
            {
                var pages = BuildMergedPages(originalDoc, activeRecords);
                return pages.Count == 0 ? BuildCurrentPrintDocuments() : pages;
            }

            return activeRecords
                .Select(record => _dataBinding.ApplyRecord(originalDoc, record))
                .ToList();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Building Merge Documents", $"Could not build mail merge documents: {ex.Message}");
            return BuildCurrentPrintDocuments();
        }
    }

    private string BuildMailMergeJobTitle(DataSourceConfig? dataSource, int pageCount)
    {
        var name = dataSource == null || string.IsNullOrWhiteSpace(dataSource.Path)
            ? "Mail Merge"
            : $"Mail Merge - {Path.GetFileNameWithoutExtension(dataSource.Path)}";
        if (!string.IsNullOrWhiteSpace(dataSource?.WorksheetName))
            name += $" [{dataSource.WorksheetName}]";

        return pageCount <= 1 ? name : $"{name} ({pageCount} pages)";
    }

    [RelayCommand]
    private void ExportPng()
    {
        _ = ExportPngAsync();
    }

    private async Task ExportPngAsync()
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
            _printService.PixelsPerMm = DpiService.PixelsPerMm;
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
        var layerId = GetTargetLayerId();
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
                Symbology = b.Symbology,
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
            Bounds = new RectD(0, 0, 30 * PixelsPerMm, 20 * PixelsPerMm),
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
    private void AddImage()
    {
        _ = AddImageAsync();
    }

    private async Task AddImageAsync()
    {
        ActiveTool = ToolMode.PlaceImage;
        await PickAndPlaceImage(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg" });
    }

    [RelayCommand]
    private void AddSvg()
    {
        _ = AddSvgAsync();
    }

    private async Task AddSvgAsync()
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
            Bounds = new RectD(0, 0, 40 * PixelsPerMm, 40 * PixelsPerMm),
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
            Bounds = new RectD(0, 0, 40 * PixelsPerMm, 40 * PixelsPerMm),
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
        RequestRedraw?.Invoke();
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
        // Save user-added guides before interaction (don't lose them during drag/resize)
        _savedUserGuides = new List<GuideLine>(Guides);
        Guides.Clear();
        var p = Viewport.ScreenToWorld(screenPoint);
        var pD = new PointD(p.X, p.Y);

        // Placement mode: place the pending element at cursor
        if (_placementMode == PlacementMode.PlaceOnce && _pendingElement != null)
        {
            var page = _scene.CurrentDocument.Page;
            var pw = page.WidthMm * PixelsPerMm;
            var ph = page.HeightMm * PixelsPerMm;
            var layerId = GetTargetLayerId();

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
            var update = _interaction.UpdateDrag(pD, Selected, otherBounds, GetPageRect(), PixelsPerMm);

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
        
        // Restore user-added guides after interaction
        Guides.Clear();
        Guides.AddRange(_savedUserGuides);
        
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

    private Guid? GetTargetLayerId()
    {
        if (Layers.SelectedLayer is { } selectedLayer)
            return selectedLayer.LayerId;

        return _scene.CurrentDocument.Layers.FirstOrDefault()?.Id;
    }

    [RelayCommand]
    private void SetMergeModeOneRecordPerPage()
    {
        if (_scene.CurrentDocument.DataSource == null)
            return;

        _scene.CurrentDocument.DataSource.MergeMode = nameof(DataMergeMode.OneRecordPerPage);
        OnPropertyChanged(nameof(DataMergeModeIndex));
    }

    [RelayCommand]
    private void SetMergeModeMultipleRecordsPerPage()
    {
        if (_scene.CurrentDocument.DataSource == null)
            return;

        _scene.CurrentDocument.DataSource.MergeMode = nameof(DataMergeMode.MultipleRecordsPerPage);
        OnPropertyChanged(nameof(DataMergeModeIndex));
    }

    /// <summary>Sets barcode Value to {{fieldName}} template token, binding it to the named CSV column.</summary>
    [RelayCommand]
    private void BindBarcodeToField(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || Selected is not BarcodeElement b) return;
        b.Value = $"{{{{{fieldName}}}}}";
        _properties.TrackElement(b);
        RequestRedraw?.Invoke();
    }

    /// <summary>Sets text element Text to {{fieldName}} template token, binding it to the named CSV column.</summary>
    [RelayCommand]
    private void BindTextToField(string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || Selected is not TextElement t) return;
        t.Text = $"{{{{{fieldName}}}}}";
        _properties.TrackElement(t);
        RequestRedraw?.Invoke();
    }

    [RelayCommand]
    private void ToggleDataMergePane()
    {
        WorkspaceTabIndex = WorkspaceTabIndex == 1 ? 0 : 1;
    }

    [RelayCommand]
    private void ShowDataMergePane()
    {
        WorkspaceTabIndex = 1;
    }

    [RelayCommand]
    private void ShowDesignerWorkspace()
    {
        WorkspaceTabIndex = 0;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        WorkspaceTabIndex = 2;
    }

    [RelayCommand]
    private void BindSelectedFieldToBarcode()
    {
        BindBarcodeToField(SelectedDataField);
    }

    [RelayCommand]
    private void BindSelectedFieldToText()
    {
        BindTextToField(SelectedDataField);
    }

    private DataMergeMode GetDataMergeMode()
    {
        var modeText = _scene.CurrentDocument.DataSource?.MergeMode;
        if (string.IsNullOrWhiteSpace(modeText))
            return DataMergeMode.OneRecordPerPage;

        return Enum.TryParse<DataMergeMode>(modeText, true, out var mode)
            ? mode
            : DataMergeMode.OneRecordPerPage;
    }

    private IReadOnlyList<SceneDocument> BuildMergedPages(
        SceneDocument originalDoc,
        IReadOnlyList<IReadOnlyDictionary<string, string>> records)
    {
        var contentBounds = CalculateContentBounds(originalDoc.AllElements);
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return Array.Empty<SceneDocument>();

        var pageRect = GetPageRect();
        var columns = Math.Max(1, (int)Math.Floor(pageRect.Width / contentBounds.Width));
        var rows = Math.Max(1, (int)Math.Floor(pageRect.Height / contentBounds.Height));
        var slotsPerPage = Math.Max(1, columns * rows);
        var pages = new List<SceneDocument>();

        for (int start = 0; start < records.Count; start += slotsPerPage)
        {
            var pageDoc = CreatePageCloneWithoutElements(originalDoc);
            var end = Math.Min(start + slotsPerPage, records.Count);

            for (int i = start; i < end; i++)
            {
                var slot = i - start;
                var col = slot % columns;
                var row = slot / columns;
                var offsetX = (col * contentBounds.Width) - contentBounds.X;
                var offsetY = (row * contentBounds.Height) - contentBounds.Y;
                var boundDoc = _dataBinding.ApplyRecord(originalDoc, records[i]);

                AppendDocumentElements(pageDoc, boundDoc, offsetX, offsetY);
            }

            pages.Add(pageDoc);
        }

        return pages;
    }

    private static RectD CalculateContentBounds(IEnumerable<DesignElement> elements)
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        var hasElement = false;

        foreach (var element in elements)
        {
            var bounds = element.Bounds;
            minX = Math.Min(minX, bounds.Left);
            minY = Math.Min(minY, bounds.Top);
            maxX = Math.Max(maxX, bounds.Right);
            maxY = Math.Max(maxY, bounds.Bottom);
            hasElement = true;
        }

        return hasElement ? new RectD(minX, minY, maxX - minX, maxY - minY) : new RectD(0, 0, 0, 0);
    }

    private static SceneDocument CreatePageCloneWithoutElements(SceneDocument source)
    {
        var clone = new SceneDocument
        {
            Version = source.Version,
            Page = new PageNode
            {
                WidthMm = source.Page.WidthMm,
                HeightMm = source.Page.HeightMm,
                Dpi = source.Page.Dpi,
                Margins = new Margins(
                    source.Page.Margins.Left,
                    source.Page.Margins.Top,
                    source.Page.Margins.Right,
                    source.Page.Margins.Bottom)
            },
            Defaults = source.Defaults,
            DataSource = source.DataSource == null
                ? null
                : new DataSourceConfig
                {
                    Type = source.DataSource.Type,
                    Path = source.DataSource.Path,
                    WorksheetName = source.DataSource.WorksheetName,
                    MergeMode = source.DataSource.MergeMode
                }
        };

        foreach (var layer in source.Layers)
        {
            clone.Layers.Add(new LayerNode
            {
                Id = layer.Id,
                Name = layer.Name,
                Visible = layer.Visible,
                Locked = layer.Locked
            });
        }

        return clone;
    }

    private static void AppendDocumentElements(SceneDocument target, SceneDocument source, double offsetX, double offsetY)
    {
        foreach (var element in source.AllElements)
        {
            var clone = DeepCloneWithNewId(element);
            clone.Bounds = new RectD(
                clone.Bounds.X + offsetX,
                clone.Bounds.Y + offsetY,
                clone.Bounds.Width,
                clone.Bounds.Height);

            if (clone is LineElement line)
            {
                line.X1 += offsetX;
                line.Y1 += offsetY;
                line.X2 += offsetX;
                line.Y2 += offsetY;
            }

            target.AllElements.Add(clone);

            var sourceLayer = source.Layers.FirstOrDefault(l => l.ElementIds.Contains(element.Id));
            var targetLayer = sourceLayer == null
                ? target.Layers.FirstOrDefault()
                : target.Layers.FirstOrDefault(l => l.Id == sourceLayer.Id);
            targetLayer?.ElementIds.Add(clone.Id);
        }
    }

    public ResizeHandle GetHoverHandle(PointD pD)
    {
        return _interaction.GetHoverHandle(Selected, pD, Viewport.Zoom);
    }

    private void OnSettingsChanged()
    {
        _interaction.SnapEnabled = AppSettingsService.ShowSnapGrid;
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
            bool isTemplate = string.Equals(Path.GetExtension(filePath), ".ldtemplate", StringComparison.OrdinalIgnoreCase);
            if (isTemplate)
            {
                doc.DataSource = null; // Templates should not be loaded with merged datafile
            }
            if (doc.Version == "2.0")
            {
                // V2.0: bounds stored in mm → convert to screen pixels at current window DPI
                ConvertMmBoundsToPixels(doc, PixelsPerMm);
            }
            else if (doc.Page.Dpi > 0)
            {
                // Legacy V1: bounds stored in pixels. If saved on a different DPI,
                // rescale into current screen pixels using stored page DPI.
                var currentDpi = PixelsPerMm * 25.4;
                if (Math.Abs(currentDpi - doc.Page.Dpi) > 0.1)
                    RescaleElementBounds(doc, currentDpi / doc.Page.Dpi);
            }
            else
            {
                // V1.0: bounds stored in screen pixels from the save-time DPI.
                // Best-effort correction: assume the file was saved when the app used
                // system DPI (the fallback before InitializeForWindow). If window DPI
                // differs, rescale so elements appear at the intended mm positions.
                double sysPpm = DpiService.SystemPixelsPerMm;
                double winPpm = PixelsPerMm;
                if (Math.Abs(sysPpm - winPpm) > 0.05 && sysPpm > 0)
                    RescaleElementBounds(doc, winPpm / sysPpm);
            }
            _scene.Load(doc);
            // Opening a template starts a new untitled document — don't track template path
            _currentFilePath = isTemplate ? null : filePath;
            if (!isTemplate)
            {
                AppSettingsService.AddRecentFile(filePath);
                SelectedRecentFile = filePath;
            }
            ClearDirty();
            if (isTemplate)
            {
                ClearDataMergeState();
            }
            else
            {
                await RestoreLoadedDataSourceAsync(doc.DataSource);
            }
            OnPropertyChanged(nameof(DataMergeModeIndex));
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

    private void LoadDataMergeRecords(
        IReadOnlyList<IReadOnlyDictionary<string, string>> records,
        string sourcePath,
        string? worksheetName = null)
    {
        try
        {
            var gridModel = DataMergeGridModelBuilder.Build(records);
            _worksheetDataCache.Store(sourcePath, worksheetName, gridModel);
            ApplyGridModel(gridModel, sourcePath, worksheetName);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Loading Data", $"An error occurred while loading the data source: {ex.Message}");
            ClearDataMergeState();
        }
    }

    private void ApplyGridModel(DataMergeGridModel gridModel, string sourcePath, string? worksheetName)
    {
        _csvRecords = gridModel.Records.ToList();

        _dataMergeColumns.Clear();
        _dataMergeColumns.AddRange(gridModel.Columns);

        DataMergeItemsSource.Clear();
        foreach (var row in gridModel.Rows)
            DataMergeItemsSource.Add(row);

        AvailableDataFields.Clear();
        foreach (var column in gridModel.Columns)
            AvailableDataFields.Add(column.HeaderText);

        if (string.IsNullOrWhiteSpace(SelectedDataField) || !AvailableDataFields.Contains(SelectedDataField))
            SelectedDataField = AvailableDataFields.FirstOrDefault();

        _loadedDataSourcePath = sourcePath;
        _loadedWorksheetName = NormalizeWorksheetName(worksheetName);
        SetSelectedWorksheetNameSilently(_loadedWorksheetName);
        _selectedMergeRows.Clear();
        SelectedDataMergeRow = null;
        PreviewRecordIndex = DataMergeItemsSource.Count > 0 ? 0 : -1;
        WorkspaceTabIndex = 1;

        OnPropertyChanged(nameof(HasDataFields));
        OnPropertyChanged(nameof(DataMergeItemsSource));
        OnPropertyChanged(nameof(DataMergeColumns));
        OnPropertyChanged(nameof(HasLoadedDataSource));
        OnPropertyChanged(nameof(HasNoLoadedDataSource));
        OnPropertyChanged(nameof(HasLoadedDataSourceButNoMergeBindings));
        OnPropertyChanged(nameof(HasMultipleWorksheets));
        OnPropertyChanged(nameof(DataSourceSummary));
        OnPropertyChanged(nameof(SelectedMergeRecordCount));
        OnPropertyChanged(nameof(HasSelectedMergeRecords));
        OnPropertyChanged(nameof(PrintDataButtonText));
        OnPropertyChanged(nameof(DataMergeActionSummary));
        OnPropertyChanged(nameof(PreviewRecordCount));
        OnPropertyChanged(nameof(PreviewRecordText));
        RefreshMergeBindings();
    }

    private void ClearDataMergeState()
    {
        ++_worksheetSwitchVersion;
        DataMergeItemsSource.Clear();
        _dataMergeColumns.Clear();
        ClearWorksheetNames();
        _worksheetDataCache.Clear();
        AvailableDataFields.Clear();
        MergeBindings.Clear();
        _csvRecords = new();
        _loadedDataSourcePath = null;
        _loadedWorksheetName = null;
        _selectedMergeRows.Clear();
        SelectedDataField = null;
        SelectedDataMergeRow = null;
        PreviewRecordIndex = -1;
        WorkspaceTabIndex = 0;
        RefreshPreviewDocument();

        OnPropertyChanged(nameof(HasDataFields));
        OnPropertyChanged(nameof(HasMergeBindings));
        OnPropertyChanged(nameof(HasNoMergeBindings));
        OnPropertyChanged(nameof(HasLoadedDataSourceButNoMergeBindings));
        OnPropertyChanged(nameof(DataMergeItemsSource));
        OnPropertyChanged(nameof(DataMergeColumns));
        OnPropertyChanged(nameof(HasLoadedDataSource));
        OnPropertyChanged(nameof(HasNoLoadedDataSource));
        OnPropertyChanged(nameof(HasMultipleWorksheets));
        OnPropertyChanged(nameof(DataSourceSummary));
        OnPropertyChanged(nameof(SelectedMergeRecordCount));
        OnPropertyChanged(nameof(HasSelectedMergeRecords));
        OnPropertyChanged(nameof(PrintDataButtonText));
        OnPropertyChanged(nameof(PreviewRecordCount));
        OnPropertyChanged(nameof(PreviewRecordText));
        OnPropertyChanged(nameof(DataMergeActionSummary));
    }

    /// <summary>Rebuilds MergeBindings from the current document elements and available CSV columns.</summary>
    private void RefreshMergeBindings()
    {
        MergeBindings.Clear();
        if (!HasLoadedDataSource) return;

        var doc = _scene?.CurrentDocument;
        if (doc == null) return;

        var columns = new List<string>(AvailableDataFields.Count + 1) { "(none)" };
        foreach (var c in AvailableDataFields)
            columns.Add(c);

        foreach (var el in doc.AllElements)
        {
            string type, name;
            string? boundCol;

            if (el is TextElement txt)
            {
                type = "Text";
                name = string.IsNullOrWhiteSpace(el.Name) ? "Text" : el.Name;
                boundCol = ExtractMergeToken(txt.Text);
            }
            else if (el is BarcodeElement bc)
            {
                type = "Barcode";
                name = string.IsNullOrWhiteSpace(el.Name) ? "Barcode" : el.Name;
                boundCol = ExtractMergeToken(bc.Value);
            }
            else
            {
                continue;
            }

            var item = new MergeBindingItem
            {
                ElementId = el.Id,
                DisplayName = name,
                ElementType = type,
                AvailableColumns = columns,
                OnColumnSelected = OnMergeBindingColumnSelected
            };
            item.SetColumnSilently(boundCol);
            MergeBindings.Add(item);
        }

        OnPropertyChanged(nameof(HasMergeBindings));
        OnPropertyChanged(nameof(HasNoMergeBindings));
        OnPropertyChanged(nameof(HasLoadedDataSourceButNoMergeBindings));
    }

    private void OnMergeBindingColumnSelected(MergeBindingItem item)
    {
        var el = _scene.CurrentDocument.AllElements.FirstOrDefault(e => e.Id == item.ElementId);
        if (el == null) return;

        var col = item.SelectedColumn;
        var isClear = col == null || col == "(none)";

        if (el is TextElement txt)
        {
            txt.Text = isClear ? "" : $"{{{{{col}}}}}";
            _properties.TrackElement(txt);
        }
        else if (el is BarcodeElement bc)
        {
            bc.Value = isClear ? "0" : $"{{{{{col}}}}}";
            _properties.TrackElement(bc);
        }

        RequestRedraw?.Invoke();
        RefreshPreviewDocument();
    }

    private static string? ExtractMergeToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"^\{\{(.+?)\}\}$");
        return m.Success ? m.Groups[1].Value : null;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> GetActiveMergeRecordsAsync(DataSourceConfig ds)
    {
        if (IsCurrentLoadedDataSource(ds.Path, ds.WorksheetName))
            return _csvRecords;

        if (_worksheetDataCache.TryGet(ds.Path, ds.WorksheetName, out var cachedModel) && cachedModel != null)
            return cachedModel.Records;

        var records = await _dataSource.LoadAsync(ds.Path, ds.WorksheetName);
        return records;
    }

    private async Task RestoreLoadedDataSourceAsync(DataSourceConfig? dataSource)
    {
        if (dataSource == null || string.IsNullOrWhiteSpace(dataSource.Path))
        {
            ClearDataMergeState();
            return;
        }

        if (!File.Exists(dataSource.Path))
        {
            ShowErrorDialog("Data Source Not Found", $"Could not find the data source file: {dataSource.Path}");
            ClearDataMergeState();
            return;
        }

        try
        {
            var worksheetNames = await GetAvailableWorksheetNamesAsync(dataSource.Path);
            var selectedWorksheetName = ResolveWorksheetName(worksheetNames, dataSource.WorksheetName);
            if (worksheetNames.Count > 0)
                SetAvailableWorksheetNames(worksheetNames, selectedWorksheetName);
            else
                ClearWorksheetNames();

            var gridModel = await GetOrLoadGridModelAsync(dataSource.Path, selectedWorksheetName);
            if (gridModel.Records.Count == 0)
            {
                ClearDataMergeState();
                return;
            }

            dataSource.WorksheetName = selectedWorksheetName;
            ApplyGridModel(gridModel, dataSource.Path, selectedWorksheetName);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Restoring Data Source", $"Could not restore the data source: {ex.Message}");
            ClearDataMergeState();
        }
    }

    private void RefreshPreviewDocument()
    {
        if (PreviewRecordIndex >= 0 && PreviewRecordIndex < _csvRecords.Count)
            _previewDocument = _dataBinding.ApplyRecord(_scene.CurrentDocument, _csvRecords[PreviewRecordIndex]);
        else
            _previewDocument = null;

        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(PreviewRecordText));
        OnPropertyChanged(nameof(MergePreviewDocument));
    }

    private async Task SwitchWorksheetAsync(string sourcePath, string worksheetName)
    {
        var previousWorksheetName = _loadedWorksheetName;
        var switchVersion = ++_worksheetSwitchVersion;

        try
        {
            var gridModel = await GetOrLoadGridModelAsync(sourcePath, worksheetName);
            if (switchVersion != _worksheetSwitchVersion)
                return;

            ApplyGridModel(gridModel, sourcePath, worksheetName);

            var dataSource = _scene.CurrentDocument.DataSource;
            if (dataSource != null)
            {
                dataSource.WorksheetName = worksheetName;
                MarkDirty();
            }
        }
        catch (Exception ex)
        {
            if (switchVersion != _worksheetSwitchVersion)
                return;

            SetSelectedWorksheetNameSilently(previousWorksheetName);
            ShowErrorDialog("Worksheet Error", $"Could not load worksheet '{worksheetName}': {ex.Message}");
        }
    }

    private async Task<DataMergeGridModel> GetOrLoadGridModelAsync(string sourcePath, string? worksheetName)
    {
        if (_worksheetDataCache.TryGet(sourcePath, worksheetName, out var cachedModel) && cachedModel != null)
            return cachedModel;

        var records = await _dataSource.LoadAsync(sourcePath, worksheetName);
        if (records == null || records.Count == 0)
            throw new InvalidOperationException("The selected data source contains no data records.");

        var gridModel = DataMergeGridModelBuilder.Build(records);
        _worksheetDataCache.Store(sourcePath, worksheetName, gridModel);
        return gridModel;
    }

    private async Task<IReadOnlyList<string>> GetAvailableWorksheetNamesAsync(string sourcePath)
    {
        if (!IsExcelFilePath(sourcePath))
            return Array.Empty<string>();

        return await _dataSource.GetWorksheetNamesAsync(sourcePath);
    }

    private void SetAvailableWorksheetNames(IEnumerable<string> worksheetNames, string? selectedWorksheetName)
    {
        AvailableWorksheetNames.Clear();
        foreach (var worksheetName in worksheetNames)
            AvailableWorksheetNames.Add(worksheetName);

        SetSelectedWorksheetNameSilently(selectedWorksheetName);
        OnPropertyChanged(nameof(HasMultipleWorksheets));
    }

    private void ClearWorksheetNames()
    {
        AvailableWorksheetNames.Clear();
        SetSelectedWorksheetNameSilently(null);
        OnPropertyChanged(nameof(HasMultipleWorksheets));
    }

    private void SetSelectedWorksheetNameSilently(string? worksheetName)
    {
        _isApplyingWorksheetSelection = true;
        SelectedWorksheetName = worksheetName;
        _isApplyingWorksheetSelection = false;
    }

    private static string? ResolveWorksheetName(IReadOnlyList<string> worksheetNames, string? preferredWorksheetName)
    {
        if (worksheetNames.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredWorksheetName))
        {
            var match = worksheetNames.FirstOrDefault(name => string.Equals(name, preferredWorksheetName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
                return match;
        }

        return worksheetNames[0];
    }

    private bool IsCurrentLoadedDataSource(string sourcePath, string? worksheetName)
    {
        return string.Equals(_loadedDataSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_loadedWorksheetName, NormalizeWorksheetName(worksheetName), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeWorksheetName(string? worksheetName)
    {
        return string.IsNullOrWhiteSpace(worksheetName)
            ? null
            : worksheetName.Trim();
    }

    private static bool IsExcelFilePath(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase);
    }

    private static DataSourceConfig? CloneDataSourceConfig(DataSourceConfig? dataSource)
    {
        if (dataSource == null)
            return null;

        return new DataSourceConfig
        {
            Type = dataSource.Type,
            Path = dataSource.Path,
            WorksheetName = dataSource.WorksheetName,
            MergeMode = dataSource.MergeMode
        };
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
