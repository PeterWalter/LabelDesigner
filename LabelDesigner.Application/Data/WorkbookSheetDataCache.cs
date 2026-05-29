namespace LabelDesigner.Application.Data;

public sealed class WorkbookSheetDataCache
{
    private const string DefaultWorksheetKey = "__default__";
    private readonly Dictionary<string, DataMergeGridModel> _sheetModels = new(StringComparer.OrdinalIgnoreCase);

    public string? SourcePath { get; private set; }

    public void Clear()
    {
        SourcePath = null;
        _sheetModels.Clear();
    }

    public void Store(string sourcePath, string? worksheetName, DataMergeGridModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(model);

        EnsureSourcePath(sourcePath);
        _sheetModels[NormalizeWorksheetKey(worksheetName)] = model;
    }

    public bool TryGet(string sourcePath, string? worksheetName, out DataMergeGridModel? model)
    {
        model = null;
        if (!string.Equals(SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            return false;

        return _sheetModels.TryGetValue(NormalizeWorksheetKey(worksheetName), out model);
    }

    private void EnsureSourcePath(string sourcePath)
    {
        if (string.Equals(SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            return;

        SourcePath = sourcePath;
        _sheetModels.Clear();
    }

    private static string NormalizeWorksheetKey(string? worksheetName)
    {
        return string.IsNullOrWhiteSpace(worksheetName)
            ? DefaultWorksheetKey
            : worksheetName.Trim();
    }
}
