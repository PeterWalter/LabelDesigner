using LabelDesigner.Core.Models;
using Windows.Graphics.Imaging;

namespace LabelDesigner.Infrastructure.Interfaces;

public interface IDocumentRasterizer
{
    Task<SoftwareBitmap> RenderDocumentToBitmapAsync(SceneDocument document, float dpi);
}
