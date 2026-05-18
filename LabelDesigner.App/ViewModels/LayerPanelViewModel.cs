using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using System.Collections.ObjectModel;

namespace LabelDesigner.App.ViewModels;

public partial class LayerPanelViewModel : ObservableObject
{
    private readonly ISceneGraphService _scene;

    public ObservableCollection<LayerItemViewModel> Layers { get; } = new();

    [ObservableProperty]
    public partial LayerItemViewModel? SelectedLayer { get; set; }

    public Action? RequestRedraw { get; set; }

    public LayerPanelViewModel(ISceneGraphService scene)
    {
        _scene = scene;
    }

    public void Refresh(Guid? selectedElementId = null)
    {
        var previouslySelectedLayerId = SelectedLayer?.LayerId;
        Layers.Clear();
        foreach (var layer in _scene.CurrentDocument.Layers)
        {
            var children = new List<ElementItemViewModel>();
            foreach (var id in layer.ElementIds)
            {
                var el = _scene.GetElement(id);
                if (el != null)
                    children.Add(new ElementItemViewModel(el, isSelected: id == selectedElementId));
            }
            var vm = new LayerItemViewModel(this, layer, children);
            Layers.Add(vm);
        }

        SelectedLayer = Layers.FirstOrDefault(l => l.LayerId == previouslySelectedLayerId)
            ?? Layers.FirstOrDefault();
    }

    public void SelectElement(Guid? elementId)
    {
        _scene.ClearSelection();
        if (elementId.HasValue)
        {
            _scene.Select(elementId.Value);
            Refresh(elementId);
        }
    }

    [RelayCommand]
    public void AddLayer()
    {
        int layerNum = _scene.CurrentDocument.Layers.Count + 1;
        _scene.AddLayer($"Layer {layerNum}");
        Refresh();
        SelectedLayer = Layers.LastOrDefault();
    }

    [RelayCommand]
    public void DeleteSelectedLayer()
    {
        if (SelectedLayer == null) return;
        // Do not delete the last layer
        if (_scene.CurrentDocument.Layers.Count <= 1) return;
        _scene.RemoveLayer(SelectedLayer.LayerId);
        Refresh();
    }

    internal void OnToggleVisibility(LayerItemViewModel layer)
    {
        layer.LayerNode.Visible = !layer.LayerNode.Visible;
        layer.RefreshVisibility();
        RequestRedraw?.Invoke();
    }

    internal void OnToggleLock(LayerItemViewModel layer)
    {
        layer.LayerNode.Locked = !layer.LayerNode.Locked;
        layer.RefreshLock();
        RequestRedraw?.Invoke();
    }
}

public partial class LayerItemViewModel : ObservableObject
{
    private readonly LayerPanelViewModel _panel;
    internal readonly LayerNode LayerNode;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Guid LayerId => LayerNode.Id;
    public string VisibilityGlyph => IsVisible ? "\uE7B3" : "\uED1A";  // Eye / EyeHide
    public string LockGlyph => IsLocked ? "\uE72E" : "\uE785";         // Lock / Unlock
    public string ElementCountText => Children.Count == 1 ? "1 element" : $"{Children.Count} elements";

    public ObservableCollection<ElementItemViewModel> Children { get; }

    public LayerItemViewModel(LayerPanelViewModel panel, LayerNode layer, List<ElementItemViewModel> children)
    {
        _panel = panel;
        LayerNode = layer;
        Name = layer.Name;
        IsVisible = layer.Visible;
        IsLocked = layer.Locked;
        Children = new ObservableCollection<ElementItemViewModel>(children);
    }

    partial void OnNameChanged(string value)
    {
        LayerNode.Name = value;
    }

    public void RefreshVisibility() => IsVisible = LayerNode.Visible;
    public void RefreshLock() => IsLocked = LayerNode.Locked;

    [RelayCommand]
    private void ToggleVisibility() => _panel.OnToggleVisibility(this);

    [RelayCommand]
    private void ToggleLock() => _panel.OnToggleLock(this);
}

public partial class ElementItemViewModel : ObservableObject
{
    private readonly DesignElement _element;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Guid Id => _element.Id;
    public Guid ElementId => _element.Id;

    public string Name => string.IsNullOrEmpty(_element.Name)
        ? _element.GetType().Name.Replace("Element", "")
        : _element.Name;

    public string TypeGlyph => _element is BarcodeElement ? "\uE72E" :
        _element is TextElement ? "\uE8D2" :
        _element is ShapeElement ? "\uE7B4" :
        _element is LineElement ? "\uE750" :
        _element is ImageElement ? "\uEB9F" : "\uE8A5";

    public string TypeLabel => _element is BarcodeElement ? "Barcode" :
        _element is TextElement ? "Text" :
        _element is ShapeElement ? "Shape" :
        _element is LineElement ? "Line" :
        _element is ImageElement ? "Image" : "Element";

    public ElementItemViewModel(DesignElement element, bool isSelected = false)
    {
        _element = element;
        IsSelected = isSelected;
    }
}
