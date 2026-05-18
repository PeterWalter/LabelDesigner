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
                resolvedGuides.Add(new GuideLine { X1 = other.Left, Y1 = 0, X2 = other.Left, Y2 = 5000 });
            }

            if (Math.Abs(moving.Right - other.Right) < Threshold)
            {
                moving.X = other.Right - moving.Width;
                resolvedGuides.Add(new GuideLine { X1 = other.Right, Y1 = 0, X2 = other.Right, Y2 = 5000 });
            }

            if (Math.Abs(moving.CenterX - other.CenterX) < Threshold)
            {
                moving.X = other.CenterX - (moving.Width / 2);
                resolvedGuides.Add(new GuideLine { X1 = other.CenterX, Y1 = 0, X2 = other.CenterX, Y2 = 5000 });
            }

            if (Math.Abs(moving.Top - other.Top) < Threshold)
            {
                moving.Y = other.Top;
                resolvedGuides.Add(new GuideLine { X1 = 0, Y1 = other.Top, X2 = 5000, Y2 = other.Top });
            }

            if (Math.Abs(moving.Bottom - other.Bottom) < Threshold)
            {
                moving.Y = other.Bottom - moving.Height;
                resolvedGuides.Add(new GuideLine { X1 = 0, Y1 = other.Bottom, X2 = 5000, Y2 = other.Bottom });
            }

            if (Math.Abs(moving.CenterY - other.CenterY) < Threshold)
            {
                moving.Y = other.CenterY - (moving.Height / 2);
                resolvedGuides.Add(new GuideLine { X1 = 0, Y1 = other.CenterY, X2 = 5000, Y2 = other.CenterY });
            }
        }

        guides = resolvedGuides;
        return moving;
    }
}
