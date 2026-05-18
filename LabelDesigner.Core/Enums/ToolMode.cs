namespace LabelDesigner.Core.Enums;

/// <summary>
/// Represents the active tool mode in the Label editor.
/// Only one tool can be active at a time (mutually exclusive).
/// </summary>
public enum ToolMode
{
    /// <summary>Selection and editing mode. Click to select, drag to move, use handles to resize/rotate.</summary>
    Select = 0,

    /// <summary>Barcode placement mode. Click/drag to place a barcode element.</summary>
    PlaceBarcode = 1,

    /// <summary>Text placement mode. Click/drag to place a text element.</summary>
    PlaceText = 2,

    /// <summary>Shape placement mode. Click/drag to place a shape element.</summary>
    PlaceShape = 3,

    /// <summary>Line placement mode. Two-click sequence to draw a line.</summary>
    PlaceLine = 4,

    /// <summary>Image placement mode. Click/drag to place an image element.</summary>
    PlaceImage = 5,

    /// <summary>SVG placement mode. Click/drag to place an SVG element.</summary>
    PlaceSvg = 6
}
