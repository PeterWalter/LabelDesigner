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
            .Select(row => (IReadOnlyDictionary<string, string>)columns.ToDictionary(
                column => column.ColumnName,
                column => row[column.ColumnName]?.ToString() ?? string.Empty))
            .ToList();
    }
}
