using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LabelDesigner.App.ViewModels;



public partial class MainViewModel : ObservableObject
{
    public DesignerViewModel Designer
    {
        get;
    }
    public RibbonViewModel Ribbon
    {
        get;
    }
    public PropertiesViewModel Properties
    {
        get;
    }

    public MainViewModel(
        DesignerViewModel designer,
        RibbonViewModel ribbon,
        PropertiesViewModel properties)
    {
        Designer = designer;
        Ribbon = ribbon;
        Properties = properties;
    }
}
