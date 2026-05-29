using FluentAssertions;
using LabelDesigner.Application.Data;

namespace LabelDesigner.Tests;

public class DataMergeGridModelBuilderTests
{
    [Fact]
    public void Build_uses_safe_mapping_names_for_display_columns()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> records =
        [
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item"] = "Widget",
                ["Code"] = "101"
            }
        ];

        var model = DataMergeGridModelBuilder.Build(records);

        model.Columns.Select(column => column.MappingName).Should().Equal("Field_1", "Field_2");
        model.Columns.Select(column => column.HeaderText).Should().Equal("Item", "Code");

        var row = (IDictionary<string, object?>)model.Rows.Single();
        row[DataMergeGridModelBuilder.RecordIndexPropertyName].Should().Be(0);
        row["Field_1"].Should().Be("Widget");
        row["Field_2"].Should().Be("101");
    }

    [Fact]
    public void Build_normalizes_colliding_headers_without_reusing_raw_names_as_mapping_names()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> records =
        [
            new Dictionary<string, string>
            {
                ["Item"] = "Alpha",
                ["\uFEFFItem"] = "Beta",
                [" Code "] = "201"
            }
        ];

        var model = DataMergeGridModelBuilder.Build(records);

        model.Columns.Select(column => column.HeaderText).Should().Equal("Item", "Item_2", "Code");
        model.Columns.Select(column => column.MappingName).Should().Equal("Field_1", "Field_2", "Field_3");

        var row = (IDictionary<string, object?>)model.Rows.Single();
        row["Field_1"].Should().Be("Alpha");
        row["Field_2"].Should().Be("Beta");
        row["Field_3"].Should().Be("201");
    }
}
