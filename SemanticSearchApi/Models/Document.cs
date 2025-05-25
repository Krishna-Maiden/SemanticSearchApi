
using Pgvector;

public class Document
{
    public string TransactionId { get; set; }
    public string ExporterName { get; set; }
    public string ProductName { get; set; }
    public float PriceInInr { get; set; }
    public string ProductType { get; set; }
    public Vector Embedding { get; set; }
}
