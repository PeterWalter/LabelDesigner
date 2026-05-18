using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface ISvgService
{
    // Convert ShapeElement to SVG path string
    string ToSvgPath(ShapeElement shape);

    // Export entire document to standalone SVG
    string ExportToSvg(SceneDocument document);

    // Rasterize an SVG file to PNG bytes for Win2D rendering.
    byte[] RasterizeToPng(string svgFilePath, int pixelWidth, int pixelHeight);
}
