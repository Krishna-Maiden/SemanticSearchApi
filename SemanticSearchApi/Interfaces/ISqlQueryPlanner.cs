// Interfaces/ISqlQueryPlanner.cs
using SemanticSearchApi.Models;
using System.Threading.Tasks;

namespace SemanticSearchApi.Interfaces
{
    public interface ISqlQueryPlanner
    {
        Task<string> PlanSqlAsync(UserIntent intent);
    }
}