using System.Text.Json;

public interface IAnswerSynthesizer
{
    string Summarize(JsonElement results, UserIntent intent);
}
