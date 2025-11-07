using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Quality;

/// <summary>
/// Validates C# source code against quality rules
/// Performs comprehensive analysis including complexity, length, and documentation
/// </summary>
public class QualityValidator
{
    private readonly QualityRules _rules;
    private readonly ComplexityAnalyzer _complexityAnalyzer;
    
    /// <summary>
    /// Initializes quality validator with specified rules
    /// </summary>
    /// <param name="rules">Quality validation rules to apply</param>
    public QualityValidator(QualityRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _complexityAnalyzer = new ComplexityAnalyzer(_rules);
    }
    
    /// <summary>
    /// Validates source code file against all quality rules
    /// </summary>
    /// <param name="sourceCode">C# source code to validate</param>
    /// <param name="fileName">Name of the file being validated</param>
    /// <returns>Quality validation result with violations</returns>
    public QualityResult ValidateSourceCode(string sourceCode, string fileName)
    {
        var result = new QualityResult
        {
            FileName = fileName,
            LinesOfCode = sourceCode.Split('\n').Length
        };
        
        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();
            
            ValidateClasses(root, result);
            ValidateMethods(root, result);
            ValidateDocumentation(root, result);
            
            result.CalculateScore();
        }
        catch (Exception ex)
        {
            result.ComplexityViolations.Add(new QualityViolation
            {
                RuleName = "ParseError",
                Message = $"Failed to parse file: {ex.Message}",
                Severity = "Error",
                FileName = fileName,
                LineNumber = 1,
                Column = 1
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// Validates class declarations for length violations
    /// </summary>
    private void ValidateClasses(SyntaxNode root, QualityResult result)
    {
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classNode in classes)
        {
            var lineCount = GetNodeLineCount(classNode);
            if (lineCount > _rules.MaxClassLines)
            {
                var location = classNode.GetLocation();
                result.ClassLengthViolations.Add(new QualityViolation
                {
                    RuleName = "MaxClassLines",
                    Message = $"Class '{classNode.Identifier}' has {lineCount} lines, max allowed: {_rules.MaxClassLines}",
                    Severity = "Warning",
                    LineNumber = location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = location.GetLineSpan().StartLinePosition.Character + 1,
                    FileName = result.FileName
                });
            }
        }
    }
    
    /// <summary>
    /// Validates method declarations for complexity and length violations
    /// </summary>
    private void ValidateMethods(SyntaxNode root, QualityResult result)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            // Check method length
            var lineCount = GetNodeLineCount(method);
            if (lineCount > _rules.MaxMethodLines)
            {
                var location = method.GetLocation();
                result.MethodLengthViolations.Add(new QualityViolation
                {
                    RuleName = "MaxMethodLines",
                    Message = $"Method '{method.Identifier}' has {lineCount} lines, max allowed: {_rules.MaxMethodLines}",
                    Severity = "Warning",
                    LineNumber = location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = location.GetLineSpan().StartLinePosition.Character + 1,
                    FileName = result.FileName
                });
            }
            
            // Check cyclomatic complexity
            var complexityViolation = _complexityAnalyzer.ValidateMethodComplexity(method);
            if (complexityViolation != null)
            {
                result.ComplexityViolations.Add(complexityViolation);
            }
        }
    }
    
    /// <summary>
    /// Validates XML documentation completeness
    /// </summary>
    private void ValidateDocumentation(SyntaxNode root, QualityResult result)
    {
        var publicMembers = root.DescendantNodes()
            .Where(n => n is ClassDeclarationSyntax or MethodDeclarationSyntax or PropertyDeclarationSyntax)
            .Where(IsPublicMember);
            
        foreach (var member in publicMembers)
        {
            if (!HasXmlDocumentation(member))
            {
                var location = member.GetLocation();
                var memberName = GetMemberName(member);
                
                result.DocumentationViolations.Add(new QualityViolation
                {
                    RuleName = "RequireDocumentation", 
                    Message = $"Public member '{memberName}' missing XML documentation",
                    Severity = "Warning",
                    LineNumber = location.GetLineSpan().StartLinePosition.Line + 1,
                    Column = location.GetLineSpan().StartLinePosition.Character + 1,
                    FileName = result.FileName
                });
            }
        }
    }
    
    /// <summary>
    /// Gets the line count for a syntax node
    /// </summary>
    private static int GetNodeLineCount(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }
    
    /// <summary>
    /// Checks if a member has public accessibility
    /// </summary>
    private static bool IsPublicMember(SyntaxNode node)
    {
        return node switch
        {
            ClassDeclarationSyntax cls => cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)),
            MethodDeclarationSyntax method => method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)),
            PropertyDeclarationSyntax prop => prop.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)),
            _ => false
        };
    }
    
    /// <summary>
    /// Checks if a member has XML documentation
    /// </summary>
    private static bool HasXmlDocumentation(SyntaxNode node)
    {
        var documentationComment = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
                                
        return !documentationComment.IsKind(SyntaxKind.None);
    }
    
    /// <summary>
    /// Gets the display name for a member
    /// </summary>
    private static string GetMemberName(SyntaxNode member)
    {
        return member switch
        {
            ClassDeclarationSyntax cls => cls.Identifier.ValueText,
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            PropertyDeclarationSyntax prop => prop.Identifier.ValueText,
            _ => "Unknown"
        };
    }
}