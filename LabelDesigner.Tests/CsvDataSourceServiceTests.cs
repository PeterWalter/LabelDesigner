using FluentAssertions;
using LabelDesigner.Infrastructure.Data;
using Syncfusion.XlsIO;
using System.Text;

namespace LabelDesigner.Tests;

public class CsvDataSourceServiceTests
{
    [Fact]
    public async Task LoadAsync_reads_excel_records_from_first_worksheet()
    {
        var service = new CsvDataSourceService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using var excelEngine = new ExcelEngine();
            var app = excelEngine.Excel;
            app.DefaultVersion = ExcelVersion.Xlsx;
            var workbook = app.Workbooks.Create(1);
            try
            {
                var sheet = workbook.Worksheets[0];
                sheet["A1"].Text = "Name";
                sheet["B1"].Text = "Code";
                sheet["A2"].Text = "Alpha";
                sheet["B2"].Number = 12345;
                sheet["A3"].Text = string.Empty;
                sheet["B3"].Text = string.Empty;
                workbook.SaveAs(path);
            }
            finally
            {
                workbook.Close();
            }

            var records = await service.LoadAsync(path);
            var alphaRecord = records.Single(record =>
                string.Equals(record["Name"], "Alpha", StringComparison.Ordinal));
            alphaRecord["Code"].Should().Be("12345");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_reads_records_from_selected_excel_worksheet()
    {
        var service = new CsvDataSourceService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using var excelEngine = new ExcelEngine();
            var app = excelEngine.Excel;
            app.DefaultVersion = ExcelVersion.Xlsx;
            var workbook = app.Workbooks.Create(2);
            try
            {
                var sheet1 = workbook.Worksheets[0];
                sheet1.Name = "First";
                sheet1["A1"].Text = "Name";
                sheet1["A2"].Text = "FromFirst";

                var sheet2 = workbook.Worksheets[1];
                sheet2.Name = "Second";
                sheet2["A1"].Text = "Name";
                sheet2["A2"].Text = "FromSecond";

                workbook.SaveAs(path);
            }
            finally
            {
                workbook.Close();
            }

            var names = await service.GetWorksheetNamesAsync(path);
            names.Should().Contain(new[] { "First", "Second" });

            var records = await service.LoadAsync(path, "Second");
            var target = records.Single(record =>
                string.Equals(record["Name"], "FromSecond", StringComparison.Ordinal));
            target["Name"].Should().Be("FromSecond");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_reads_json_array_records()
    {
        var service = new CsvDataSourceService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        try
        {
            const string json = """
            [
              { "Name": "Alice", "Qty": 3 },
              { "Name": "Bob", "Qty": 5 }
            ]
            """;
            await File.WriteAllTextAsync(path, json, Encoding.UTF8);

            var records = await service.LoadAsync(path);

            records.Should().HaveCount(2);
            records[0]["Name"].Should().Be("Alice");
            records[0]["Qty"].Should().Be("3");
            records[1]["Name"].Should().Be("Bob");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_normalizes_duplicate_csv_headers()
    {
        var service = new CsvDataSourceService();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

        try
        {
            const string csv = """
            Item,Item,item
            A,100,200
            """;
            await File.WriteAllTextAsync(path, csv, Encoding.UTF8);

            var records = await service.LoadAsync(path);

            records.Should().HaveCount(1);
            records[0]["Item"].Should().Be("A");
            records[0]["Item_2"].Should().Be("100");
            records[0]["item_3"].Should().Be("200");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
