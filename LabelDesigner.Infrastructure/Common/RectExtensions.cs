using System;
using System.Collections.Generic;
using System.Text;
using global::LabelDesigner.Core.ValueObjects;
using LabelDesigner.Core.ValueObjects;
using Windows.Foundation;

namespace LabelDesigner.Infrastructure.Common;

public static class RectExtensions
{
    public static Rect ToWinRect(this RectD r)
    {
        return new Rect(r.X, r.Y, r.Width, r.Height);
    }
}
