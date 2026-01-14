namespace Core.Quality;

/// <summary>
/// Calculates cyclomatic complexity for C# methods and classes
/// Uses control flow analysis to determine code complexity
/// </summary>
public class ComplexityAnalyzer
{
    private readonly QualityRules _rules;
    
    /// <summary>
    /// Initializes complexity analyzer with quality rules
    /// </summary>
    /// <param name="rules">Quality validation rules</param>
    public ComplexityAnalyzer(QualityRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }
    
    /// <summary>
    /// Calculates cyclomatic complexity for a method
    /// </summary>
    /// <param name="method">Method syntax node to analyze</param>
    /// <returns>Cyclomatic complexity value</returns>
    public int CalculateMethodComplexity(MethodDeclarationSyntax method)
    {
        if (method?.Body == null && method?.ExpressionBody == null)
            return 1;
            
        var complexity = 1; // Base complexity
        var walker = new ComplexityWalker();
        walker.Visit(method);
        
        return complexity + walker.ComplexityPoints;
    }
    
    /// <summary>
    /// Validates method complexity against quality rules
    /// </summary>
    /// <param name="method">Method to validate</param>
    /// <returns>Quality violation if complexity exceeds threshold</returns>
    public QualityViolation? ValidateMethodComplexity(MethodDeclarationSyntax method)
    {
        var complexity = CalculateMethodComplexity(method);
        
        if (complexity > _rules.MaxCyclomaticComplexity)
        {
            var location = method.GetLocation();
            return new QualityViolation
            {
                RuleName = "MaxCyclomaticComplexity",
                Message = $"Method '{method.Identifier}' has complexity {complexity}, max allowed: {_rules.MaxCyclomaticComplexity}",
                Severity = GetComplexitySeverity(complexity),
                LineNumber = location.GetLineSpan().StartLinePosition.Line + 1,
                Column = location.GetLineSpan().StartLinePosition.Character + 1,
                FileName = location.SourceTree?.FilePath ?? "Unknown"
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Determines violation severity based on complexity value
    /// </summary>
    private string GetComplexitySeverity(int complexity) => complexity switch
    {
        <= 10 => "Warning",
        <= 20 => "Error",
        _ => "Critical"
    };
}

/// <summary>
/// Syntax walker for counting cyclomatic complexity points
/// Visits control flow statements that increase complexity
/// </summary>
internal class ComplexityWalker : CSharpSyntaxWalker
{
    /// <summary>
    /// Total complexity points found
    /// </summary>
    public int ComplexityPoints { get; private set; }
    
    /// <summary>
    /// Visits if statements and increments complexity
    /// </summary>
    public override void VisitIfStatement(IfStatementSyntax node)
    {
        ComplexityPoints++;
        base.VisitIfStatement(node);
    }
    
    /// <summary>
    /// Visits while loops and increments complexity
    /// </summary>
    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        ComplexityPoints++;
        base.VisitWhileStatement(node);
    }
    
    /// <summary>
    /// Visits for loops and increments complexity
    /// </summary>
    public override void VisitForStatement(ForStatementSyntax node)
    {
        ComplexityPoints++;
        base.VisitForStatement(node);
    }
    
    /// <summary>
    /// Visits foreach loops and increments complexity
    /// </summary>
    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        ComplexityPoints++;
        base.VisitForEachStatement(node);
    }
    
    /// <summary>
    /// Visits switch statements and increments complexity for each case
    /// </summary>
    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        ComplexityPoints += node.Sections.Count;
        base.VisitSwitchStatement(node);
    }
    
    /// <summary>
    /// Visits catch clauses and increments complexity
    /// </summary>
    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        ComplexityPoints++;
        base.VisitCatchClause(node);
    }
    
    /// <summary>
    /// Visits conditional expressions and increments complexity
    /// </summary>
    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        ComplexityPoints++;
        base.VisitConditionalExpression(node);
    }
}