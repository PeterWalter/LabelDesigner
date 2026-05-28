using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Data;
using System.Diagnostics;
using System.ComponentModel;
using System.Linq;

namespace LabelDesigner.App.Views;

public sealed partial class DataMergePaneView : UserControl
{
    private DesignerViewModel? _vm;
    private bool _isRefreshingDataGrid;
    private string? _lastGridSchema;

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
            || e.PropertyName == nameof(DesignerViewModel.DataMergeItemsSource)
            || e.PropertyName == nameof(DesignerViewModel.HasLoadedDataSource))
            DispatcherQueue.TryEnqueue(RefreshDataGrid);
    }

    private void RefreshDataGrid()
    {
        try
        {
            if (_vm == null) return;

            _isRefreshingDataGrid = true;
            var table = _vm.DataMergeItemsSource;
            var schema = BuildSchemaKey(table);
            var schemaChanged = !string.Equals(_lastGridSchema, schema, StringComparison.Ordinal);

            if (schemaChanged)
            {
                CsvDataGrid.ItemsSource = null;
                CsvDataGrid.ItemsSource = table;
                _lastGridSchema = schema;
            }
            else if (!ReferenceEquals(CsvDataGrid.ItemsSource, table))
            {
                CsvDataGrid.ItemsSource = table;
            }

            if (_vm.SelectedDataMergeRow != null && !ReferenceEquals(CsvDataGrid.SelectedItem, _vm.SelectedDataMergeRow))
                CsvDataGrid.SelectedItem = _vm.SelectedDataMergeRow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DataMergePaneView.RefreshDataGrid failed: {ex}");
        }
        finally
        {
            _isRefreshingDataGrid = false;
        }
    }

    private static string? BuildSchemaKey(DataTable? table)
    {
        if (table == null)
            return null;

        return string.Join("|", table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
    }

    private void OnGridSelectionChanged(object sender, object e)
    {
        if (_isRefreshingDataGrid || _vm == null)
            return;

        var selectedRows = CsvDataGrid.SelectedItems?.OfType<DataRowView>().ToList() ?? new List<DataRowView>();
        _vm.SetSelectedMergeRows(selectedRows);

        if (selectedRows.Count > 0 && !ReferenceEquals(_vm.SelectedDataMergeRow, selectedRows[0]))
            _vm.SelectedDataMergeRow = selectedRows[0];
    }
}
