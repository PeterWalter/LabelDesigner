using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface ISvgService
{
    // SVG to renderable bitmap
    //CanvasBitmap LoadSvg(CanvasDevice device, string svgFilePath, float width, float height);
    //CanvasBitmap LoadSvgFromString(CanvasDevice device, string svgContent, float width, float height);

    // Convert ShapeElement to SVG path string
    string ToSvgPath(ShapeElement shape);

    // Export entire document to standalone SVG
    string ExportToSvg(SceneDocument document);
}
