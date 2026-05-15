using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface ILabelPersistenceService
{
    Task<SceneDocument> LoadAsync(string filePath);
    Task SaveAsync(SceneDocument document, string filePath);
    Task<SceneDocument> LoadFromJsonAsync(string json);
    Task<string> SaveToJsonAsync(SceneDocument document);
}
