using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public class SimpleQueryPlanner : IQueryPlanner
{
    public Task<string> PlanAsync(UserIntent intent, Dictionary<string, List<int>> companyMap)
    {
        var must = new List<object>();

        if (intent.CompanyMentions?.Exporter != null)
        {
            if (companyMap.TryGetValue("Exporter", out var exporters) && exporters.Count > 0)
            {
                must.Add(new Dictionary<string, object>
                {
                    ["terms"] = new Dictionary<string, object> { ["parentGlobalExporterId"] = exporters }
                });
            }
            else
            {
                must.Add(new Dictionary<string, object>
                {
                    ["match"] = new Dictionary<string, object> { ["parentGlobalExporterId"] = intent.CompanyMentions.Exporter }
                });
            }
        }

        if (intent.CompanyMentions?.Importer != null)
        {
            if (companyMap.TryGetValue("Importer", out var importers) && importers.Count > 0)
            {
                must.Add(new Dictionary<string, object>
                {
                    ["terms"] = new Dictionary<string, object> { ["parentGlobalImporterId"] = importers }
                });
            }
            else
            {
                must.Add(new Dictionary<string, object>
                {
                    ["match"] = new Dictionary<string, object> { ["parentGlobalImporterId"] = intent.CompanyMentions.Importer }
                });
            }
        }

        if (!string.IsNullOrEmpty(intent.Product))
        {
            must.Add(new Dictionary<string, object>
            {
                ["match_phrase"] = new Dictionary<string, object>
                {
                    ["productDesc"] = $"*{intent.Product}*"
                }
            });
        }

        var dsl = new Dictionary<string, object>
        {
            ["_source"] = new[] { intent.FocusField },
            ["query"] = new Dictionary<string, object>
            {
                ["bool"] = new Dictionary<string, object>
                {
                    ["must"] = must
                }
            }
        };

        var json = JsonSerializer.Serialize(dsl);
        return Task.FromResult(json);
    }
}
