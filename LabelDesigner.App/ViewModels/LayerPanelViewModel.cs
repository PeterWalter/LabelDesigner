using CommunityToolkit.Mvvm.ComponentModel;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using System.Collections.ObjectModel;

namespace LabelDesigner.App.ViewModels;

public partial class LayerPanelViewModel : ObservableObject
{
    private readonly ISceneGraphService _scene;

    public ObservableCollection<LayerItemViewModel> Layers { get; } = new();

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;

    public LayerPanelViewModel(ISceneGraphService scene)
    {
        _scene = scene;
    }

    public void Refresh()
    {
        Layers.Clear();
        foreach (var layer in _scene.CurrentDocument.Layers)
        {
            var children = new List<ElementItemViewModel>();
            foreach (var id in layer.ElementIds)
            {
                var el = _scene.GetElement(id);
                if (el != null)
                    children.Add(new ElementItemViewModel(el));
            }
            var vm = new LayerItemViewModel(_scene, layer, children);
            Layers.Add(vm);
        }
    }

    public void SelectElement(Guid? elementId)
    {
        _scene.ClearSelection();
        if (elementId.HasValue)
        {
            _scene.Select(elementId.Value);
            Refresh();
        }
    }
}

public partial class LayerItemViewModel : ObservableObject
{
    private readonly LayerNode _layer;

    [ObservableProperty]
    private bool _isExpanded = true;

    public Guid LayerId => _layer.Id;
    public string Name => _layer.Name;
    public string VisibilityIcon => _layer.Visible ? "👁" : "";
    public bool IsLocked => _layer.Locked;
    public ObservableCollection<ElementItemViewModel> Children { get; }

    public LayerItemViewModel(ISceneGraphService scene, LayerNode layer, List<ElementItemViewModel> children)
    {
        _layer = layer;
        Children = new ObservableCollection<ElementItemViewModel>(children);
    }
}

public partial class ElementItemViewModel : ObservableObject
{
    private readonly DesignElement _element;

    [ObservableProperty]
    private bool _isSelected;

    public Guid Id => _element.Id;
    public Guid ElementId => _element.Id;
    public string Name => string.IsNullOrEmpty(_element.Name) ? _element.GetType().Name.Replace("Element", "") : _element.Name;
    public string TypeIcon => _element is BarcodeElement ? "▌▌" :
        _element is TextElement ? "T" :
        _element is ShapeElement ? "◻" :
        _element is LineElement ? "╱" : "•";

    public ElementItemViewModel(DesignElement element)
    {
        _element = element;
    }
}
