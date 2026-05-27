namespace LabelDesigner.Core.Models;

/// <summary>A guide line placed on the canvas. Position is stored in mm for DPI-invariance.</summary>
public class GuideLine
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>true = horizontal line (fixed Y), false = vertical line (fixed X)</summary>
    public bool IsHorizontal { get; set; }

    /// <summary>Position in millimeters (Y for horizontal guides, X for vertical guides)</summary>
    public double PositionMm { get; set; }
}
