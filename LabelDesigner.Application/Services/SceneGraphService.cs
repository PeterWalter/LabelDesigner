using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Application.Services;

public class SceneGraphService : ISceneGraphService
{
    private readonly IUndoRedoService _undoRedo;
    internal readonly Dictionary<Guid, DesignElement> _elements = new();
    internal readonly Dictionary<Guid, LayerNode> _layers = new();
    private readonly HashSet<Guid> _selectedIds = new();

    public SceneDocument CurrentDocument { get; private set; } = new();

    public IUndoRedoService UndoRedo => _undoRedo;

    public IReadOnlyList<Guid> SelectedIds => _selectedIds.ToList().AsReadOnly();

    public DesignElement? SingleSelected =>
        _selectedIds.Count == 1 ? GetElement(_selectedIds.First()) : null;

    public SceneGraphService(IUndoRedoService undoRedo)
    {
        _undoRedo = undoRedo;
    }

    public void Load(SceneDocument doc)
    {
        CurrentDocument = doc;
        _elements.Clear();
        _layers.Clear();
        _selectedIds.Clear();

        foreach (var el in doc.AllElements)
            _elements[el.Id] = el;

        foreach (var layer in doc.Layers)
            _layers[layer.Id] = layer;
    }

    public void Clear()
    {
        CurrentDocument = new SceneDocument();
        _elements.Clear();
        _layers.Clear();
        _selectedIds.Clear();
        _undoRedo.Clear();
    }

    public void AddElement(DesignElement element, Guid? parentLayerId = null)
    {
        var layerId = parentLayerId ?? GetDefaultLayer().Id;
        element.ParentId = layerId;
        _undoRedo.Execute(new AddElementCommand(this, element, layerId));
    }

    public void RemoveElement(Guid id)
    {
        if (!_elements.TryGetValue(id, out var element)) return;
        _undoRedo.Execute(new RemoveElementCommand(this, element, element.ParentId));
    }

    public void MoveElement(Guid id, Guid newParentLayerId)
    {
        if (!_elements.TryGetValue(id, out var element)) return;
        var oldParent = element.ParentId;

        _undoRedo.Execute(new MoveElementCommand(this, id, oldParent!, newParentLayerId));

        if (oldParent.HasValue && _layers.TryGetValue(oldParent.Value, out var oldLayer))
            oldLayer.ElementIds.Remove(id);

        element.ParentId = newParentLayerId;

        if (_layers.TryGetValue(newParentLayerId, out var newLayer))
            newLayer.ElementIds.Add(id);
    }

    public void ReorderElement(Guid id, int newZIndex)
    {
        if (!_elements.TryGetValue(id, out var element)) return;
        var oldZIndex = element.ZIndex;

        _undoRedo.Execute(new ReorderElementCommand(this, id, oldZIndex, newZIndex));
        element.ZIndex = newZIndex;
    }

    public DesignElement? GetElement(Guid id) =>
        _elements.TryGetValue(id, out var el) ? el : null;

    public IReadOnlyList<DesignElement> GetLayerElements(Guid layerId)
    {
        if (!_layers.TryGetValue(layerId, out var layer))
            return Array.Empty<DesignElement>();

        return layer.ElementIds
            .Select(id => _elements.GetValueOrDefault(id))
            .Where(el => el != null)
            .Cast<DesignElement>()
            .ToList()
            .AsReadOnly();
    }

    public DesignElement? HitTest(PointD p)
    {
        for (int i = CurrentDocument.Layers.Count - 1; i >= 0; i--)
        {
            var layer = CurrentDocument.Layers[i];
            if (!layer.Visible || layer.Locked) continue;

            foreach (var id in layer.ElementIds.OrderByDescending(id =>
                _elements.TryGetValue(id, out var el) ? el.ZIndex : 0))
            {
                if (_elements.TryGetValue(id, out var el) && el.Visible && !el.Locked)
                {
                    var b = el.Bounds;
                    if (p.X >= b.X && p.X <= b.X + b.Width && p.Y >= b.Y && p.Y <= b.Y + b.Height)
                        return el;
                }
            }
        }
        return null;
    }

    public IReadOnlyList<DesignElement> HitTestAll(PointD p)
    {
        var hits = new List<DesignElement>();
        for (int i = CurrentDocument.Layers.Count - 1; i >= 0; i--)
        {
            var layer = CurrentDocument.Layers[i];
            if (!layer.Visible) continue;

            foreach (var id in layer.ElementIds.OrderByDescending(id =>
                _elements.TryGetValue(id, out var el) ? el.ZIndex : 0))
            {
                if (_elements.TryGetValue(id, out var el) && el.Visible)
                {
                    var b = el.Bounds;
                    if (p.X >= b.X && p.X <= b.X + b.Width && p.Y >= b.Y && p.Y <= b.Y + b.Height)
                        hits.Add(el);
                }
            }
        }
        return hits;
    }

    public IEnumerable<DesignElement> GetElementsInRect(RectD rect)
    {
        return _elements.Values.Where(el =>
            el.Visible &&
            el.Bounds.X + el.Bounds.Width >= rect.X &&
            el.Bounds.X <= rect.X + rect.Width &&
            el.Bounds.Y + el.Bounds.Height >= rect.Y &&
            el.Bounds.Y <= rect.Y + rect.Height);
    }

