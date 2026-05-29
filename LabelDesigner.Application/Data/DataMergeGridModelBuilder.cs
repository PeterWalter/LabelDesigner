using LabelDesigner.Core.Utilities;
using System.Dynamic;

namespace LabelDesigner.Application.Data;

public sealed record DataMergeGridColumn(string MappingName, string HeaderText);

public sealed class DataMergeGridModel
{
    public DataMergeGridModel(
        IReadOnlyList<IReadOnlyDictionary<string, string>> records,
        IReadOnlyList<DataMergeGridColumn> columns,
        IReadOnlyList<ExpandoObject> rows)
    {
        Records = records;
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Records { get; }

    public IReadOnlyList<DataMergeGridColumn> Columns { get; }

    public IReadOnlyList<ExpandoObject> Rows { get; }
}

public static class DataMergeGridModelBuilder
{
    public const string RecordIndexPropertyName = "__RecordIndex";

    public static DataMergeGridModel Build(IReadOnlyList<IReadOnlyDictionary<string, string>> sourceRecords)
    {
        ArgumentNullException.ThrowIfNull(sourceRecords);

        var normalizedRecords = NormalizeRecords(sourceRecords);
        var headers = normalizedRecords
            .SelectMany(record => record.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var columns = headers
            .Select((header, index) => new DataMergeGridColumn($"Field_{index + 1}", header))
            .ToList();

        var rows = new List<ExpandoObject>(normalizedRecords.Count);
        for (var recordIndex = 0; recordIndex < normalizedRecords.Count; recordIndex++)
        {
            var record = normalizedRecords[recordIndex];
            IDictionary<string, object?> row = new ExpandoObject();
            row[RecordIndexPropertyName] = recordIndex;

            foreach (var column in columns)
                row[column.MappingName] = record.TryGetValue(column.HeaderText, out var value) ? value : string.Empty;

            rows.Add((ExpandoObject)row);
        }

        return new DataMergeGridModel(normalizedRecords, columns, rows);
    }

    public static bool TryGetRecordIndex(object? row, out int recordIndex)
    {
        recordIndex = -1;
        if (row is not IDictionary<string, object?> values
            || !values.TryGetValue(RecordIndexPropertyName, out var rawIndex)
            || rawIndex is null)
            return false;

        switch (rawIndex)
        {
            case int intIndex:
                recordIndex = intIndex;
                return true;
            case long longIndex when longIndex is >= int.MinValue and <= int.MaxValue:
                recordIndex = (int)longIndex;
                return true;
            case string text when int.TryParse(text, out var parsedIndex):
                recordIndex = parsedIndex;
                return true;
            default:
                return false;
        }
    }

    private static List<IReadOnlyDictionary<string, string>> NormalizeRecords(
        IReadOnlyList<IReadOnlyDictionary<string, string>> sourceRecords)
    {
        var normalizedRecords = new List<IReadOnlyDictionary<string, string>>(sourceRecords.Count);

        foreach (var sourceRecord in sourceRecords)
        {
            var rawKeys = sourceRecord.Keys.ToList();
            var normalizedKeys = DataColumnNameNormalizer.NormalizeUnique(rawKeys);
            var normalizedRecord = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < rawKeys.Count; index++)
            {
                var rawKey = rawKeys[index];
                normalizedRecord[normalizedKeys[index]] = sourceRecord.TryGetValue(rawKey, out var value) ? value : string.Empty;
            }

            normalizedRecords.Add(normalizedRecord);
        }

        return normalizedRecords;
    }
}
