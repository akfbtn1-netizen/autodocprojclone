using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Enterprise.Documentation.Core.Application.Services.SqlAnalysis
{
    public interface ISqlAnalysisService
    {
        SqlAnalysisResult AnalyzeSql(string sqlCode, string? knownTicket = null);
    }

    public class SqlAnalysisService : ISqlAnalysisService
    {
        public SqlAnalysisResult AnalyzeSql(string sqlCode, string? knownTicket = null)
        {
            var result = new SqlAnalysisResult
            {
                Schema = ExtractSchema(sqlCode, out var procName),
                ProcedureName = procName,
                Parameters = ExtractParameters(sqlCode),
                Dependencies = ExtractDependencies(sqlCode),
                Complexity = CalculateComplexity(sqlCode),
                LogicSteps = ExtractLogicSteps(sqlCode),
                ValidationRules = ExtractValidationRules(sqlCode),
                BracketedChange = DetectBracketedChange(sqlCode, knownTicket)
            };
            return result;
        }

        // ... [rest of the methods stay the same until DetectBracketedChange]

        private BracketedChange? DetectBracketedChange(string sqlCode, string? knownTicket = null)
        {
            // If we already know the ticket (from CodeExtractionService), use it directly
            if (!string.IsNullOrEmpty(knownTicket))
            {
                // Normalize the known ticket
                var numberMatch = Regex.Match(knownTicket, @"\d{3,4}");
                if (numberMatch.Success)
                {
                    var ticketNumber = numberMatch.Value;
                    var normalizedTicket = $"BAS-{ticketNumber}";
                    
                    return new BracketedChange
                    {
                        Ticket = normalizedTicket,
                        Code = sqlCode, // The entire sqlCode IS the bracketed change
                        StartLine = 1,
                        EndLine = sqlCode.Split('\n').Length
                    };
                }
            }
            
            // Otherwise, search for bracketed markers in the code
            // Ultra-flexible JIRA pattern handles:
            // BAS-9818, BAS9818, BAS 9818, BAS- 9818, BAS -9818, BAS - 9818, [BAS-9818], etc.
            
            var jiraPattern = @"BAS\s*-?\s*\d{3,4}";
            
            // Match Begin marker with any number of dashes, optional brackets
            var beginPattern = $@"-{{1,}}\s*Begin\s*\[?\s*({jiraPattern})\s*\]?";
            var beginMatch = Regex.Match(sqlCode, beginPattern, RegexOptions.IgnoreCase);
            
            if (!beginMatch.Success) return null;
            
            var ticket = beginMatch.Groups[1].Value.Trim();
            
            // Normalize ticket format - extract just the numbers
            var numberMatch2 = Regex.Match(ticket, @"\d{3,4}");
            if (!numberMatch2.Success) return null;
            
            var ticketNumber2 = numberMatch2.Value;
            
            // Match End marker - be flexible about the exact format
            var endPattern = $@"-{{1,}}\s*End\s*\[?\s*BAS\s*-?\s*{ticketNumber2}\s*\]?";
            var endMatch = Regex.Match(sqlCode, endPattern, RegexOptions.IgnoreCase);
            
            if (!endMatch.Success) return null;
            
            var startPos = beginMatch.Index + beginMatch.Length;
            var endPos = endMatch.Index;
            
            if (endPos <= startPos) return null;
            
            var code = sqlCode.Substring(startPos, endPos - startPos).Trim();
            
            return new BracketedChange
            {
                Ticket = $"BAS-{ticketNumber2}", // Normalize to standard format
                Code = code,
                StartLine = sqlCode.Substring(0, beginMatch.Index).Split('\n').Length,
                EndLine = sqlCode.Substring(0, endMatch.Index).Split('\n').Length
            };
        }

        // [Keep all other helper methods exactly as they were]
        private string ExtractSchema(string sqlCode, out string procName)
        {
            var match = Regex.Match(sqlCode, @"(?:CREATE|ALTER)\s+PROCEDURE\s+\[?(\w+)\]?\.\[?(\w+)\]?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                procName = match.Groups[2].Value;
                return match.Groups[1].Value;
            }
            procName = string.Empty;
            return string.Empty;
        }

        private List<SqlParameter> ExtractParameters(string sqlCode)
        {
            var parameters = new List<SqlParameter>();
            var paramPattern = new Regex(@"@(\w+)\s+([\w()]+(?:\(\d+(?:,\s*\d+)?\))?)(?:\s+(OUTPUT|OUT))?", RegexOptions.IgnoreCase);
            var matches = paramPattern.Matches(sqlCode);
            foreach (Match match in matches)
            {
                parameters.Add(new SqlParameter
                {
                    Name = match.Groups[1].Value,
                    Type = match.Groups[2].Value,
                    Direction = string.IsNullOrEmpty(match.Groups[3].Value) ? "IN" : "OUT",
                    Description = string.Empty
                });
            }
            return parameters;
        }

        private SqlDependencies ExtractDependencies(string sqlCode)
        {
            var dependencies = new SqlDependencies
            {
                Tables = new List<string>(),
                Procedures = new List<string>(),
                TempTables = new List<string>(),
                ControlTables = new List<string>()
            };
            
            var tablePattern = new Regex(@"(?:FROM|JOIN)\s+\[?(\w+)\]?\.\[?(\w+)\]?(?:\s+AS\s+\w+|\s+\w+)?", RegexOptions.IgnoreCase);
            var tableMatches = tablePattern.Matches(sqlCode);
            foreach (Match match in tableMatches)
            {
                var schema = match.Groups[1].Value;
                var table = match.Groups[2].Value;
                var fullName = $"{schema}.{table}";
                
                if (schema.Equals("gwControl", StringComparison.OrdinalIgnoreCase) || 
                    table.StartsWith("ctl", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dependencies.ControlTables.Contains(fullName))
                        dependencies.ControlTables.Add(fullName);
                }
                else
                {
                    if (!dependencies.Tables.Contains(fullName))
                        dependencies.Tables.Add(fullName);
                }
            }
            
            var procPattern = new Regex(@"EXEC\s+([\w.]+)", RegexOptions.IgnoreCase);
            var procMatches = procPattern.Matches(sqlCode);
            foreach (Match match in procMatches)
            {
                var procName = match.Groups[1].Value;
                if (!dependencies.Procedures.Contains(procName))
                    dependencies.Procedures.Add(procName);
            }
            
            var tempPattern = new Regex(@"#(\w+)", RegexOptions.IgnoreCase);
            var tempMatches = tempPattern.Matches(sqlCode);
            var seenTempTables = new HashSet<string>();
            foreach (Match match in tempMatches)
            {
                var tempTable = match.Groups[1].Value;
                if (seenTempTables.Add(tempTable))
                    dependencies.TempTables.Add(tempTable);
            }
            
            return dependencies;
        }

        private SqlComplexity CalculateComplexity(string sqlCode)
        {
            var lines = sqlCode.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            int nonCommentLines = 0, tempTableCount = 0, cteCount = 0, joinCount = 0;
            
            var seenTempTables = new HashSet<string>();
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("--") && !trimmed.StartsWith("-") && trimmed.Length > 0)
                    nonCommentLines++;
                
                if (trimmed.Contains("#"))
                {
                    var tempMatches = Regex.Matches(trimmed, @"#(\w+)");
                    foreach (Match m in tempMatches)
                        seenTempTables.Add(m.Groups[1].Value);
                }
                
                if (trimmed.StartsWith("with ", StringComparison.OrdinalIgnoreCase) || 
                    trimmed.StartsWith(";with ", StringComparison.OrdinalIgnoreCase))
                    cteCount++;
                
                if (Regex.IsMatch(trimmed, @"\bJOIN\b", RegexOptions.IgnoreCase))
                    joinCount++;
            }
            
            tempTableCount = seenTempTables.Count;
            
            string level = nonCommentLines < 100 ? "LOW" : 
                          nonCommentLines < 200 ? "MEDIUM" : 
                          nonCommentLines < 500 ? "HIGH" : "VERY HIGH";
            
            return new SqlComplexity
            {
                LineCount = nonCommentLines,
                TempTableCount = tempTableCount,
                CteCount = cteCount,
                JoinCount = joinCount,
                ComplexityLevel = level
            };
        }

        private List<string> ExtractLogicSteps(string sqlCode)
        {
            var steps = new List<string>();
            var stepPattern = new Regex(@"--+\s*(Step|Phase|Section)[^\n]*", RegexOptions.IgnoreCase);
            var matches = stepPattern.Matches(sqlCode);
            foreach (Match match in matches)
            {
                steps.Add(match.Value.TrimStart('-').Trim());
            }
            return steps;
        }

        private List<ValidationRule> ExtractValidationRules(string sqlCode)
        {
            var rules = new List<ValidationRule>();
            var wherePattern = new Regex(@"WHERE\s+([^\n;]+)", RegexOptions.IgnoreCase);
            var matches = wherePattern.Matches(sqlCode);
            foreach (Match match in matches)
            {
                rules.Add(new ValidationRule { RuleText = match.Groups[1].Value.Trim() });
            }
            return rules;
        }
    }

    // [Model classes stay exactly the same]
    public class SqlAnalysisResult
    {
        public string Schema { get; set; } = null!;
        public string ProcedureName { get; set; } = null!;
        public List<SqlParameter> Parameters { get; set; } = null!;
        public SqlDependencies Dependencies { get; set; } = null!;
        public SqlComplexity Complexity { get; set; } = null!;
        public List<string> LogicSteps { get; set; } = null!;
        public List<ValidationRule> ValidationRules { get; set; } = null!;
        public BracketedChange? BracketedChange { get; set; }
    }

    public class SqlParameter
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Direction { get; set; } = null!;
        public string Description { get; set; } = null!;
    }

    public class SqlDependencies
    {
        public List<string> Tables { get; set; } = null!;
        public List<string> Procedures { get; set; } = null!;
        public List<string> TempTables { get; set; } = null!;
        public List<string> ControlTables { get; set; } = null!;
    }

    public class SqlComplexity
    {
        public int LineCount { get; set; }
        public int TempTableCount { get; set; }
        public int CteCount { get; set; }
        public int JoinCount { get; set; }
        public string ComplexityLevel { get; set; } = null!;
    }

    public class ValidationRule
    {
        public string RuleText { get; set; } = null!;
    }

    public class BracketedChange
    {
        public string Ticket { get; set; } = null!;
        public string Code { get; set; } = null!;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
    }
}