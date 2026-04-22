using System;
using System.Collections.Generic;
using System.Text;

namespace LabelDesigner.Core.ValueObjects;

public struct RectD
{
    public double X;
    public double Y;
    public double Width;
    public double Height;

    // ✅ ADD THIS
    public RectD(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    public RectD Translate(double dx, double dy)
    {
        return new RectD(X + dx, Y + dy, Width, Height);
    }
}
