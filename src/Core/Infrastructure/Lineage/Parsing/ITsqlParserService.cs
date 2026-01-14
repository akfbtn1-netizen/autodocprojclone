using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing;

/// <summary>
/// Service for parsing T-SQL using Microsoft ScriptDom.
/// Provides full-fidelity AST parsing for SQL Server 2022.
/// </summary>
public interface ITsqlParserService
{
    /// <summary>
    /// Parses T-SQL text into an AST fragment.
    /// </summary>
    ParseResult Parse(string sql);

    /// <summary>
    /// Parses T-SQL text with option to continue on errors.
    /// </summary>
    ParseResult Parse(string sql, bool continueOnError);

    /// <summary>
    /// Validates T-SQL syntax without returning AST.
    /// </summary>
    IReadOnlyList<ParseError> ValidateSyntax(string sql);

    /// <summary>
    /// Extracts the SQL definition text from a procedure/view/function.
    /// </summary>
    string? ExtractDefinitionBody(string sql);
}

/// <summary>
/// Result of parsing T-SQL.
/// </summary>
public record ParseResult
{
    public TSqlFragment? Fragment { get; init; }
    public IReadOnlyList<ParseError> Errors { get; init; } = Array.Empty<ParseError>();
    public bool IsSuccess => Fragment != null && Errors.Count == 0;
    public bool HasWarnings => Errors.Count > 0 && Fragment != null;
}
