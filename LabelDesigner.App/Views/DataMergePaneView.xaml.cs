using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.DataGrid;
using System.Dynamic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;

namespace LabelDesigner.App.Views;

public sealed partial class DataMergePaneView : UserControl
{
    private DesignerViewModel? _vm;
    private bool _isRefreshingDataGrid;
    private string? _lastSchemaSignature;

    private DesignerViewModel? VM => DataContext switch
    {
        MainViewModel main => main.Designer,
        DesignerViewModel d => d,
        _ => null
    };

    public DataMergePaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => WireViewModel();
    private void OnUnloaded(object sender, RoutedEventArgs e) => UnwireViewModel();
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => WireViewModel();

    private void WireViewModel()
    {
        var next = VM;
        if (_vm == next) return;
        UnwireViewModel();
        _vm = next;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            // Defer DataGrid refresh until after layout to avoid SfDataGrid timing issues
            DispatcherQueue.TryEnqueue(RefreshDataGrid);
        }
    }

    private void UnwireViewModel()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignerViewModel.DataMergeItemsSource)
            || e.PropertyName == nameof(DesignerViewModel.DataMergeColumns))
            DispatcherQueue.TryEnqueue(RefreshDataGrid);
    }

    private void RefreshDataGrid()
    {
        if (_vm == null) return;

        try
        {
            _isRefreshingDataGrid = true;
            EnsureGridColumns();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Data merge grid refresh skipped: {ex}");
        }
        finally
        {
            _isRefreshingDataGrid = false;
        }
    }

    private void EnsureGridColumns()
    {
        var columns = _vm?.DataMergeColumns;
        var signature = columns == null
            ? string.Empty
            : string.Join("|", columns.Select(column => $"{column.MappingName}:{column.HeaderText}"));

        if (string.Equals(_lastSchemaSignature, signature, StringComparison.Ordinal))
            return;

        CsvDataGrid.Columns.Clear();
        if (columns != null)
        {
            foreach (var column in columns)
            {
                CsvDataGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = column.MappingName,
                    HeaderText = column.HeaderText
                });
            }
        }

        _lastSchemaSignature = signature;
    }

    private void OnGridSelectionChanged(object sender, object e)
    {
        if (_isRefreshingDataGrid || _vm == null)
            return;

        var selectedRows = CsvDataGrid.SelectedItems?.OfType<ExpandoObject>().ToList() ?? new List<ExpandoObject>();
        _vm.SetSelectedMergeRows(selectedRows);

        if (selectedRows.Count > 0 && !ReferenceEquals(_vm.SelectedDataMergeRow, selectedRows[0]))
            _vm.SelectedDataMergeRow = selectedRows[0];
    }
}
