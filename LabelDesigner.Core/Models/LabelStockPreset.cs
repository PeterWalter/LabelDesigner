namespace LabelDesigner.Core.Models;

/// <summary>
/// Describes the physical layout of a label stock (sheet dimensions, label
/// size, row/column count, and gutter margins). Used by
/// <see cref="LabelDesigner.Core.Interfaces.ILabelStockPresetService"/>.
/// </summary>
public class LabelStockPreset
{
    /// <summary>Stable, unique identifier (e.g. "avery-5160").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable name shown in the preset picker.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Manufacturer name (e.g. "Avery", "Dymo", "Zebra").</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>Individual label width in millimetres.</summary>
    public double LabelWidthMm { get; init; }

    /// <summary>Individual label height in millimetres.</summary>
    public double LabelHeightMm { get; init; }

    /// <summary>Number of label rows on the sheet.</summary>
    public int Rows { get; init; } = 1;

    /// <summary>Number of label columns on the sheet.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>Top margin of the sheet in millimetres.</summary>
    public double TopMarginMm { get; init; }

    /// <summary>Left margin of the sheet in millimetres.</summary>
    public double LeftMarginMm { get; init; }

    /// <summary>Horizontal gap between columns in millimetres.</summary>
    public double HorizontalGapMm { get; init; }

    /// <summary>Vertical gap between rows in millimetres.</summary>
    public double VerticalGapMm { get; init; }

    /// <summary><c>true</c> for built-in presets; <c>false</c> for user-defined ones.</summary>
    public bool IsBuiltIn { get; init; } = true;
}
