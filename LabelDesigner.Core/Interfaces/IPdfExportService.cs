using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface IPdfExportService
{
    Task ExportAsync(SceneDocument document, string outputPath, PdfExportOptions options);
}

public class PdfExportOptions
{
    public bool VectorGraphics { get; set; } = true;
    public bool EmbedFonts { get; set; } = true;
    public float Dpi { get; set; } = 300;

    /// <summary>
    /// How many screen pixels equal 1 mm in the canvas coordinate system.
    /// Set to <c>DpiService.PixelsPerMm</c> so element bounds are correctly
    /// converted to PDF points. Defaults to the 96-DPI value.
    /// </summary>
    public double PixelsPerMm { get; set; } = 96.0 / 25.4;
}
