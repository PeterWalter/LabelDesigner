using LabelDesigner.Application.Commands;
using LabelDesigner.Application.Services;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class SceneGraphOrderingTests
{
    private static SceneGraphService CreateService()
    {
        var undo = new UndoRedoService();
        var svc = new SceneGraphService(undo);
        svc.Clear();
        return svc;
    }

    private static (SceneGraphService svc, TextElement a, TextElement b, TextElement c) BuildLayerABC()
    {
        var svc = CreateService();
        var layer = svc.AddLayer("Test");

        var a = new TextElement { Name = "A", Bounds = new RectD(0, 0, 10, 10) };
        var b = new TextElement { Name = "B", Bounds = new RectD(0, 0, 10, 10) };
        var c = new TextElement { Name = "C", Bounds = new RectD(0, 0, 10, 10) };

        svc.AddElement(a, layer.Id);
        svc.AddElement(b, layer.Id);
        svc.AddElement(c, layer.Id);

        return (svc, a, b, c);
    }

    private static IReadOnlyList<string> LayerOrder(SceneGraphService svc)
    {
        var layer = svc.CurrentDocument.Layers[0];
        return layer.ElementIds
            .Select(id => svc.GetElement(id)?.Name ?? id.ToString())
            .ToList();
    }

    [Fact]
    public void BringToFront_moves_element_to_last_position()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.BringToFront(b.Id);

        var order = LayerOrder(svc);
        Assert.Equal(new[] { "A", "C", "B" }, order);
    }

    [Fact]
    public void SendToBack_moves_element_to_first_position()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.SendToBack(b.Id);

        var order = LayerOrder(svc);
        Assert.Equal(new[] { "B", "A", "C" }, order);
    }

    [Fact]
    public void BringForward_moves_element_one_step_up()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.BringForward(a.Id);

        var order = LayerOrder(svc);
        Assert.Equal(new[] { "B", "A", "C" }, order);
    }

    [Fact]
    public void SendBackward_moves_element_one_step_down()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.SendBackward(c.Id);

        var order = LayerOrder(svc);
        Assert.Equal(new[] { "A", "C", "B" }, order);
    }

    [Fact]
    public void BringToFront_is_undoable()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.BringToFront(b.Id);
        Assert.Equal(new[] { "A", "C", "B" }, LayerOrder(svc));

        svc.UndoRedo.Undo();
        Assert.Equal(new[] { "A", "B", "C" }, LayerOrder(svc));
    }

    [Fact]
    public void SendToBack_is_undoable()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.SendToBack(b.Id);
        Assert.Equal(new[] { "B", "A", "C" }, LayerOrder(svc));

        svc.UndoRedo.Undo();
        Assert.Equal(new[] { "A", "B", "C" }, LayerOrder(svc));
    }

    [Fact]
    public void BringToFront_on_already_front_element_is_no_op()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.BringToFront(c.Id);

        Assert.Equal(new[] { "A", "B", "C" }, LayerOrder(svc));
    }

    [Fact]
    public void SendToBack_on_already_back_element_is_no_op()
    {
        var (svc, a, b, c) = BuildLayerABC();

        svc.SendToBack(a.Id);

        Assert.Equal(new[] { "A", "B", "C" }, LayerOrder(svc));
    }

    [Fact]
    public void DuplicateSelected_creates_offset_clone_and_selects_it()
    {
        var svc = CreateService();
        var layer = svc.AddLayer("Test");
        var el = new TextElement { Name = "Original", Bounds = new RectD(10, 20, 50, 30), Text = "Hello" };
        svc.AddElement(el, layer.Id);
        svc.Select(el.Id);

        var duplicates = svc.DuplicateSelected(offsetX: 5, offsetY: 5);

        Assert.Single(duplicates);
        var dup = Assert.IsType<TextElement>(duplicates[0]);
        Assert.Equal("Hello", dup.Text);
        Assert.Equal(15, dup.Bounds.X);
        Assert.Equal(25, dup.Bounds.Y);
        Assert.Equal(50, dup.Bounds.Width);
        Assert.Equal(30, dup.Bounds.Height);
        // New element should be selected
        Assert.Contains(dup.Id, svc.SelectedIds);
        Assert.DoesNotContain(el.Id, svc.SelectedIds);
    }

    [Fact]
    public void DuplicateSelected_is_undoable()
    {
        var svc = CreateService();
        var layer = svc.AddLayer("Test");
        var el = new TextElement { Name = "Original", Bounds = new RectD(0, 0, 10, 10) };
        svc.AddElement(el, layer.Id);
        svc.Select(el.Id);

        svc.DuplicateSelected();
        Assert.Equal(2, svc.CurrentDocument.AllElements.Count);

        svc.UndoRedo.Undo();
        Assert.Single(svc.CurrentDocument.AllElements);
    }
}
