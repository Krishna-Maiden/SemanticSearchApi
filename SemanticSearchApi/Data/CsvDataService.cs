
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO;

public class CsvDataService
{
    private readonly string _csvPath;

    public CsvDataService(IConfiguration config)
    {
        _csvPath = Path.Combine(Directory.GetCurrentDirectory(), "storage", "semantic_sql_data.csv");
    }

    public List<Document> GetDocuments()
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(_csvPath);
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<Document>().ToList();
        return records;
    }
}
