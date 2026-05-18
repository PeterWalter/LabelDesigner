using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class RectDTests
{
    [Fact]
    public void SnapOriginToGrid_rounds_X_and_Y_to_grid()
    {
        var rect = new RectD(13, 27, 50, 20);

        var snapped = rect.SnapOriginToGrid(10);

        Assert.Equal(10, snapped.X);
        Assert.Equal(30, snapped.Y);
    }

    [Fact]
    public void ClampToBounds_keeps_rect_inside_container()
    {
        var rect = new RectD(95, 70, 20, 40);
        var bounds = new RectD(0, 0, 100, 100);

        var clamped = rect.ClampToBounds(bounds);

        Assert.Equal(80, clamped.X);
        Assert.Equal(60, clamped.Y);
        Assert.Equal(20, clamped.Width);
        Assert.Equal(40, clamped.Height);
    }

    [Fact]
    public void ClampToBounds_reduces_size_when_rect_is_larger_than_bounds()
    {
        var rect = new RectD(-10, -10, 300, 200);
        var bounds = new RectD(0, 0, 120, 80);

        var clamped = rect.ClampToBounds(bounds);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(120, clamped.Width);
        Assert.Equal(80, clamped.Height);
    }
}
