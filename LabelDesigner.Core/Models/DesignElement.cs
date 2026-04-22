using System;
using System.Collections.Generic;
using System.Text;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public abstract class DesignElement
{
    public RectD Bounds
    {
        get; set;
    }
}
