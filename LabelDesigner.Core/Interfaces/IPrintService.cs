using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface IPrintService
{
    Task PrintAsync(SceneDocument document);
    Task ShowPrintPreviewAsync(SceneDocument document);
}
