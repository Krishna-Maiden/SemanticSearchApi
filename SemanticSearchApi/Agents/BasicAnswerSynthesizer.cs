using System.Text.Json;
using System.Linq;

public class BasicAnswerSynthesizer : IAnswerSynthesizer
{
    public string Summarize(JsonElement result, UserIntent intent)
    {
        var hits = result.GetProperty("hits").GetProperty("hits");
        if (hits.GetArrayLength() == 0)
            return "No matching results found.";

        var values = hits.EnumerateArray()
            .Select(hit => hit.GetProperty("_source").TryGetProperty(intent.FocusField, out var field) ? field.ToString() : null)
            .Where(val => !string.IsNullOrWhiteSpace(val))
            .Take(5);

        return $"Top results for {intent.FocusField}:\n" + string.Join("\n", values);
    }
}
