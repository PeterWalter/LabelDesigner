using FluentAssertions;
using LabelDesigner.Application.Data;
using System.Data;

namespace LabelDesigner.Tests;

public class MergeRecordSelectorTests
{
    [Fact]
    public void SelectActiveRecords_returns_all_records_when_no_selection()
    {
        var table = BuildTable();
        var allRecords = BuildAllRecords(table);

        var result = MergeRecordSelector.SelectActiveRecords(table, Array.Empty<DataRow>(), allRecords);

        result.Should().BeSameAs(allRecords);
    }

    [Fact]
    public void SelectActiveRecords_returns_selected_rows_in_table_order()
    {
        var table = BuildTable();
        var allRecords = BuildAllRecords(table);
        var selected = new[] { table.Rows[2], table.Rows[0] };

        var result = MergeRecordSelector.SelectActiveRecords(table, selected, allRecords);

        result.Should().HaveCount(2);
        result[0]["Name"].Should().Be("A");
        result[1]["Name"].Should().Be("C");
    }

    [Fact]
    public void SelectActiveRecords_ignores_rows_from_other_tables()
    {
        var table = BuildTable();
        var other = BuildTable();
        var allRecords = BuildAllRecords(table);

        var result = MergeRecordSelector.SelectActiveRecords(table, new[] { other.Rows[0] }, allRecords);

        result.Should().BeEmpty();
    }

    private static DataTable BuildTable()
    {
        var table = new DataTable("MergeData");
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Code", typeof(string));
        table.Rows.Add("A", "101");
        table.Rows.Add("B", "102");
        table.Rows.Add("C", "103");
        return table;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> BuildAllRecords(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row => (IReadOnlyDictionary<string, string>)table.Columns
                .Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => row[column.ColumnName]?.ToString() ?? string.Empty))
            .ToList();
    }
}
