using System.Text.Json;
using SemanticSearchApi.Tools.Base;

namespace SemanticSearchApi.Tools
{
    public class CompanyResolverTool : SemanticSearchTool
    {
        private readonly ICompanyResolverAgent _companyResolver;
        
        public override string ToolType => "resolver";

        public CompanyResolverTool(ICompanyResolverAgent companyResolver) 
            : base("company_resolver", "Resolves company names to their database IDs")
        {
            _companyResolver = companyResolver;
        }

        protected override async Task<object> ExecuteAsync(string input)
        {
            var companyMap = await _companyResolver.ResolveCompaniesAsync(input);
            return new
            {
                query = input,
                resolved = companyMap,
                count = companyMap.Values.Sum(v => v.Count)
            };
        }
    }
}
