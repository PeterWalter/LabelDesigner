namespace LabelDesigner.Core.Interfaces;

public interface IDataSourceService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadAsync(string path);
}
