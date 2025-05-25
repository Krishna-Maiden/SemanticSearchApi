using System.Text.Json;
using System.Text.RegularExpressions;

public static class CompanyNameRewriter
{
    public static async Task<string> ReplaceCompanyNamesWithIdsAsync(
        string elasticDsl,
        Func<string, Task<List<int>>> resolveCompanyIdsAsync)
    {
        string query = Regex.Match(elasticDsl ?? string.Empty, @"\{[\s\S]*\}").Value;
        using var jsonDoc = JsonDocument.Parse(query);
        var root = jsonDoc.RootElement.Clone();
        var rootStr = root.GetRawText();

        foreach (var field in new[] { "parentGlobalExporterId", "parentGlobalImporterId" })
        {
            var pattern = $"\\\"{field}\\\":\\s*\\\"(.*?)\\\"";
            var match = Regex.Match(rootStr, pattern);
            if (match.Success)
            {
                var name = match.Groups[1].Value;
                var ids = await resolveCompanyIdsAsync(name);
                if (ids.Any())
                {
                    var jsonArray = $"[{string.Join(",", ids)}]";
                    rootStr = Regex.Replace(rootStr, pattern, $"\"{field}\": {jsonArray}");
                    rootStr = Regex.Replace(
    rootStr,
    $"\\{{\\s*\\\"match\\\"\\s*:\\s*\\{{\\s*\\\"{field}\\\"\\s*:\\s*\\[.*?\\]\\s*\\}}\\s*\\}}",
    $"{{\"terms\": {{ \"{field}\": {jsonArray} }} }}");
                }
            }
        }

        rootStr = Regex.Replace(
rootStr,
"\\{\\s*\"wildcard\"\\s*:\\s*\\{\\s*\"productDescEnglish\"\\s*:\\s*\"(.*?)\"\\s*\\}\\s*\\}",
m => $"{{ \"match_phrase\": {{ \"productDesc\": \"*{m.Groups[1].Value}*\" }} }}"
);

        return rootStr;
    }
}
