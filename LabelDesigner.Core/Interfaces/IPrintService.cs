using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface IPrintService
{
    /// <summary>
    /// Canvas-to-physical scale: how many screen pixels equal 1 mm.
    /// Must be set to <c>DpiService.PixelsPerMm</c> before printing so that
    /// element bounds (stored in screen pixels) are correctly converted to
    /// print/PDF coordinates. Defaults to the 96-DPI value (96 / 25.4).
    /// </summary>
    double PixelsPerMm { get; set; }

    Task PrintAsync(SceneDocument document, nint windowHandle);
    Task PrintAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null);
    Task ShowPrintPreviewAsync(SceneDocument document, nint windowHandle);
    Task ShowPrintPreviewAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null);
}
