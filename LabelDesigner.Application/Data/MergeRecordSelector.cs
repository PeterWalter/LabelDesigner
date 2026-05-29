using System.Data;

namespace LabelDesigner.Application.Data;

public static class MergeRecordSelector
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> SelectActiveRecords(
        DataTable? table,
        IReadOnlyCollection<DataRow> selectedRows,
        IReadOnlyList<IReadOnlyDictionary<string, string>> allRecords)
    {
        if (selectedRows.Count == 0)
            return allRecords;

        if (table == null)
            return Array.Empty<IReadOnlyDictionary<string, string>>();

        var columns = table.Columns.Cast<DataColumn>().ToArray();
        if (columns.Length == 0)
            return Array.Empty<IReadOnlyDictionary<string, string>>();

        var selectedSet = new HashSet<DataRow>(selectedRows);
        return table.Rows
            .Cast<DataRow>()
            .Where(row => row.RowState != DataRowState.Deleted && selectedSet.Contains(row))
            .Select(row => (IReadOnlyDictionary<string, string>)ToSafeDictionary(columns, row))
            .ToList();
    }

    private static Dictionary<string, string> ToSafeDictionary(IEnumerable<DataColumn> columns, DataRow row)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var suffixes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            var baseName = string.IsNullOrWhiteSpace(column.ColumnName) ? "Column" : column.ColumnName.Trim();
            if (!suffixes.TryGetValue(baseName, out var count))
                count = 0;
            count++;
            suffixes[baseName] = count;

            var key = count == 1 ? baseName : $"{baseName}_{count}";
            dict[key] = row[column.ColumnName]?.ToString() ?? string.Empty;
        }

        return dict;
    }
}
