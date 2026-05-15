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

        // Set default window size (WinUI 3 Window doesn't support Width/Height in XAML)
        Microsoft.UI.Windowing.AppWindow appWindow = this.AppWindow;
        if (appWindow != null)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        }
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