    public void Select(Guid id) => _selectedIds.Add(id);
    public void Deselect(Guid id) => _selectedIds.Remove(id);
    public void ToggleSelect(Guid id)
    {
        if (!_selectedIds.Remove(id)) _selectedIds.Add(id);
    }
    public void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var id in _elements.Keys) _selectedIds.Add(id);
    }
    public void ClearSelection() => _selectedIds.Clear();

    public void MoveSelected(double dx, double dy)
    {
        foreach (var id in _selectedIds)
        {
            if (_elements.TryGetValue(id, out var el) && !el.Locked)
            {
                _undoRedo.Execute(new MoveElementCommand(this, id,
                    new RectD(el.Bounds.X, el.Bounds.Y, el.Bounds.Width, el.Bounds.Height),
                    new RectD(el.Bounds.X + dx, el.Bounds.Y + dy, el.Bounds.Width, el.Bounds.Height)));
                el.Bounds = new RectD(el.Bounds.X + dx, el.Bounds.Y + dy, el.Bounds.Width, el.Bounds.Height);
            }
        }
    }

    public void ResizeSelected(ResizeHandle handle, double dx, double dy) { /* Phase 2 */ }

    public void RotateSelected(double angle)
    {
        foreach (var id in _selectedIds)
        {
            if (_elements.TryGetValue(id, out var el) && !el.Locked)
            {
                _undoRedo.Execute(new RotateElementCommand(this, id, el.Rotation, el.Rotation + angle));
                el.Rotation += angle;
            }
        }
    }

    public LayerNode AddLayer(string name)
    {
        var layer = new LayerNode { Name = name };
        _layers[layer.Id] = layer;
        CurrentDocument.Layers.Add(layer);
        return layer;
    }

    public void RemoveLayer(Guid id)
    {
        if (!_layers.TryGetValue(id, out var layer)) return;
        _layers.Remove(id);
        CurrentDocument.Layers.Remove(layer);
    }

    public void ReorderLayer(Guid id, int newIndex)
    {
        if (!_layers.TryGetValue(id, out var layer)) return;
        CurrentDocument.Layers.Remove(layer);
        CurrentDocument.Layers.Insert(Math.Clamp(newIndex, 0, CurrentDocument.Layers.Count), layer);
    }

    private LayerNode GetDefaultLayer()
    {
        if (CurrentDocument.Layers.Count == 0)
            AddLayer("Default");
        return CurrentDocument.Layers[0];
    }

    private record AddElementCommand(SceneGraphService Owner, DesignElement Element, Guid LayerId) : IUndoableCommand
    {
        public string Description => $"Add {Element.GetType().Name} '{Element.Name}'";
        public void Execute()
        {
            Owner._elements[Element.Id] = Element;
            if (Owner._layers.TryGetValue(LayerId, out var l)) l.ElementIds.Add(Element.Id);
            Owner.CurrentDocument.AllElements.Add(Element);
        }
        public void Undo()
        {
            Owner._elements.Remove(Element.Id);
            if (Owner._layers.TryGetValue(LayerId, out var l)) l.ElementIds.Remove(Element.Id);
            Owner.CurrentDocument.AllElements.Remove(Element);
        }
    }

    private record RemoveElementCommand(SceneGraphService Owner, DesignElement Element, Guid? LayerId) : IUndoableCommand
    {
        public string Description => $"Remove {Element.GetType().Name} '{Element.Name}'";
        public void Execute()
        {
            Owner._elements.Remove(Element.Id);
            if (LayerId.HasValue && Owner._layers.TryGetValue(LayerId.Value, out var l)) l.ElementIds.Remove(Element.Id);
            Owner.CurrentDocument.AllElements.Remove(Element);
        }
        public void Undo()
        {
            Owner._elements[Element.Id] = Element;
            if (LayerId.HasValue && Owner._layers.TryGetValue(LayerId.Value, out var l)) l.ElementIds.Add(Element.Id);
            Owner.CurrentDocument.AllElements.Add(Element);
        }
    }

    private record MoveElementCommand(SceneGraphService Owner, Guid Id, object From, object To) : IUndoableCommand
    {
        public string Description => "Move element";
        public void Execute()
        {
            if (!Owner._elements.TryGetValue(Id, out var el)) return;
            if (To is RectD r) el.Bounds = r;
            else if (To is Guid parentId)
            {
                if (From is Guid oldParentId && Owner._layers.TryGetValue(oldParentId, out var oldLayer))
                    oldLayer.ElementIds.Remove(Id);
                el.ParentId = parentId;
                if (Owner._layers.TryGetValue(parentId, out var newLayer))
                    newLayer.ElementIds.Add(Id);
            }
        }
        public void Undo()
        {
            if (!Owner._elements.TryGetValue(Id, out var el)) return;
            if (From is RectD r) el.Bounds = r;
            else if (From is Guid oldParentId)
            {
                if (To is Guid newParentId && Owner._layers.TryGetValue(newParentId, out var newLayer))
                    newLayer.ElementIds.Remove(Id);
                el.ParentId = oldParentId;
                if (Owner._layers.TryGetValue(oldParentId, out var oldLayer))
                    oldLayer.ElementIds.Add(Id);
            }
        }
    }

    private record ReorderElementCommand(SceneGraphService Owner, Guid Id, int OldZ, int NewZ) : IUndoableCommand
    {
        public string Description => "Reorder element";
        public void Execute() { if (Owner._elements.TryGetValue(Id, out var el)) el.ZIndex = NewZ; }
        public void Undo() { if (Owner._elements.TryGetValue(Id, out var el)) el.ZIndex = OldZ; }
    }

    private record RotateElementCommand(SceneGraphService Owner, Guid Id, double OldAngle, double NewAngle) : IUndoableCommand
    {
        public string Description => "Rotate element";
        public void Execute() { if (Owner._elements.TryGetValue(Id, out var el)) el.Rotation = NewAngle; }
        public void Undo() { if (Owner._elements.TryGetValue(Id, out var el)) el.Rotation = OldAngle; }
    }
}

file static class DictionaryExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull
    {
        return dict.TryGetValue(key, out var value) ? value : default;
    }
}
