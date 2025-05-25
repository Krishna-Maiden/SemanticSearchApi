// Data/SqlDataService.cs

using Microsoft.Data.SqlClient;

public class SqlDataService
{
    private readonly string _connectionString;

    public SqlDataService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    public async Task<List<Document>> GetDocumentsAsync()
    {
        var docs = new List<Document>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT Id, TextColumn FROM YourTable", conn);
        var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            docs.Add(new Document
            {
                //Id = reader.GetInt32(0),
                //Text = reader.GetString(1),
            });
        }
        return docs;
    }
}
