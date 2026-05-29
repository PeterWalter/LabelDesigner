using FluentAssertions;
using LabelDesigner.Application.Data;

namespace LabelDesigner.Tests;

public class MergeRecordSelectorTests
{
    [Fact]
    public void SelectActiveRecords_returns_all_records_when_no_selection()
    {
        var allRecords = BuildAllRecords();

        var result = MergeRecordSelector.SelectActiveRecords(Array.Empty<int>(), allRecords);

        result.Should().BeSameAs(allRecords);
    }

    [Fact]
    public void SelectActiveRecords_returns_selected_rows_in_table_order()
    {
        var allRecords = BuildAllRecords();

        var result = MergeRecordSelector.SelectActiveRecords(new[] { 2, 0 }, allRecords);

        result.Should().HaveCount(2);
        result[0]["Item"].Should().Be("A");
        result[1]["Item"].Should().Be("C");
    }

    [Fact]
    public void SelectActiveRecords_ignores_invalid_indices()
    {
        var allRecords = BuildAllRecords();

        var result = MergeRecordSelector.SelectActiveRecords(new[] { -1, 1, 8 }, allRecords);

        result.Should().ContainSingle();
        result[0]["Item"].Should().Be("B");
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> BuildAllRecords()
    {
        return
        [
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item"] = "A",
                ["Code"] = "101"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item"] = "B",
                ["Code"] = "102"
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item"] = "C",
                ["Code"] = "103"
            }
        ];
    }
}
