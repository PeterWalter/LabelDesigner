using System;
using System.Collections.Generic;
using System.Text;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;


public class TextElement : DesignElement
{
    public string Text { get; set; } = "Sample Text";
}