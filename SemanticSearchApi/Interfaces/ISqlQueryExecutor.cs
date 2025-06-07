// Interfaces/ISqlQueryExecutor.cs  
using SemanticSearchApi.Models;
using System.Threading.Tasks;

namespace SemanticSearchApi.Interfaces
{
    public interface ISqlQueryExecutor
    {
        Task<SqlQueryResult> ExecuteAsync(string query);
    }
}