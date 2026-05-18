using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LabelDesigner.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private bool _isDraggingSplitter;
    private bool _isLeftSplitter;
    private double _startPointerX;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        RootGrid.DataContext = ViewModel;

        ViewModel.Designer.Layers.Refresh();

        var appWindow = this.AppWindow;
        if (appWindow != null)
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
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

    private void OnSplitterPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSplitter = true;
        _isLeftSplitter = sender == SplitterLeft;
        _startPointerX = e.GetCurrentPoint(RootGrid).Position.X;
        (sender as UIElement)?.CapturePointer(e.Pointer);
    }

    private void OnSplitterMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        var currentX = e.GetCurrentPoint(RootGrid).Position.X;
        var delta = currentX - _startPointerX;

        if (_isLeftSplitter)
        {
            var newWidth = LayerColumn.Width.Value + delta;
            if (newWidth > 60 && newWidth < 500)
            {
                LayerColumn.Width = new GridLength(newWidth);
                _startPointerX = currentX;
            }
        }
        else
        {
            var newWidth = PropertiesColumn.Width.Value - delta;
            if (newWidth > 120 && newWidth < 500)
            {
                PropertiesColumn.Width = new GridLength(newWidth);
                _startPointerX = currentX;
            }
        }
    }

    private void OnSplitterReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSplitter = false;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
    }
}
