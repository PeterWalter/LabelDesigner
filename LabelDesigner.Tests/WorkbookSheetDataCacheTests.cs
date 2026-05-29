using FluentAssertions;
using LabelDesigner.Application.Data;
using System.Dynamic;

namespace LabelDesigner.Tests;

public class WorkbookSheetDataCacheTests
{
    [Fact]
    public void Store_and_try_get_are_scoped_to_the_current_workbook_path()
    {
        var cache = new WorkbookSheetDataCache();
        var workbookOneModel = CreateModel("Item", "Alpha");
        var workbookTwoModel = CreateModel("Item", "Beta");

        cache.Store(@"C:\data\one.xlsx", "Sheet1", workbookOneModel);
        cache.Store(@"C:\data\two.xlsx", "Sheet1", workbookTwoModel);

        cache.TryGet(@"C:\data\one.xlsx", "Sheet1", out var workbookOneResult).Should().BeFalse();
        cache.TryGet(@"C:\data\two.xlsx", "Sheet1", out var workbookTwoResult).Should().BeTrue();
        workbookTwoResult.Should().BeSameAs(workbookTwoModel);
    }

    [Fact]
    public void Try_get_distinguishes_between_worksheet_names_for_the_same_workbook()
    {
        var cache = new WorkbookSheetDataCache();
        var sheetOneModel = CreateModel("Item", "Alpha");
        var sheetTwoModel = CreateModel("Item", "Beta");

        cache.Store(@"C:\data\book.xlsx", "Sheet1", sheetOneModel);
        cache.Store(@"C:\data\book.xlsx", "Sheet2", sheetTwoModel);

        cache.TryGet(@"C:\data\book.xlsx", "Sheet1", out var sheetOneResult).Should().BeTrue();
        cache.TryGet(@"C:\data\book.xlsx", "Sheet2", out var sheetTwoResult).Should().BeTrue();
        sheetOneResult.Should().BeSameAs(sheetOneModel);
        sheetTwoResult.Should().BeSameAs(sheetTwoModel);
    }

    [Fact]
    public void Try_get_uses_default_key_for_non_worksheet_sources()
    {
        var cache = new WorkbookSheetDataCache();
        var model = CreateModel("Item", "Alpha");

        cache.Store(@"C:\data\records.csv", null, model);

        cache.TryGet(@"C:\data\records.csv", null, out var result).Should().BeTrue();
        result.Should().BeSameAs(model);
    }

    private static DataMergeGridModel CreateModel(string header, string value)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> records =
        [
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [header] = value
            }
        ];

        return DataMergeGridModelBuilder.Build(records);
    }
}
