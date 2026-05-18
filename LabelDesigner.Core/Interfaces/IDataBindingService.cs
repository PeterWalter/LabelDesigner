using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

public interface IDataBindingService
{
    SceneDocument ApplyRecord(SceneDocument document, IReadOnlyDictionary<string, string> record);
    string ResolveTemplate(string template, IReadOnlyDictionary<string, string> record);
}
