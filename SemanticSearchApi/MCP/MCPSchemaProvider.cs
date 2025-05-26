using Nest;
using Npgsql;

namespace SemanticSearchApi.MCP
{
    public class MCPSchemaProvider
    {
        private readonly IConfiguration _configuration;
        private readonly NpgsqlDataSource _dataSource;
        private readonly IElasticClient _elasticClient;

        public MCPSchemaProvider(IConfiguration configuration, NpgsqlDataSource dataSource)
        {
            _configuration = configuration;
            _dataSource = dataSource;
            
            var elasticUri = _configuration["Elastic:Uri"];
            var settings = new ConnectionSettings(new Uri(elasticUri))
                .DefaultIndex("documents");
            _elasticClient = new ElasticClient(settings);
        }

        public async Task<object> GetResourceManifest()
        {
            return new[]
            {
                new
                {
                    name = "elasticsearch_indices",
                    description = "Available Elasticsearch indices and their mappings",
                    type = "schema"
                },
                new
                {
                    name = "postgresql_tables",
                    description = "PostgreSQL tables and columns",
                    type = "schema"
                },
                new
                {
                    name = "company_mappings",
                    description = "Company name to ID mappings",
                    type = "data"
                },
                new
                {
                    name = "product_catalog",
                    description = "Available products and their variations",
                    type = "data"
                }
            };
        }

        public async Task<object> GetResource(string resourceName)
        {
            return resourceName switch
            {
                "elasticsearch_indices" => await GetElasticsearchSchema(),
                "postgresql_tables" => await GetPostgreSQLSchema(),
                "company_mappings" => GetCompanyMappings(),
                "product_catalog" => GetProductCatalog(),
                _ => null
            };
        }

        private async Task<object> GetElasticsearchSchema()
        {
            var indices = await _elasticClient.Indices.GetAsync("documents");
            var mapping = await _elasticClient.Indices.GetMappingAsync<object>(m => m.Index("documents"));
            
            return new
            {
                indices = indices.Indices.Keys.Select(k => k.Name),
                mappings = new
                {
                    documents = new
                    {
                        fields = new[]
                        {
                            new { name = "documentId", type = "keyword" },
                            new { name = "parentGlobalExporterId", type = "integer" },
                            new { name = "parentGlobalImporterId", type = "integer" },
                            new { name = "productDesc", type = "text" },
                            new { name = "productDescription", type = "text" },
                            new { name = "productDescEnglish", type = "text" },
                            new { name = "countryId", type = "integer" },
                            new { name = "unitPrice", type = "double" }
                        }
                    }
                }
            };
        }

        private async Task<object> GetPostgreSQLSchema()
        {
            var tables = new List<object>();
            
            using var conn = await _dataSource.OpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT table_name, column_name, data_type 
                FROM information_schema.columns 
                WHERE table_schema = 'public' 
                ORDER BY table_name, ordinal_position";
            
            using var reader = await cmd.ExecuteReaderAsync();
            var tableColumns = new Dictionary<string, List<object>>();
            
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);
                
                if (!tableColumns.ContainsKey(tableName))
                    tableColumns[tableName] = new List<object>();
                
                tableColumns[tableName].Add(new { name = columnName, type = dataType });
            }
            
            return new
            {
                tables = tableColumns.Select(kvp => new
                {
                    name = kvp.Key,
                    columns = kvp.Value
                })
            };
        }

        private object GetCompanyMappings()
        {
            return new
            {
                exporters = new[]
                {
                    new { id = 1, name = "Oceanic Tea House Pvt. Ltd." },
                    new { id = 3, name = "Saffron Valley Traders" },
                    new { id = 5, name = "Trident Agro Exports" },
                    new { id = 7, name = "Evergreen Beverages Co" },
                    new { id = 9, name = "Global Spices Ltd" }
                },
                importers = new[]
                {
                    new { id = 2, name = "Wellness World Trade Co" },
                    new { id = 4, name = "Leaf & Bean Imports" },
                    new { id = 6, name = "SavorLine Global Imports" },
                    new { id = 8, name = "United Natural Goods Inc" },
                    new { id = 10, name = "EuroFlora Essentials" },
                    new { id = 12, name = "Bluewave Imports Ltd" },
                    new { id = 14, name = "FreshMart International" }
                }
            };
        }

        private object GetProductCatalog()
        {
            return new
            {
                products = new[]
                {
                    new { name = "Lemon Soda", type = "Cool Drink", synonyms = new[] { "lemon drink", "citrus soda" } },
                    new { name = "Blueberry Soda", type = "Cool Drink", synonyms = new[] { "blueberry", "berry drink" } },
                    new { name = "Mehandi", type = "Powder", synonyms = new[] { "henna", "mehndi", "hina" } },
                    new { name = "Green Tea", type = "Hot Drink", synonyms = new[] { "green tea", "matcha" } },
                    new { name = "Red Label Tea", type = "Hot Drink", synonyms = new[] { "black tea", "red tea" } },
                    new { name = "Coffee", type = "Hot Drink", synonyms = new[] { "coffee", "espresso", "caffeine" } }
                }
            };
        }
    }
}