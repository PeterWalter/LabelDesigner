using CsvHelper;
using CsvHelper.Configuration;
using LabelDesigner.Core.Interfaces;
using System.Globalization;

namespace LabelDesigner.Infrastructure.Data;

public class CsvDataSourceService : IDataSourceService
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadAsync(string path)
    {
        var results = new List<IReadOnlyDictionary<string, string>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in headers)
                row[header] = csv.GetField(header) ?? "";
            results.Add(row);
        }

        return results;
    }
}
