using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class OpenAISummarizer : IAnswerSynthesizer
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAISummarizer> _logger;

    public OpenAISummarizer(IConfiguration configuration, ILogger<OpenAISummarizer> logger)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", configuration["OpenAI:ApiKey"]);
        _logger = logger;
    }

    public string Summarize(JsonElement results, UserIntent intent)
    {
        try
        {
            // Check if we have aggregation results for price summaries
            if (results.TryGetProperty("aggregations", out var aggregations))
            {
                return SummarizePriceData(results, aggregations, intent);
            }

            // Regular summarization for non-aggregated results
            return SummarizeSearchResults(results, intent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing results");
            return GenerateFallbackSummary(results, intent);
        }
    }

    private string SummarizePriceData(JsonElement results, JsonElement aggregations, UserIntent intent)
    {
        var summary = new StringBuilder();

        // Get basic stats if available
        if (aggregations.TryGetProperty("price_stats", out var priceStats))
        {
            var count = priceStats.GetProperty("count").GetInt32();
            var min = priceStats.GetProperty("min").GetDouble();
            var max = priceStats.GetProperty("max").GetDouble();
            var avg = priceStats.GetProperty("avg").GetDouble();

            summary.AppendLine($"Price Summary for {intent.Product ?? "the products"}:");
            summary.AppendLine($"- Total transactions: {count}");
            summary.AppendLine($"- Price range: ${min:F2} - ${max:F2} USD");
            summary.AppendLine($"- Average price: ${avg:F2} USD");
            summary.AppendLine();
        }

        // Get price trends over time if available
        if (aggregations.TryGetProperty("price_over_time", out var priceOverTime) &&
            priceOverTime.TryGetProperty("buckets", out var buckets))
        {
            summary.AppendLine("Price trends by month:");
            foreach (var bucket in buckets.EnumerateArray())
            {
                try
                {
                    var date = bucket.GetProperty("key_as_string").GetString();
                    var avgPrice = bucket.GetProperty("avg_price").GetProperty("value").GetDouble();
                    var docCount = bucket.GetProperty("doc_count").GetInt32();

                    if (docCount > 0)
                    {
                        summary.AppendLine($"- {date}: ${avgPrice:F2} USD (from {docCount} transactions)");
                    }
                }
                catch (Exception)
                {
                    continue;
                }
                
            }
            summary.AppendLine();
        }

        // Add details from individual hits
        if (results.TryGetProperty("hits", out var hits) &&
            hits.TryGetProperty("hits", out var documents))
        {
            summary.AppendLine("Recent transactions:");
            var count = 0;
            foreach (var doc in documents.EnumerateArray())
            {
                if (count++ >= 5) break; // Show only top 5

                var source = doc.GetProperty("_source");
                var price = source.TryGetProperty("unitRateUsd", out var usd) ? usd.GetDouble() : 0;
                var date = source.TryGetProperty("date", out var d) ? d.GetString() :
                           source.TryGetProperty("transactionDate", out var td) ? td.GetString() :
                           source.TryGetProperty("shipmentDate", out var sd) ? sd.GetString() : "N/A";

                var product = source.TryGetProperty("productDesc", out var pd) ? pd.GetString() :
                              source.TryGetProperty("productDescription", out var pdesc) ? pdesc.GetString() : "N/A";

                summary.AppendLine($"- {date}: ${price:F2} USD for {product}");
            }
        }

        return summary.ToString();
    }

    private string SummarizeSearchResults(JsonElement results, UserIntent intent)
    {
        var summary = new StringBuilder();

        if (results.TryGetProperty("hits", out var hits) &&
            hits.TryGetProperty("hits", out var documents))
        {
            var totalHits = hits.GetProperty("total").GetProperty("value").GetInt32();
            summary.AppendLine($"Found {totalHits} results for your query.");
            summary.AppendLine();

            foreach (var doc in documents.EnumerateArray().Take(10))
            {
                var source = doc.GetProperty("_source");
                var description = ExtractRelevantInfo(source, intent);
                if (!string.IsNullOrEmpty(description))
                {
                    summary.AppendLine($"• {description}");
                }
            }
        }

        return summary.ToString();
    }

    private string ExtractRelevantInfo(JsonElement source, UserIntent intent)
    {
        var parts = new List<string>();

        // Extract date
        if (source.TryGetProperty("date", out var date))
        {
            parts.Add($"Date: {date.GetString()}");
        }

        // Extract price based on focus
        if (intent.FocusField?.ToLower().Contains("price") == true)
        {
            if (source.TryGetProperty("unitRateUsd", out var usd))
            {
                parts.Add($"Price: ${usd.GetDouble():F2} USD");
            }
            else if (source.TryGetProperty("unitPrice", out var price))
            {
                parts.Add($"Price: {price.GetDouble():F2}");
            }
        }

        // Extract product info
        if (source.TryGetProperty("productDesc", out var product))
        {
            parts.Add($"Product: {product.GetString()}");
        }

        // Extract company info
        if (source.TryGetProperty("exporterName", out var exporter))
        {
            parts.Add($"Supplier: {exporter.GetString()}");
        }
        if (source.TryGetProperty("importerName", out var importer))
        {
            parts.Add($"Buyer: {importer.GetString()}");
        }

        return string.Join(", ", parts);
    }

    private string GenerateFallbackSummary(JsonElement results, UserIntent intent)
    {
        try
        {
            if (results.TryGetProperty("hits", out var hits))
            {
                var total = hits.GetProperty("total").GetProperty("value").GetInt32();
                return $"Found {total} results for your query about {intent.Product ?? "products"}.";
            }
        }
        catch { }

        return "I found some results but couldn't generate a detailed summary. Please check the raw data.";
    }
}