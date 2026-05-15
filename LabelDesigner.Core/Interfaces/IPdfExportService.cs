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
}
