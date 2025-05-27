using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SemanticSearchApi.Agents
{
    public class SimpleQueryPlanner : IQueryPlanner
    {
        public async Task<string> PlanAsync(UserIntent intent, Dictionary<string, List<int>> companyMap)
        {
            var query = new
            {
                query = BuildQuery(intent, companyMap),
                size = intent.Limit ?? 100,
                _source = DetermineSourceFields(intent),
                sort = DetermineSortOrder(intent),
                aggs = BuildAggregations(intent)
            };

            return JsonSerializer.Serialize(query, new JsonSerializerOptions { WriteIndented = true });
        }

        private object BuildQuery(UserIntent intent, Dictionary<string, List<int>> companyMap)
        {
            var mustClauses = new List<object>();

            // Add company filters
            if (companyMap.ContainsKey("Exporter") && companyMap["Exporter"].Any())
            {
                mustClauses.Add(new
                {
                    terms = new { parentGlobalExporterId = companyMap["Exporter"] }
                });
            }

            if (companyMap.ContainsKey("Importer") && companyMap["Importer"].Any())
            {
                mustClauses.Add(new
                {
                    terms = new { parentGlobalImporterId = companyMap["Importer"] }
                });
            }

            // Add product filter
            if (!string.IsNullOrEmpty(intent.Product))
            {
                mustClauses.Add(new
                {
                    multi_match = new
                    {
                        query = intent.Product,
                        fields = new[] { "productDesc", "productDescription", "productDescEnglish" },
                        type = "phrase_prefix"
                    }
                });
            }

            // Add date range filter if specified
            if (!string.IsNullOrEmpty(intent.TimeFilter))
            {
                mustClauses.Add(new
                {
                    range = new
                    {
                        date = new
                        {
                            gte = "now-1y", // Adjust based on TimeFilter
                            lte = "now"
                        }
                    }
                });
            }

            if (mustClauses.Any())
            {
                return new
                {
                    @bool = new
                    {
                        must = mustClauses
                    }
                };
            }

            return new { match_all = new { } };
        }

        private string[] DetermineSourceFields(UserIntent intent)
        {
            var fields = new List<string>();

            // Always include date fields
            fields.Add("date");
            fields.Add("transactionDate");
            fields.Add("shipmentDate");
            
            // Include fields based on focus
            if (intent.FocusField?.ToLower() == "unitprice" || 
                intent.FocusField?.ToLower().Contains("price") ||
                intent.RawQuery.ToLower().Contains("price"))
            {
                fields.Add("unitPrice");
                fields.Add("unitRateUsd");
                fields.Add("unitRateInr");
            }

            // Include company info if needed
            if (intent.RawQuery.ToLower().Contains("supplier") || 
                intent.RawQuery.ToLower().Contains("buyer") ||
                intent.CompanyMentions != null)
            {
                fields.Add("parentGlobalExporterId");
                fields.Add("parentGlobalImporterId");
                fields.Add("exporterName");
                fields.Add("importerName");
            }

            // Include product info
            if (!string.IsNullOrEmpty(intent.Product))
            {
                fields.Add("productDesc");
                fields.Add("productDescription");
                fields.Add("quantity");
                fields.Add("quantityUnit");
            }

            // If no specific fields, include common ones
            if (!fields.Any())
            {
                fields.AddRange(new[] { 
                    "date", "unitRateUsd", "productDesc", 
                    "exporterName", "importerName", "quantity" 
                });
            }

            return fields.Distinct().ToArray();
        }

        private object[] DetermineSortOrder(UserIntent intent)
        {
            // Sort by date descending by default when price is requested
            if (intent.FocusField?.ToLower().Contains("price") == true ||
                intent.RawQuery.ToLower().Contains("price"))
            {
                return new object[]
                {
                    new { date = new { order = "desc" } },
                    new { unitRateUsd = new { order = "desc" } }
                };
            }

            return new object[] { new { _score = new { order = "desc" } } };
        }

        private object BuildAggregations(UserIntent intent)
        {
            // Add aggregations for price summary over time
            if (intent.FocusField?.ToLower().Contains("price") == true ||
                intent.RawQuery.ToLower().Contains("price"))
            {
                return new
                {
                    price_over_time = new
                    {
                        date_histogram = new
                        {
                            field = "date",
                            calendar_interval = "month",
                            format = "yyyy-MM-dd"
                        },
                        aggs = new
                        {
                            avg_price = new { avg = new { field = "unitRateUsd" } },
                            min_price = new { min = new { field = "unitRateUsd" } },
                            max_price = new { max = new { field = "unitRateUsd" } }
                        }
                    },
                    price_stats = new
                    {
                        stats = new { field = "unitRateUsd" }
                    }
                };
            }

            return null;
        }
    }
}