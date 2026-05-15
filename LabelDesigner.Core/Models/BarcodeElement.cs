using System;
using System.Collections.Generic;
using System.Text;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

public class BarcodeElement : DesignElement
{
    public string Value { get; set; } = "123456789";

    public BarcodeTextPosition TextPosition { get; set; } = BarcodeTextPosition.Bottom;

    public string DisplayText => Value;
}
