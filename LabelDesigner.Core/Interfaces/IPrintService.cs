using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface IPrintService
{
    Task PrintAsync(SceneDocument document, nint windowHandle);
    Task PrintAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null);
    Task ShowPrintPreviewAsync(SceneDocument document, nint windowHandle);
    Task ShowPrintPreviewAsync(IReadOnlyList<SceneDocument> documents, nint windowHandle, string? jobTitle = null);
}
