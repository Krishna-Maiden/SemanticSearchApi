// Agents/SqlAnswerSynthesizer.cs
using System.Text;
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;

namespace SemanticSearchApi.Agents
{
    public class SqlAnswerSynthesizer
    {
        private readonly ILogger<SqlAnswerSynthesizer> _logger;

        public SqlAnswerSynthesizer(ILogger<SqlAnswerSynthesizer> logger)
        {
            _logger = logger;
        }

        public string SummarizeSqlResults(SqlQueryResult result, UserIntent intent)
        {
            if (!result.Success)
            {
                return $"Query failed: {result.Error}";
            }

            if (result.RowCount == 0)
            {
                return "No results found for your query.";
            }

            var summary = new StringBuilder();
            var query = intent.RawQuery.ToLower();

            // Handle count queries
            if (result.Rows.Count == 1 && result.Rows[0].ContainsKey("TotalStudents"))
            {
                var count = result.Rows[0]["TotalStudents"];
                summary.AppendLine($"Total number of students: {count}");
                return summary.ToString();
            }

            // Handle average queries
            if (result.Rows.Any(r => r.ContainsKey("AverageGrade")))
            {
                summary.AppendLine("Average Grades:");
                foreach (var row in result.Rows)
                {
                    if (row.ContainsKey("Subject"))
                    {
                        summary.AppendLine($"- {row["Subject"]}: {Convert.ToDouble(row["AverageGrade"]):F2} (Students: {row["StudentCount"]})");
                    }
                    else if (row.ContainsKey("Name"))
                    {
                        summary.AppendLine($"- {row["Name"]}: {Convert.ToDouble(row["AverageGrade"]):F2} (Subjects: {row["SubjectCount"]})");
                    }
                }
                return summary.ToString();
            }

            // Handle grade distribution
            if (result.Rows.Any(r => r.ContainsKey("Grade") && r.ContainsKey("Count")))
            {
                summary.AppendLine("Grade Distribution:");
                foreach (var row in result.Rows)
                {
                    var studentCount = row.ContainsKey("StudentCount") ? $" ({row["StudentCount"]} students)" : "";
                    summary.AppendLine($"- Grade {row["Grade"]}: {row["Count"]} enrollments{studentCount}");
                }
                return summary.ToString();
            }

            // Handle student details
            if (result.Rows.Any(r => r.ContainsKey("Name") && r.ContainsKey("Subject") && r.ContainsKey("Grade")))
            {
                var groupedByStudent = result.Rows.GroupBy(r => r["Name"].ToString());
                
                foreach (var studentGroup in groupedByStudent.Take(10))
                {
                    summary.AppendLine($"\n{studentGroup.Key}:");
                    foreach (var row in studentGroup)
                    {
                        summary.AppendLine($"  - {row["Subject"]}: Grade {row["Grade"]}");
                    }
                    
                    if (studentGroup.First().ContainsKey("AverageGrade"))
                    {
                        var avg = Convert.ToDouble(studentGroup.First()["AverageGrade"]);
                        summary.AppendLine($"  Average: {avg:F2}");
                    }
                }

                if (result.RowCount > 10)
                {
                    summary.AppendLine($"\n... and {result.RowCount - 10} more records");
                }
                
                return summary.ToString();
            }

            // Default summary
            summary.AppendLine($"Found {result.RowCount} results:");
            foreach (var row in result.Rows.Take(5))
            {
                var rowDesc = string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                summary.AppendLine($"- {rowDesc}");
            }

            if (result.RowCount > 5)
            {
                summary.AppendLine($"... and {result.RowCount - 5} more");
            }

            return summary.ToString();
        }
    }
}