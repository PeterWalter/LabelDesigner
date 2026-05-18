using LabelDesigner.Application.Commands;
using LabelDesigner.Application.Services;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class SceneGraphHitTestTests
{
    private static SceneGraphService CreateService()
    {
        var undo = new UndoRedoService();
        var svc = new SceneGraphService(undo);
        svc.Clear();
        return svc;
    }

    [Fact]
    public void HitTest_returns_topmost_element_from_layer_order()
    {
        var svc = CreateService();
        var layer = svc.AddLayer("Test");

        var back = new TextElement { Name = "Back", Bounds = new RectD(0, 0, 100, 100) };
        var front = new TextElement { Name = "Front", Bounds = new RectD(0, 0, 100, 100) };
        svc.AddElement(back, layer.Id);
        svc.AddElement(front, layer.Id);

        var hit = svc.HitTest(new PointD(10, 10));

        Assert.NotNull(hit);
        Assert.Equal("Front", hit!.Name);
    }

    [Fact]
    public void HitTest_uses_precise_line_geometry_not_only_bounds()
    {
        var svc = CreateService();
        var layer = svc.AddLayer("Test");
        var line = new LineElement
        {
            Name = "Line",
            X1 = 0,
            Y1 = 0,
            X2 = 100,
            Y2 = 0,
            Bounds = new RectD(0, 0, 100, 1)
        };
        svc.AddElement(line, layer.Id);

        var hitOnLine = svc.HitTest(new PointD(50, 1));
        var hitOffLine = svc.HitTest(new PointD(50, 20));

        Assert.NotNull(hitOnLine);
        Assert.Equal(line.Id, hitOnLine!.Id);
        Assert.Null(hitOffLine);
    }
}
