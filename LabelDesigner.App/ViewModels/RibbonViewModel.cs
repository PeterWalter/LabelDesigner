using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace LabelDesigner.App.ViewModels;


public partial class RibbonViewModel : ObservableObject
{
    public ObservableCollection<string> Tabs
    {
        get;
    } =
        new() { "Home", "Insert", "View" };
}
