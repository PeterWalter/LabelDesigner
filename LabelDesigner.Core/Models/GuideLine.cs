namespace LabelDesigner.Core.Models;

/// <summary>A guide line placed on the canvas. Position is in world pixels.</summary>
public class GuideLine
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>true = horizontal line (fixed Y), false = vertical line (fixed X)</summary>
    public bool IsHorizontal { get; set; }

    /// <summary>Position in world pixels (Y for horizontal guides, X for vertical guides)</summary>
    public double Position { get; set; }
}
