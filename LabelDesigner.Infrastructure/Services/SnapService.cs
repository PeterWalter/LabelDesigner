using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Infrastructure.Services;

public class SnapService
{
    public double GridSize { get; set; } = 10;
    public double Threshold { get; set; } = 5;

    public RectD Snap(
        RectD moving,
        IEnumerable<RectD> others,
        out List<GuideLine> guides)
    {
        guides = new List<GuideLine>();

        // =========================
        // 1. GRID SNAP
        // =========================
        moving = new RectD(
            Math.Round(moving.X / GridSize) * GridSize,
            Math.Round(moving.Y / GridSize) * GridSize,
            moving.Width,
            moving.Height);

        // =========================
        // 2. ALIGNMENT SNAP
        // =========================
        foreach (var o in others)
        {
            // LEFT EDGE
            if (Math.Abs(moving.X - o.X) < Threshold)
            {
                moving.X = o.X;

                guides.Add(new GuideLine
                {
                    X1 = o.X,
                    Y1 = 0,
                    X2 = o.X,
                    Y2 = 5000
                });
            }

            // RIGHT EDGE
            if (Math.Abs((moving.X + moving.Width) - (o.X + o.Width)) < Threshold)
            {
                moving.X = (o.X + o.Width) - moving.Width;

                guides.Add(new GuideLine
                {
                    X1 = o.X + o.Width,
                    Y1 = 0,
                    X2 = o.X + o.Width,
                    Y2 = 5000
                });
            }

            // CENTER X
            var mcx = moving.X + moving.Width / 2;
            var ocx = o.X + o.Width / 2;

            if (Math.Abs(mcx - ocx) < Threshold)
            {
                moving.X = ocx - moving.Width / 2;

                guides.Add(new GuideLine
                {
                    X1 = ocx,
                    Y1 = 0,
                    X2 = ocx,
                    Y2 = 5000
                });
            }

            // TOP EDGE
            if (Math.Abs(moving.Y - o.Y) < Threshold)
            {
                moving.Y = o.Y;

                guides.Add(new GuideLine
                {
                    X1 = 0,
                    Y1 = o.Y,
                    X2 = 5000,
                    Y2 = o.Y
                });
            }

            // BOTTOM EDGE
            if (Math.Abs((moving.Y + moving.Height) - (o.Y + o.Height)) < Threshold)
            {
                moving.Y = (o.Y + o.Height) - moving.Height;

                guides.Add(new GuideLine
                {
                    X1 = 0,
                    Y1 = o.Y + o.Height,
                    X2 = 5000,
                    Y2 = o.Y + o.Height
                });
            }

            // CENTER Y
            var mcy = moving.Y + moving.Height / 2;
            var ocy = o.Y + o.Height / 2;

            if (Math.Abs(mcy - ocy) < Threshold)
            {
                moving.Y = ocy - moving.Height / 2;

                guides.Add(new GuideLine
                {
                    X1 = 0,
                    Y1 = ocy,
                    X2 = 5000,
                    Y2 = ocy
                });
            }
        }

        return moving;
    }
}