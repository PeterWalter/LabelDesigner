using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LabelDesigner.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        RootGrid.DataContext = ViewModel;

        // Initial layers panel refresh
        ViewModel.Designer.Layers.Refresh();
    }

    private void OnLayerItemClick(object sender, PointerRoutedEventArgs e)
    {
        var grid = sender as Grid;
        if (grid?.DataContext is ElementItemViewModel evm)
        {
            ViewModel.Designer.Layers.SelectElement(evm.ElementId);
            ViewModel.Designer.RequestRedraw?.Invoke();
        }
    }
}
