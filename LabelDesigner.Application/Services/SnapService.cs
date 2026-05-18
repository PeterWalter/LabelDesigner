using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Application.Services;

public class SnapService : ISnapService
{
    public double GridSize { get; set; } = 20;
    public double Threshold { get; set; } = 5;

    public RectD Snap(
        RectD moving,
        IEnumerable<RectD> others,
        out IReadOnlyList<GuideLine> guides)
    {
        var resolvedGuides = new List<GuideLine>();
        moving = moving.SnapOriginToGrid(GridSize);

        foreach (var other in others)
        {
            if (Math.Abs(moving.Left - other.Left) < Threshold)
            {
                moving.X = other.Left;
                resolvedGuides.Add(new GuideLine { IsHorizontal = false, Position = other.Left });
            }

            if (Math.Abs(moving.Right - other.Right) < Threshold)
            {
                moving.X = other.Right - moving.Width;
                resolvedGuides.Add(new GuideLine { IsHorizontal = false, Position = other.Right });
            }

            if (Math.Abs(moving.CenterX - other.CenterX) < Threshold)
            {
                moving.X = other.CenterX - (moving.Width / 2);
                resolvedGuides.Add(new GuideLine { IsHorizontal = false, Position = other.CenterX });
            }

            if (Math.Abs(moving.Top - other.Top) < Threshold)
            {
                moving.Y = other.Top;
                resolvedGuides.Add(new GuideLine { IsHorizontal = true, Position = other.Top });
            }

            if (Math.Abs(moving.Bottom - other.Bottom) < Threshold)
            {
                moving.Y = other.Bottom - moving.Height;
                resolvedGuides.Add(new GuideLine { IsHorizontal = true, Position = other.Bottom });
            }

            if (Math.Abs(moving.CenterY - other.CenterY) < Threshold)
            {
                moving.Y = other.CenterY - (moving.Height / 2);
                resolvedGuides.Add(new GuideLine { IsHorizontal = true, Position = other.CenterY });
            }
        }

        guides = resolvedGuides;
        return moving;
    }
}
