using FluentAssertions;
using LabelDesigner.Core.Utilities;

namespace LabelDesigner.Tests;

public class DataColumnNameNormalizerTests
{
    [Fact]
    public void NormalizeUnique_collapses_invisible_and_whitespace_header_variants()
    {
        var result = DataColumnNameNormalizer.NormalizeUnique(new[] { "Item", " Item ", "I\u200Btem", "\uFEFFItem" });

        result.Should().ContainInOrder("Item", "Item_2", "Item_3", "Item_4");
    }

    [Fact]
    public void NormalizeUnique_keeps_stable_schema_for_repeated_rows()
    {
        var row1 = new[] { "Item", "Code" };
        var row2 = new[] { "Item", "Code" };
        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;

        foreach (var key in row1.Concat(row2))
        {
            var canonical = DataColumnNameNormalizer.Canonicalize(key, index++);
            if (seen.Add(canonical))
                distinct.Add(key);
        }

        var result = DataColumnNameNormalizer.NormalizeUnique(distinct);
        result.Should().Equal("Item", "Code");
    }
}
