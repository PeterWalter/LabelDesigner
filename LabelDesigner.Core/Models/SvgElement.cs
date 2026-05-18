using System.Numerics;
using System.Text.Json.Serialization;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Models;

/// <summary>
/// Represents an SVG vector graphic element.
/// SVGs are imported as rasterized bitmaps but preserve original source path
/// and can be re-imported at higher quality if needed.
/// </summary>
public class SvgElement : DesignElement
{
    /// <summary>
    /// Path to the SVG file on disk.
    /// </summary>
    public string SourcePath { get; set; } = "";

    /// <summary>
    /// How the SVG should stretch to fill the bounds.
    /// </summary>
    public string Stretch { get; set; } = "Uniform";

    /// <summary>
    /// Cached rendering of the SVG as a bitmap.
    /// Not serialized; regenerated on load.
    /// </summary>
    [JsonIgnore]
    public byte[]? CachedBitmap { get; set; }
}

