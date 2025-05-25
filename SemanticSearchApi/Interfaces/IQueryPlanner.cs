using System.Collections.Generic;
using System.Threading.Tasks;

public interface IQueryPlanner
{
    Task<string> PlanAsync(UserIntent intent, Dictionary<string, List<int>> resolvedCompanies);
}
