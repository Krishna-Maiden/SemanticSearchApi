// Agents/SqlQueryPlanner.cs
using System.Text;
using System.Text.RegularExpressions;
using SemanticSearchApi.Interfaces;
using SemanticSearchApi.Models;

namespace SemanticSearchApi.Agents
{
    public class SqlQueryPlanner : ISqlQueryPlanner
    {
        private readonly ILogger<SqlQueryPlanner> _logger;

        public SqlQueryPlanner(ILogger<SqlQueryPlanner> logger)
        {
            _logger = logger;
        }

        public Task<string> PlanSqlAsync(UserIntent intent)
        {
            var sql = new StringBuilder();
            
            // Determine the type of query needed
            if (IsAggregationQuery(intent))
            {
                sql.Append(BuildAggregationQuery(intent));
            }
            else if (IsStudentDetailsQuery(intent))
            {
                sql.Append(BuildStudentDetailsQuery(intent));
            }
            else
            {
                sql.Append(BuildSearchQuery(intent));
            }

            _logger.LogInformation($"Generated SQL: {sql}");
            return Task.FromResult(sql.ToString());
        }

        private bool IsAggregationQuery(UserIntent intent)
        {
            var query = intent.RawQuery.ToLower();
            return query.Contains("average") || query.Contains("count") || 
                   query.Contains("how many") || query.Contains("statistics") ||
                   query.Contains("summary") || query.Contains("total");
        }

        private bool IsStudentDetailsQuery(UserIntent intent)
        {
            var query = intent.RawQuery.ToLower();
            return query.Contains("all subjects") || query.Contains("transcript") ||
                   query.Contains("student details") || query.Contains("grades for");
        }

        private string BuildAggregationQuery(UserIntent intent)
        {
            var query = intent.RawQuery.ToLower();
            
            // Average grade queries
            if (query.Contains("average"))
            {
                if (query.Contains("by subject"))
                {
                    return @"
                        SELECT Subject, 
                               AVG(CAST(Grade as FLOAT)) as AverageGrade,
                               COUNT(DISTINCT Name) as StudentCount
                        FROM Student
                        GROUP BY Subject
                        ORDER BY AverageGrade DESC";
                }
                else if (query.Contains("by student"))
                {
                    return @"
                        SELECT Name, 
                               AVG(CAST(Grade as FLOAT)) as AverageGrade,
                               COUNT(Subject) as SubjectCount
                        FROM Student
                        GROUP BY Name
                        ORDER BY AverageGrade DESC";
                }
            }
            
            // Count queries
            if (query.Contains("how many") || query.Contains("count"))
            {
                if (query.Contains("students"))
                {
                    var conditions = BuildWhereConditions(intent);
                    return $@"
                        SELECT COUNT(DISTINCT Name) as TotalStudents
                        FROM Student
                        {conditions}";
                }
                else if (query.Contains("subjects"))
                {
                    return @"
                        SELECT Subject, COUNT(DISTINCT Name) as StudentCount
                        FROM Student
                        GROUP BY Subject
                        ORDER BY StudentCount DESC";
                }
            }

            // Grade distribution
            if (query.Contains("grade distribution") || query.Contains("grades"))
            {
                return @"
                    SELECT Grade, 
                           COUNT(*) as Count,
                           COUNT(DISTINCT Name) as StudentCount
                    FROM Student
                    GROUP BY Grade
                    ORDER BY Grade";
            }

            return BuildSearchQuery(intent);
        }

        private string BuildStudentDetailsQuery(UserIntent intent)
        {
            var conditions = BuildWhereConditions(intent);
            
            return $@"
                SELECT s1.Name, s1.Subject, s1.Grade,
                       s2.TotalSubjects, s2.AverageGrade
                FROM Student s1
                JOIN (
                    SELECT Name, 
                           COUNT(Subject) as TotalSubjects,
                           AVG(CAST(Grade as FLOAT)) as AverageGrade
                    FROM Student
                    GROUP BY Name
                ) s2 ON s1.Name = s2.Name
                {conditions}
                ORDER BY s1.Name, s1.Subject";
        }

