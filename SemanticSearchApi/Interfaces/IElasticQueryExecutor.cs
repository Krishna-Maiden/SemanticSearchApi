using System.Text.Json;
using System.Threading.Tasks;

public interface IElasticQueryExecutor
{
    Task<JsonElement> ExecuteAsync(string queryDsl);
}
