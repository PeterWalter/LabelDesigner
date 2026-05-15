using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;

namespace LabelDesigner.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        RootGrid.DataContext = ViewModel;
    }
}