        private string BuildSearchQuery(UserIntent intent)
        {
            var select = DetermineSelectClause(intent);
            var conditions = BuildWhereConditions(intent);
            var orderBy = DetermineOrderBy(intent);
            var limit = ExtractLimit(intent.RawQuery);

            var sql = $@"
                {select}
                FROM Student
                {conditions}
                {orderBy}";

            if (limit.HasValue)
            {
                // SQL Server uses TOP instead of LIMIT
                sql = sql.Replace("SELECT", $"SELECT TOP {limit.Value}");
            }

            return sql;
        }

        private string DetermineSelectClause(UserIntent intent)
        {
            var query = intent.RawQuery.ToLower();
            
            if (query.Contains("name") && !query.Contains("subject"))
            {
                return "SELECT DISTINCT Name";
            }
            else if (query.Contains("subject") && !query.Contains("name"))
            {
                return "SELECT DISTINCT Subject";
            }
            else if (query.Contains("grade") && query.Contains("subject"))
            {
                return "SELECT Name, Subject, Grade";
            }
            
            return "SELECT *";
        }

        private string BuildWhereConditions(UserIntent intent)
        {
            var conditions = new List<string>();
            var query = intent.RawQuery.ToLower();

            // Extract student name
            var studentName = ExtractStudentName(intent);
            if (!string.IsNullOrEmpty(studentName))
            {
                conditions.Add($"Name LIKE '%{studentName}%'");
            }

            // Extract subject
            if (query.Contains("maths") || query.Contains("math"))
            {
                conditions.Add("Subject = 'Maths'");
            }
            else if (query.Contains("science"))
            {
                conditions.Add("Subject = 'Science'");
            }
            else if (query.Contains("english"))
            {
                conditions.Add("Subject = 'English'");
            }

            // Extract grade conditions
            var gradeCondition = ExtractGradeCondition(query);
            if (!string.IsNullOrEmpty(gradeCondition))
            {
                conditions.Add(gradeCondition);
            }

            return conditions.Any() ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        }

        private string ExtractStudentName(UserIntent intent)
        {
            // Look for patterns like "for John" or "student Emma"
            var patterns = new[]
            {
                @"for\s+(\w+\s*\w*)",
                @"student\s+(\w+\s*\w*)",
                @"grades?\s+(?:of|for)\s+(\w+\s*\w*)",
                @"(\w+\s+\w+)'s\s+grades?"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(intent.RawQuery, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return null;
        }

        private string ExtractGradeCondition(string query)
        {
            // Grade equal to
            var match = Regex.Match(query, @"grade\s*(?:=|equals?|is)\s*(\d+)");
            if (match.Success)
            {
                return $"Grade = {match.Groups[1].Value}";
            }

            // Grade greater than
            match = Regex.Match(query, @"grade\s*(?:>|greater\s+than|above)\s*(\d+)");
            if (match.Success)
            {
                return $"Grade > {match.Groups[1].Value}";
            }

            // Grade less than
            match = Regex.Match(query, @"grade\s*(?:<|less\s+than|below)\s*(\d+)");
            if (match.Success)
            {
                return $"Grade < {match.Groups[1].Value}";
            }

            // Top performers (grade 4 or 5)
            if (query.Contains("top") || query.Contains("best") || query.Contains("excellent"))
            {
                return "Grade >= 4";
            }

            // Poor performers (grade 1 or 2)
            if (query.Contains("poor") || query.Contains("struggling") || query.Contains("fail"))
            {
                return "Grade <= 2";
            }

            return null;
        }

        private string DetermineOrderBy(UserIntent intent)
        {
            var query = intent.RawQuery.ToLower();
            
            if (query.Contains("highest") || query.Contains("best"))
            {
                return "ORDER BY Grade DESC";
            }
            else if (query.Contains("lowest") || query.Contains("worst"))
            {
                return "ORDER BY Grade ASC";
            }
            else if (query.Contains("alphabetical") || query.Contains("name"))
            {
                return "ORDER BY Name";
            }
            
            return "";
        }

        private int? ExtractLimit(string query)
        {
            var match = Regex.Match(query.ToLower(), @"(?:top|first|limit)\s+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var limit))
            {
                return limit;
            }
            return null;
        }
    }
}