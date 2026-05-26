using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace LabelDesigner.App.Views;

public sealed partial class DataMergePaneView : UserControl
{
    private DesignerViewModel? _vm;

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
        if (e.PropertyName == nameof(DesignerViewModel.DataMergeView))
            DispatcherQueue.TryEnqueue(RefreshDataGrid);
    }

    private void RefreshDataGrid()
    {
        try
        {
            if (_vm == null) return;
            // Force SfDataGrid to rebuild columns by resetting ItemsSource
            CsvDataGrid.ItemsSource = null;
            CsvDataGrid.ItemsSource = _vm.DataMergeView;
        }
        catch
        {
            // SfDataGrid can throw during column regeneration; suppress to prevent app crash
        }
    }
}
