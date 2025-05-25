using Npgsql;
using Dapper;
using Pgvector;

public class PostgresDocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDocumentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task SaveDocumentAsync(Document doc)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        INSERT INTO documents (
            id, transaction_id, exporter_name, product_name, price_in_inr, product_type, embedding
        ) VALUES (
            @id, @transaction_id, @exporter_name, @product_name, @price_in_inr, @product_type, @embedding
        ) ON CONFLICT (id) DO NOTHING;
    ";

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("transaction_id", doc.TransactionId);
        cmd.Parameters.AddWithValue("exporter_name", doc.ExporterName);
        cmd.Parameters.AddWithValue("product_name", doc.ProductName);
        cmd.Parameters.AddWithValue("price_in_inr", doc.PriceInInr);
        cmd.Parameters.AddWithValue("product_type", doc.ProductType);
        cmd.Parameters.AddWithValue("embedding", doc.Embedding); // ✅ Works here

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Document>> GetAllDocumentsAsync()
    {
        using var conn = await _dataSource.OpenConnectionAsync();

        var sql = "SELECT transaction_id, exporter_name, product_name, price_in_inr, product_type, embedding FROM documents";
        var docs = (await conn.QueryAsync<Document>(sql)).ToList();
        return docs;
    }

    public async Task<Document> SearchClosestAsync(Vector queryEmbedding)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var sql = @"
            SELECT transaction_id, exporter_name, product_name, price_in_inr, product_type, embedding
            FROM documents
            ORDER BY embedding <=> @query_embedding
            LIMIT 1;
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("query_embedding", queryEmbedding);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Document
            {
                TransactionId = reader.GetString(0),
                ExporterName = reader.GetString(1),
                ProductName = reader.GetString(2),
                PriceInInr = reader.GetFloat(3),
                ProductType = reader.GetString(4),
                Embedding = (Vector)reader[5]
            };
        }

        return null;
    }

    public async Task<List<Document>> SearchTopNAsync(Vector queryEmbedding, int topN, double threshold)
    {
        var results = new List<Document>();
        await using var conn = await _dataSource.OpenConnectionAsync();

        var sql = @"
            SELECT transaction_id, exporter_name, product_name, price_in_inr, product_type, embedding
            FROM documents
            WHERE embedding <=> @query_embedding < @threshold
            ORDER BY embedding <=> @query_embedding
            LIMIT @top_n;
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("query_embedding", queryEmbedding);
        cmd.Parameters.AddWithValue("top_n", topN);
        cmd.Parameters.AddWithValue("threshold", threshold); // Adjust as needed

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new Document
            {
                TransactionId = reader.GetString(0),
                ExporterName = reader.GetString(1),
                ProductName = reader.GetString(2),
                PriceInInr = reader.GetFloat(3),
                ProductType = reader.GetString(4),
                Embedding = (Vector)reader[5]
            });
        }

        return results;
    }

    public async Task<object> ExecuteCustomSQLAsync(string sql)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var results = new List<Dictionary<string, object>>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }
}
