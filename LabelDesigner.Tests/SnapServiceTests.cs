using LabelDesigner.Application.Services;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class SnapServiceTests
{
    [Fact]
    public void Snap_applies_grid_when_no_other_elements_exist()
    {
        var snapService = new SnapService { GridSize = 20, Threshold = 5 };
        var moving = new RectD(13, 27, 40, 10);

        var snapped = snapService.Snap(moving, Array.Empty<RectD>(), out var guides);

        Assert.Equal(20, snapped.X);
        Assert.Equal(20, snapped.Y);
        Assert.Empty(guides);
    }

    [Fact]
    public void Snap_aligns_to_nearby_edges_and_returns_guides()
    {
        var snapService = new SnapService { GridSize = 1, Threshold = 5 };
        var moving = new RectD(97, 58, 40, 20);
        var other = new RectD(100, 10, 60, 30);

        var snapped = snapService.Snap(moving, new[] { other }, out var guides);

        Assert.Equal(100, snapped.X);
        Assert.True(guides.Count > 0);
    }
}
