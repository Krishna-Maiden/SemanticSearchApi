// Models/StudentRecord.cs
namespace SemanticSearchApi.Models
{
    public class StudentRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Subject { get; set; }
        public int Grade { get; set; }
    }

    public class StudentSummary
    {
        public string Name { get; set; }
        public List<SubjectGrade> Subjects { get; set; } = new List<SubjectGrade>();
        public double AverageGrade { get; set; }
    }

    public class SubjectGrade
    {
        public string Subject { get; set; }
        public int Grade { get; set; }
    }
}

// Interfaces/ISqlQueryPlanner.cs
namespace SemanticSearchApi.Interfaces
{
    public interface ISqlQueryPlanner
    {
        Task<string> PlanSqlAsync(UserIntent intent);
    }
}

// Interfaces/ISqlQueryExecutor.cs
namespace SemanticSearchApi.Interfaces
{
    public interface ISqlQueryExecutor
    {
        Task<SqlQueryResult> ExecuteAsync(string query);
    }

    public class SqlQueryResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        public int RowCount => Rows?.Count ?? 0;
        public Dictionary<string, object> Aggregations { get; set; }
    }
}