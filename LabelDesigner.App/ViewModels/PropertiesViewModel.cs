using System;
using System.Collections.Generic;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace LabelDesigner.App.ViewModels;


public partial class PropertiesViewModel : ObservableObject
{
    public ObservableCollection<PropertyItem> Properties { get; } = new();
}

public class PropertyItem
{
    public string Name
    {
        get; set;
    }
    public string Value
    {
        get; set;
    }
}