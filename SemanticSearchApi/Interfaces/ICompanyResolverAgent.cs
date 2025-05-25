using System.Collections.Generic;
using System.Threading.Tasks;

public interface ICompanyResolverAgent
{
    Task<Dictionary<string, List<int>>> ResolveCompaniesAsync(string queryText);
}
