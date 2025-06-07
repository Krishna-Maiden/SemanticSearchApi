// Models/SqlQueryResult.cs (Generic result model)
namespace SemanticSearchApi.Models
{
    public class SqlQueryResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        public int RowCount => Rows?.Count ?? 0;
        public Dictionary<string, object> Aggregations { get; set; }
        public List<string> Corrections { get; set; } = new List<string>(); // Track corrections made
    }

    public class QueryResponse
    {
        public bool Success { get; set; }
        public string Summary { get; set; }
        public string GeneratedQuery { get; set; }
        public UserIntent Intent { get; set; }
        public object RawResults { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> Corrections { get; set; } = new List<string>(); // User-friendly corrections
    }

    public class TableMetadata
    {
        public string TableName { get; set; }
        public List<ColumnMetadata> Columns { get; set; } = new();
        public string Description { get; set; }
        public long RecordCount { get; set; }
    }

    public class ColumnMetadata
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
        public List<object> SampleValues { get; set; } = new();
    }

    public class DatabaseSchema
    {
        public List<TableMetadata> Tables { get; set; } = new();
        public List<RelationshipMetadata> Relationships { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    public class RelationshipMetadata
    {
        public string ParentTable { get; set; }
        public string ParentColumn { get; set; }
        public string ChildTable { get; set; }
        public string ChildColumn { get; set; }
        public string RelationshipType { get; set; } // OneToMany, ManyToOne, etc.
    }

    public class GenericDataRecord
    {
        public Dictionary<string, object> Fields { get; set; } = new();
        public string TableSource { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class AggregationResult
    {
        public string GroupByField { get; set; }
        public object GroupByValue { get; set; }
        public Dictionary<string, object> Aggregates { get; set; } = new();
    }

    public class StatisticalSummary
    {
        public string FieldName { get; set; }
        public string DataType { get; set; }
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
        public object AvgValue { get; set; }
        public long Count { get; set; }
        public long NullCount { get; set; }
        public List<object> DistinctValues { get; set; } = new();
    }
}