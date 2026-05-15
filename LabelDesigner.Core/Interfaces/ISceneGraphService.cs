using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Interfaces;

public interface ISceneGraphService
{
    SceneDocument CurrentDocument { get; }
    void Load(SceneDocument doc);
    void Clear();

    void AddElement(DesignElement element, Guid? parentLayerId = null);
    void RemoveElement(Guid id);
    void MoveElement(Guid id, Guid newParentLayerId);
    void ReorderElement(Guid id, int newZIndex);

    DesignElement? GetElement(Guid id);
    IReadOnlyList<DesignElement> GetLayerElements(Guid layerId);
    DesignElement? HitTest(PointD p);
    IReadOnlyList<DesignElement> HitTestAll(PointD p);
    IEnumerable<DesignElement> GetElementsInRect(RectD rect);

    IReadOnlyList<Guid> SelectedIds { get; }
    DesignElement? SingleSelected { get; }
    void Select(Guid id);
    void Deselect(Guid id);
    void ToggleSelect(Guid id);
    void SelectAll();
    void ClearSelection();

    void MoveSelected(double dx, double dy);
    void ResizeSelected(ResizeHandle handle, double dx, double dy);
    void RotateSelected(double angle);

    LayerNode AddLayer(string name);
    void RemoveLayer(Guid id);
    void ReorderLayer(Guid id, int newIndex);
}
