using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Data;
using System.ComponentModel;
using System.Linq;

namespace LabelDesigner.App.Views;

public sealed partial class DataMergePaneView : UserControl
{
    private DesignerViewModel? _vm;
    private bool _isRefreshingDataGrid;

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

        _isRefreshingDataGrid = true;
        if (_vm.SelectedDataMergeRow != null && !ReferenceEquals(CsvDataGrid.SelectedItem, _vm.SelectedDataMergeRow))
            CsvDataGrid.SelectedItem = _vm.SelectedDataMergeRow;
        _isRefreshingDataGrid = false;
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
