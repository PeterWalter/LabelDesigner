using LabelDesigner.Application.Services;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using System.Numerics;

namespace LabelDesigner.Tests;

public class ElementInteractionServiceTests
{
    [Fact]
    public void GetHoverHandle_returns_move_when_pointer_is_inside_bounds()
    {
        var service = new ElementInteractionService(new SnapService());
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 100, 40)
        };

        var handle = service.GetHoverHandle(selected, new PointD(30, 30), zoom: 1.0);

        Assert.Equal(ResizeHandle.Move, handle);
    }

    [Fact]
    public void UpdateDrag_move_path_snaps_and_clamps_to_page()
    {
        var service = new ElementInteractionService(new SnapService { GridSize = 20, Threshold = 5 });
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 50, 20)
        };

        service.BeginDrag(new PointD(10, 10), selected, ResizeHandle.Move);
        var update = service.UpdateDrag(
            new PointD(87, 89),
            selected,
            Array.Empty<RectD>(),
            new RectD(0, 0, 100, 100));

        Assert.NotNull(update.Bounds);
        Assert.Equal(50, update.Bounds!.Value.X);
        Assert.Equal(80, update.Bounds!.Value.Y);
    }

    [Fact]
    public void UpdateDrag_resize_keeps_minimum_size()
    {
        var service = new ElementInteractionService(new SnapService());
        var selected = new TextElement
        {
            Bounds = new RectD(20, 20, 40, 40)
        };

        service.BeginDrag(new PointD(20, 20), selected, ResizeHandle.TopLeft);
        var update = service.UpdateDrag(
            new PointD(60, 60),
            selected,
            Array.Empty<RectD>(),
            new RectD(0, 0, 500, 500));

        Assert.NotNull(update.Bounds);
        Assert.Equal(20, update.Bounds!.Value.Width);
        Assert.Equal(20, update.Bounds!.Value.Height);
    }

    [Fact]
    public void GetHoverHandle_returns_rotation_when_pointer_is_over_rotation_grip()
    {
        var service = new ElementInteractionService(new SnapService());
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 100, 40)
        };

        var handle = service.GetHoverHandle(selected, new PointD(60, -10), zoom: 1.0);

        Assert.Equal(ResizeHandle.Rotate, handle);
    }

    [Fact]
    public void UpdateDrag_resize_snaps_to_grid()
    {
        var service = new ElementInteractionService(new SnapService { GridSize = 20, Threshold = 5 });
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 40, 20)
        };

        service.BeginDrag(new PointD(50, 20), selected, ResizeHandle.Right);
        var update = service.UpdateDrag(
            new PointD(87, 20),
            selected,
            Array.Empty<RectD>(),
            new RectD(0, 0, 200, 200));

        Assert.NotNull(update.Bounds);
        Assert.Equal(70, update.Bounds!.Value.Width);
    }

    [Fact]
    public void UpdateDrag_rotate_turns_around_center()
    {
        var service = new ElementInteractionService(new SnapService());
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 100, 40)
        };

        service.BeginDrag(new PointD(60, 0), selected, ResizeHandle.Rotate);
        var update = service.UpdateDrag(
            new PointD(90, 30),
            selected,
            Array.Empty<RectD>(),
            new RectD(0, 0, 200, 200));

        Assert.True(update.Rotation.HasValue);
        Assert.Equal(90, update.Rotation.Value, precision: 0);
    }

    [Fact]
    public void GetLocalTransform_rotates_around_element_center()
    {
        var selected = new TextElement
        {
            Bounds = new RectD(10, 10, 100, 40),
            Rotation = 90
        };

        var transform = selected.GetLocalTransform();
        var point = Vector2.Transform(new Vector2(10, 10), transform);

        Assert.Equal(80, point.X, precision: 3);
        Assert.Equal(-20, point.Y, precision: 3);
    }
}
