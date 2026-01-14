using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing;

/// <summary>
/// Implementation of T-SQL parsing using ScriptDom TSql160Parser.
/// Provides full SQL Server 2022 syntax support.
/// </summary>
public class TsqlParserService : ITsqlParserService
{
    private readonly ILogger<TsqlParserService> _logger;
    private readonly TSql160Parser _parser;

    public TsqlParserService(ILogger<TsqlParserService> logger)
    {
        _logger = logger;
        // Use SQL Server 2022 parser with quoted identifiers enabled
        _parser = new TSql160Parser(initialQuotedIdentifiers: true);
    }

    public ParseResult Parse(string sql)
    {
        return Parse(sql, continueOnError: false);
    }

    public ParseResult Parse(string sql, bool continueOnError)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new ParseResult
            {
                Errors = new[] { CreateError("Empty SQL text provided") }
            };
        }

        try
        {
            using var reader = new StringReader(sql);
            var fragment = _parser.Parse(reader, out var errors);

            var errorList = errors?.ToList() ?? new List<ParseError>();

            if (errorList.Count > 0)
            {
                foreach (var error in errorList)
                {
                    _logger.LogWarning(
                        "T-SQL parse error at Line {Line}, Column {Column}: {Message}",
                        error.Line,
                        error.Column,
                        error.Message);
                }

                if (!continueOnError && fragment == null)
                {
                    return new ParseResult { Errors = errorList };
                }
            }

            return new ParseResult
            {
                Fragment = fragment,
                Errors = errorList
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during T-SQL parsing");
            return new ParseResult
            {
                Errors = new[] { CreateError($"Parse exception: {ex.Message}") }
            };
        }
    }

    public IReadOnlyList<ParseError> ValidateSyntax(string sql)
    {
        var result = Parse(sql, continueOnError: true);
        return result.Errors;
    }

    public string? ExtractDefinitionBody(string sql)
    {
        var result = Parse(sql);
        if (!result.IsSuccess || result.Fragment is not TSqlScript script)
            return null;

        // Look for CREATE PROCEDURE/VIEW/FUNCTION statements
        foreach (var batch in script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case CreateProcedureStatement proc:
                        return ExtractStatementBody(proc.StatementList, sql);

                    case CreateViewStatement view:
                        return ExtractSelectStatement(view.SelectStatement, sql);

                    case CreateFunctionStatement func:
                        return func.ReturnType switch
                        {
                            ScalarFunctionReturnType scalar =>
                                ExtractStatementBody(func.StatementList, sql),
                            SelectFunctionReturnType select =>
                                ExtractSelectStatement(select.SelectStatement, sql),
                            TableValuedFunctionReturnType tableValued when func.StatementList != null =>
                                ExtractStatementBody(func.StatementList, sql),
                            _ => null
                        };
                }
            }
        }

        return null;
    }

    private static string? ExtractStatementBody(StatementList? statementList, string originalSql)
    {
        if (statementList == null || statementList.Statements.Count == 0)
            return null;

        var firstStatement = statementList.Statements[0];
        var lastStatement = statementList.Statements[^1];

        var startOffset = firstStatement.StartOffset;
        var endOffset = lastStatement.StartOffset + lastStatement.FragmentLength;

        if (startOffset >= 0 && endOffset <= originalSql.Length)
        {
            return originalSql.Substring(startOffset, endOffset - startOffset);
        }

        return null;
    }

    private static string? ExtractSelectStatement(SelectStatement? selectStatement, string originalSql)
    {
        if (selectStatement == null)
            return null;

        var startOffset = selectStatement.StartOffset;
        var length = selectStatement.FragmentLength;

        if (startOffset >= 0 && startOffset + length <= originalSql.Length)
        {
            return originalSql.Substring(startOffset, length);
        }

        return null;
    }

    private static ParseError CreateError(string message)
    {
        // ParseError constructor is internal, so we create a mock
        // In real implementation, we'd use a custom error type
        return new MockParseError(message);
    }

    /// <summary>
    /// Mock ParseError for custom error messages.
    /// </summary>
    private class MockParseError : ParseError
    {
        public MockParseError(string message)
        {
            // Using reflection or accepting that we can't fully mock
            // In production, use a wrapper type instead
        }
    }
}
