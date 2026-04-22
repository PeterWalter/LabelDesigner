using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml.Linq;
using LabelDesigner.App.ViewModels;
using LabelDesigner.Infrastructure.Interfaces;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LabelDesigner.App;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel
    {
        get;
    }
    private readonly DesignerViewModel _designer;
    private readonly IRenderService _renderer;
   
    public MainWindow(MainViewModel vm, IRenderService renderer)
    {
        InitializeComponent();
        ViewModel = vm;
        RootGrid.DataContext = ViewModel;
        LeftRuler.IsVertical = true;

        //_designer = vm.Designer;
        //_renderer = renderer;
    }

    //private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    //{
    //  //  _renderer.Render(ds, elements, selected, guides);
    //}
}
