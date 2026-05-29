using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Syncfusion.UI.Xaml.DataGrid;
using System.Data;
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
        if (e.PropertyName == nameof(DesignerViewModel.DataMergeView)
            || e.PropertyName == nameof(DesignerViewModel.SelectedDataMergeRow))
            DispatcherQueue.TryEnqueue(RefreshDataGrid);
    }

    private void RefreshDataGrid()
    {
        if (_vm == null) return;

        try
        {
            _isRefreshingDataGrid = true;
            EnsureGridColumns();
            if (_vm.SelectedDataMergeRow != null && !ReferenceEquals(CsvDataGrid.SelectedItem, _vm.SelectedDataMergeRow))
                CsvDataGrid.SelectedItem = _vm.SelectedDataMergeRow;
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
        var table = _vm?.DataMergeItemsSource;
        var signature = table == null
            ? string.Empty
            : string.Join("|", table.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));

        if (string.Equals(_lastSchemaSignature, signature, StringComparison.Ordinal))
            return;

        CsvDataGrid.Columns.Clear();
        if (table != null)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Data.DataColumn column in table.Columns)
            {
                var mappingName = ToGridMappingName(column.ColumnName);
                if (!used.Add(mappingName))
                    continue;

                CsvDataGrid.Columns.Add(new GridTextColumn
                {
                    MappingName = mappingName,
                    HeaderText = column.ColumnName
                });
            }
        }

        _lastSchemaSignature = signature;
    }

    private static string ToGridMappingName(string columnName)
    {
        // DataRowView columns bind reliably through indexer syntax and avoid
        // collisions with reserved property names like "Item".
        return $"[{columnName}]";
    }

    private void OnGridSelectionChanged(object sender, object e)
    {
        if (_isRefreshingDataGrid || _vm == null)
            return;

        var selectedRows = CsvDataGrid.SelectedItems?.OfType<DataRowView>().ToList() ?? new List<DataRowView>();
        _vm.SetSelectedMergeRows(selectedRows);

        if (selectedRows.Count > 0 && !ReferenceEquals(_vm.SelectedDataMergeRow, selectedRows[0]))
            _vm.SelectedDataMergeRow = selectedRows[0];
        else if (selectedRows.Count == 0 && _vm.SelectedDataMergeRow != null)
            _vm.SelectedDataMergeRow = null;
    }
}
