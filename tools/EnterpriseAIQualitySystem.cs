// EnterpriseAIQualitySystem.cs
// Complete AI code quality validation system
// Combines: Context-aware validation, Universal context protocol, AI risk management
// 
// Usage:
//   var system = new EnterpriseAIQualitySystem(config);
//   var result = await system.ValidateAndAssess(agentPath);

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace EnterpriseAIQuality
{
    // ============================================================================
    // PART 1: CONTEXT-AWARE CODE VALIDATOR (Your "Qodo")
    // ============================================================================
    
    public class EnterpriseContextValidator
    {
        private readonly CodebaseIndexer _indexer;
        private readonly PatternLearningEngine _learner;
        private readonly DependencyAnalyzer _dependencyAnalyzer;
        
        public EnterpriseContextValidator(string codebasePath, string historyPath)
        {
            _indexer = new CodebaseIndexer(codebasePath);
            _learner = new PatternLearningEngine(historyPath);
            _dependencyAnalyzer = new DependencyAnalyzer(codebasePath);
        }
        
        public async Task<ValidationResult> ValidateAgentCode(
            string agentPath,
            OrganizationalContext orgContext)
        {
            var result = new ValidationResult { AgentPath = agentPath };
            
            // 1. SEMANTIC ANALYSIS
            var tree = await ParseCodeFile(agentPath);
            result.SemanticIssues = AnalyzeSemantics(tree, orgContext);
            
            // 2. PATTERN MATCHING (learns from history)
            result.PatternViolations = await _learner.CheckPatterns(tree, orgContext);
            
            // 3. DEPENDENCY ANALYSIS (multi-repo awareness)
            result.DependencyIssues = await _dependencyAnalyzer.Analyze(agentPath);
            
            // 4. ARCHITECTURAL COMPLIANCE
            result.ArchitectureIssues = ValidateArchitecture(tree, orgContext);
            
            // 5. CALCULATE SCORES
            result.SemanticScore = CalculateScore(result.SemanticIssues);
            result.PatternScore = CalculateScore(result.PatternViolations);
            result.DependencyScore = CalculateScore(result.DependencyIssues);
            result.ArchitectureScore = CalculateScore(result.ArchitectureIssues);
            result.OverallQuality = (result.SemanticScore + result.PatternScore + 
                                     result.DependencyScore + result.ArchitectureScore) / 4.0;
            
            // 6. GENERATE RECOMMENDATIONS
            result.Recommendations = GenerateRecommendations(result);
            
            return result;
        }
        
        private async Task<SyntaxTree> ParseCodeFile(string path)
        {
            var code = await File.ReadAllTextAsync(path);
            return CSharpSyntaxTree.ParseText(code);
        }
        
        private List<CodeIssue> AnalyzeSemantics(SyntaxTree tree, OrganizationalContext context)
        {
            var issues = new List<CodeIssue>();
            var root = tree.GetRoot();
            
            // Check naming conventions
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var cls in classes)
            {
                if (!cls.Identifier.Text.EndsWith("Agent") && 
                    !cls.Identifier.Text.EndsWith("Service"))
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Type = IssueType.NamingConvention,
                        Message = $"Class '{cls.Identifier.Text}' should end with 'Agent' or 'Service'",
                        Line = cls.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            // Check method complexity
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var complexity = CalculateCyclomaticComplexity(method);
                if (complexity > context.MaxComplexity)
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.High,
                        Type = IssueType.Complexity,
                        Message = $"Method '{method.Identifier.Text}' complexity {complexity} exceeds limit {context.MaxComplexity}",
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
                
                var lineCount = method.Body?.Statements.Count ?? 0;
                if (lineCount > context.MaxMethodLines)
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Type = IssueType.MethodLength,
                        Message = $"Method '{method.Identifier.Text}' has {lineCount} lines, exceeds {context.MaxMethodLines}",
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            // Check for magic strings/numbers
            var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>();
            foreach (var literal in literals)
            {
                if (literal.Token.Value is string str && str.Length > 5 && 
                    !IsConfigKey(str) && !IsCommonString(str))
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Low,
                        Type = IssueType.MagicString,
                        Message = $"Magic string detected: '{str}' - consider using constant",
                        Line = literal.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            // Check documentation
            foreach (var cls in classes)
            {
                if (!HasXmlDocumentation(cls))
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Type = IssueType.Documentation,
                        Message = $"Class '{cls.Identifier.Text}' missing XML documentation",
                        Line = cls.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            return issues;
        }
        
        private int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            int complexity = 1; // Base complexity
            
            if (method.Body == null) return complexity;
            
            var conditionals = method.Body.DescendantNodes()
                .Count(n => n.IsKind(SyntaxKind.IfStatement) ||
                           n.IsKind(SyntaxKind.WhileStatement) ||
                           n.IsKind(SyntaxKind.ForStatement) ||
                           n.IsKind(SyntaxKind.ForEachStatement) ||
                           n.IsKind(SyntaxKind.CaseSwitchLabel) ||
                           n.IsKind(SyntaxKind.LogicalAndExpression) ||
                           n.IsKind(SyntaxKind.LogicalOrExpression));
            
            return complexity + conditionals;
        }
        
        private bool HasXmlDocumentation(ClassDeclarationSyntax cls)
        {
            var trivia = cls.GetLeadingTrivia();
            return trivia.Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                  t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }
        
        private bool IsConfigKey(string str) => 
            str.Contains(":") || str.EndsWith("ConnectionString") || str.StartsWith("Azure");
        
        private bool IsCommonString(string str) =>
            new[] { "Error", "Success", "Failed", "Warning", "Info" }.Contains(str);
        
        private List<CodeIssue> ValidateArchitecture(SyntaxTree tree, OrganizationalContext context)
        {
            var issues = new List<CodeIssue>();
            var root = tree.GetRoot();
            
            // Check for required base class inheritance
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var cls in classes)
            {
                if (cls.Identifier.Text.EndsWith("Agent") && cls.BaseList == null)
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.High,
                        Type = IssueType.Architecture,
                        Message = $"Agent class '{cls.Identifier.Text}' must inherit from BaseAgent",
                        Line = cls.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            // Check for dependency injection usage
            var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
            foreach (var ctor in constructors)
            {
                if (ctor.ParameterList.Parameters.Count == 0 && 
                    ctor.Parent is ClassDeclarationSyntax parentClass &&
                    parentClass.Identifier.Text.EndsWith("Agent"))
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Type = IssueType.Architecture,
                        Message = "Agent should use dependency injection (no parameterless constructor)",
                        Line = ctor.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            return issues;
        }
        
        private double CalculateScore(List<CodeIssue> issues)
        {
            if (!issues.Any()) return 100.0;
            
            double penalty = issues.Sum(i => i.Severity switch
            {
                IssueSeverity.Critical => 20.0,
                IssueSeverity.High => 10.0,
                IssueSeverity.Medium => 5.0,
                IssueSeverity.Low => 2.0,
                _ => 1.0
            });
            
            return Math.Max(0, 100.0 - penalty);
        }
        
        private List<string> GenerateRecommendations(ValidationResult result)
        {
            var recommendations = new List<string>();
            
            if (result.OverallQuality < 70)
                recommendations.Add("âš ï¸ Overall quality is below threshold. Recommend significant refactoring.");
            
            if (result.SemanticScore < 80)
                recommendations.Add("ðŸ“ Review naming conventions and code structure");
            
            if (result.PatternScore < 80)
                recommendations.Add("ðŸ” Code doesn't match learned organizational patterns");
            
            if (result.DependencyScore < 80)
                recommendations.Add("ðŸ”— Review dependencies for potential issues");
            
            if (result.ArchitectureScore < 90)
                recommendations.Add("ðŸ—ï¸ Architectural violations detected - review enterprise standards");
            
            var criticalIssues = result.AllIssues().Count(i => i.Severity == IssueSeverity.Critical);
            if (criticalIssues > 0)
                recommendations.Add($"ðŸš¨ {criticalIssues} CRITICAL issues must be fixed before deployment");
            
            return recommendations;
        }
    }
    
    // ============================================================================
    // PATTERN LEARNING ENGINE (Learns from approval history)
    // ============================================================================
    
    public class PatternLearningEngine
    {
        private readonly string _historyPath;
        private readonly List<ApprovalRecord> _history = new();
        
        public PatternLearningEngine(string historyPath)
        {
            _historyPath = historyPath;
            LoadHistory();
        }
        
        private void LoadHistory()
        {
            if (!File.Exists(_historyPath)) return;
            
            var json = File.ReadAllText(_historyPath);
            var records = JsonSerializer.Deserialize<List<ApprovalRecord>>(json);
            if (records != null) _history.AddRange(records);
        }
        
        public async Task<List<CodeIssue>> CheckPatterns(SyntaxTree tree, OrganizationalContext context)
        {
            var issues = new List<CodeIssue>();
            
            // Learn from rejection patterns
            var rejectedPatterns = _history
                .Where(h => !h.Approved)
                .SelectMany(h => h.Issues)
                .GroupBy(i => i.Type)
                .OrderByDescending(g => g.Count())
                .Take(10);
            
            // Check if current code contains frequently rejected patterns
            foreach (var pattern in rejectedPatterns)
            {
                var similarIssues = await DetectSimilarPattern(tree, pattern.Key);
                issues.AddRange(similarIssues);
            }
            
            return issues;
        }
        
        private async Task<List<CodeIssue>> DetectSimilarPattern(SyntaxTree tree, IssueType type)
        {
            // Simplified pattern detection - expand based on your needs
            return new List<CodeIssue>();
        }
        
        public void RecordApproval(string agentPath, bool approved, List<CodeIssue> issues)
        {
            _history.Add(new ApprovalRecord
            {
                AgentPath = agentPath,
                Approved = approved,
                Timestamp = DateTime.UtcNow,
                Issues = issues
            });
            
            SaveHistory();
        }
        
        private void SaveHistory()
        {
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_historyPath, json);
        }
    }
    
    // ============================================================================
    // CODEBASE INDEXER (Multi-repo awareness)
    // ============================================================================
    
    public class CodebaseIndexer
    {
        private readonly string _codebasePath;
        private readonly ConcurrentDictionary<string, CodeFile> _index = new();
        
        public CodebaseIndexer(string codebasePath)
        {
            _codebasePath = codebasePath;
            BuildIndex();
        }
        
        private void BuildIndex()
        {
            var csFiles = Directory.GetFiles(_codebasePath, "*.cs", SearchOption.AllDirectories);
            
            Parallel.ForEach(csFiles, file =>
            {
                var content = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(content);
                
                _index[file] = new CodeFile
                {
                    Path = file,
                    Content = content,
                    SyntaxTree = tree
                };
            });
        }
        
        public CodeFile? GetFile(string path) => _index.TryGetValue(path, out var file) ? file : null;
        
        public IEnumerable<CodeFile> SearchByClass(string className) =>
            _index.Values.Where(f => f.Content.Contains($"class {className}"));
        
        public IEnumerable<CodeFile> SearchByNamespace(string ns) =>
            _index.Values.Where(f => f.Content.Contains($"namespace {ns}"));
    }
    
    // ============================================================================
    // DEPENDENCY ANALYZER (Cross-service analysis)
    // ============================================================================
    
    public class DependencyAnalyzer
    {
        private readonly string _codebasePath;
        private readonly CodebaseIndexer _indexer;
        
        public DependencyAnalyzer(string codebasePath)
        {
            _codebasePath = codebasePath;
            _indexer = new CodebaseIndexer(codebasePath);
        }
        
        public async Task<List<CodeIssue>> Analyze(string agentPath)
        {
            var issues = new List<CodeIssue>();
            var file = _indexer.GetFile(agentPath);
            if (file == null) return issues;
            
            var root = file.SyntaxTree.GetRoot();
            
            // Find all using directives
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
            
            foreach (var usingDirective in usings)
            {
                var ns = usingDirective.Name?.ToString();
                if (ns == null) continue;
                
                // Check for circular dependencies
                var circularDeps = await DetectCircularDependencies(agentPath, ns);
                issues.AddRange(circularDeps);
                
                // Check for deprecated dependencies
                if (IsDeprecatedNamespace(ns))
                {
                    issues.Add(new CodeIssue
                    {
                        Severity = IssueSeverity.High,
                        Type = IssueType.Dependency,
                        Message = $"Using deprecated namespace: {ns}",
                        Line = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line
                    });
                }
            }
            
            return issues;
        }
        
        private async Task<List<CodeIssue>> DetectCircularDependencies(string currentFile, string targetNamespace)
        {
            // Simplified - implement full graph analysis for production
            return new List<CodeIssue>();
        }
        
        private bool IsDeprecatedNamespace(string ns) =>
            ns.Contains("Obsolete") || ns.Contains("Legacy") || ns.Contains("Old");
    }
    
    // ============================================================================
    // PART 2: UNIVERSAL CONTEXT PROTOCOL (Your "MCP")
    // ============================================================================
    
    public interface IContextProvider
    {
        string ProviderName { get; }
        Task<List<ContextResource>> GetAvailableResources();
        Task<object> GetResource(string resourceUri);
        Task<List<ToolDefinition>> GetAvailableTools();
        Task<object> InvokeTool(string toolName, Dictionary<string, object> parameters);
    }
    
    public class EnterpriseContextProtocol
    {
        private readonly List<IContextProvider> _providers = new();
        
        public void RegisterProvider(IContextProvider provider)
        {
            _providers.Add(provider);
        }
        
        public async Task<AgentContext> GatherContext(string query, List<string>? sourceFilter = null)
        {
            var context = new AgentContext();
            
            var relevantProviders = sourceFilter == null 
                ? _providers 
                : _providers.Where(p => sourceFilter.Contains(p.ProviderName));
            
            foreach (var provider in relevantProviders)
            {
                try
                {
                    var resources = await provider.GetAvailableResources();
                    foreach (var resource in resources)
                    {
                        var data = await provider.GetResource(resource.Uri);
                        context.AddResource(provider.ProviderName, resource.Uri, data);
                    }
                }
                catch (Exception ex)
                {
                    context.Errors.Add($"{provider.ProviderName}: {ex.Message}");
                }
            }
            
            return context;
        }
        
        public async Task<object> InvokeTool(string providerName, string toolName, Dictionary<string, object> parameters)
        {
            var provider = _providers.FirstOrDefault(p => p.ProviderName == providerName);
            if (provider == null)
                throw new InvalidOperationException($"Provider '{providerName}' not found");
            
            return await provider.InvokeTool(toolName, parameters);
        }
    }
    
    // Example: MasterIndex Context Provider
    public class MasterIndexContextProvider : IContextProvider
    {
        public string ProviderName => "MasterIndex";
        
        public async Task<List<ContextResource>> GetAvailableResources()
        {
            return new List<ContextResource>
            {
                new ContextResource
                {
                    Uri = "masterindex://semantic-relationships",
                    Name = "Semantic Relationships",
                    Description = "Business entity relationships and context",
                    Type = "application/json"
                },
                new ContextResource
                {
                    Uri = "masterindex://business-rules",
                    Name = "Business Rules",
                    Description = "Organizational business rules and policies",
                    Type = "application/json"
                },
                new ContextResource
                {
                    Uri = "masterindex://naming-conventions",
                    Name = "Naming Conventions",
                    Description = "Standard naming patterns and conventions",
                    Type = "application/json"
                }
            };
        }
        
        public async Task<object> GetResource(string resourceUri)
        {
            // Connect to your actual MasterIndex database/service
            // This is a simplified example
            return resourceUri switch
            {
                "masterindex://semantic-relationships" => await GetSemanticRelationships(),
                "masterindex://business-rules" => await GetBusinessRules(),
                "masterindex://naming-conventions" => await GetNamingConventions(),
                _ => throw new ArgumentException($"Unknown resource: {resourceUri}")
            };
        }
        
        public async Task<List<ToolDefinition>> GetAvailableTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "query_relationships",
                    Description = "Query semantic relationships between entities",
                    Parameters = new Dictionary<string, ParameterDefinition>
                    {
                        ["entity"] = new ParameterDefinition { Type = "string", Required = true },
                        ["relationship_type"] = new ParameterDefinition { Type = "string", Required = false }
                    }
                }
            };
        }
        
        public async Task<object> InvokeTool(string toolName, Dictionary<string, object> parameters)
        {
            return toolName switch
            {
                "query_relationships" => await QueryRelationships(
                    parameters["entity"].ToString() ?? "",
                    parameters.GetValueOrDefault("relationship_type")?.ToString()),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };
        }
        
        private async Task<object> GetSemanticRelationships()
        {
            // Query your MasterIndex for relationships
            return new { /* relationship data */ };
        }
        
        private async Task<object> GetBusinessRules()
        {
            return new { /* business rules */ };
        }
        
        private async Task<object> GetNamingConventions()
        {
            return new
            {
                ClassSuffix = new[] { "Agent", "Service", "Repository" },
                InterfacePrefix = "I",
                PrivateFieldPrefix = "_",
                AsyncMethodSuffix = "Async"
            };
        }
        
        private async Task<object> QueryRelationships(string entity, string? relationshipType)
        {
            // Implement actual query logic
            return new { /* query results */ };
        }
    }
    
    // ============================================================================
    // PART 3: AI RISK MANAGEMENT SYSTEM (Your "NIST AI RMF")
    // ============================================================================
    
    public class AIRiskManagementSystem
    {
        private readonly RiskPolicyConfig _policy;
        private readonly List<RiskEvent> _riskHistory = new();
        
        public AIRiskManagementSystem(RiskPolicyConfig policy)
        {
            _policy = policy;
        }
        
        public async Task<RiskAssessment> AssessAgentRisk(
            string agentName, 
            string agentCode,
            ValidationResult validationResult)
        {
            // GOVERN: Apply organizational policies
            var governance = ApplyGovernancePolicies(agentName);
            
            // MAP: Categorize the AI system
            var category = CategorizeAgent(agentName, agentCode);
            
            // MEASURE: Quantify risks
            var risks = MeasureRisks(agentCode, validationResult, category);
            
            // MANAGE: Determine response
            var response = DetermineResponse(risks, category);
            
            var assessment = new RiskAssessment
            {
                AgentName = agentName,
                Category = category,
                RiskLevel = risks.OverallLevel,
                SecurityRisk = risks.SecurityScore,
                QualityRisk = risks.QualityScore,
                ComplianceRisk = risks.ComplianceScore,
                RequiresHumanApproval = response.RequiresHuman,
                CanAutoDeploy = response.CanAutoDeploy,
                MitigationActions = response.Actions,
                MonitoringRequirements = response.Monitoring,
                Justification = response.Justification
            };
            
            // Record for audit trail
            RecordRiskEvent(assessment);
            
            return assessment;
        }
        
        private GovernanceDecision ApplyGovernancePolicies(string agentName)
        {
            return new GovernanceDecision
            {
                AccountableOfficer = _policy.AccountableOfficer,
                RequiresLegalReview = agentName.Contains("Payment") || agentName.Contains("Financial"),
                RequiresSecurityReview = true,
                AuditRetentionYears = _policy.AuditRetentionYears
            };
        }
        
        private RiskCategory CategorizeAgent(string name, string code)
        {
            // High-risk indicators
            if (ContainsPaymentLogic(code) || 
                ContainsPII(code) ||
                ContainsDatabaseMutations(code) ||
                name.Contains("Payment") ||
                name.Contains("Financial") ||
                name.Contains("Medical"))
            {
                return RiskCategory.High;
            }
            
            // Medium-risk indicators
            if (ContainsDataProcessing(code) ||
                ContainsExternalAPICalls(code) ||
                name.Contains("Integration") ||
                name.Contains("Processor"))
            {
                return RiskCategory.Medium;
            }
            
            // Low-risk (read-only, reporting)
            return RiskCategory.Low;
        }
        
        private RiskMeasurement MeasureRisks(
            string code, 
            ValidationResult validation,
            RiskCategory category)
        {
            var measurement = new RiskMeasurement();
            
            // Security risk based on validation + code analysis
            measurement.SecurityScore = validation.OverallQuality;
            if (ContainsHardcodedSecrets(code))
                measurement.SecurityScore -= 30;
            if (!ContainsInputValidation(code) && category != RiskCategory.Low)
                measurement.SecurityScore -= 20;
            
            // Quality risk from validation
            measurement.QualityScore = validation.OverallQuality;
            
            // Compliance risk
            measurement.ComplianceScore = 100.0;
            if (ContainsPII(code) && !ContainsPIIProtection(code))
                measurement.ComplianceScore -= 40;
            if (ContainsLogging(code) == false)
                measurement.ComplianceScore -= 10;
            
            // Overall risk level
            var avgScore = (measurement.SecurityScore + 
                           measurement.QualityScore + 
                           measurement.ComplianceScore) / 3.0;
            
            measurement.OverallLevel = category switch
            {
                RiskCategory.High => avgScore < 85 ? RiskLevel.Critical : 
                                    avgScore < 95 ? RiskLevel.High : RiskLevel.Medium,
                RiskCategory.Medium => avgScore < 70 ? RiskLevel.High :
                                      avgScore < 85 ? RiskLevel.Medium : RiskLevel.Low,
                RiskCategory.Low => avgScore < 60 ? RiskLevel.Medium : RiskLevel.Low,
                _ => RiskLevel.Unknown
            };
            
            return measurement;
        }
        
        private RiskResponse DetermineResponse(RiskMeasurement risks, RiskCategory category)
        {
            var response = new RiskResponse();
            
            switch (risks.OverallLevel)
            {
                case RiskLevel.Critical:
                    response.RequiresHuman = true;
                    response.CanAutoDeploy = false;
                    response.Actions.Add("ðŸš¨ CRITICAL: Immediate security review required");
                    response.Actions.Add("Block deployment until issues resolved");
                    response.Actions.Add("Notify security team and management");
                    response.Monitoring.Add("Real-time monitoring required");
                    response.Monitoring.Add("Alert on any errors");
                    response.Justification = "Critical security/quality issues detected";
                    break;
                    
                case RiskLevel.High:
                    response.RequiresHuman = true;
                    response.CanAutoDeploy = false;
                    response.Actions.Add("âš ï¸ High-risk: Senior engineer approval required");
                    response.Actions.Add("Security scan before deployment");
                    response.Actions.Add("Additional testing recommended");
                    response.Monitoring.Add("Enhanced monitoring for 30 days");
                    response.Monitoring.Add("Daily health checks");
                    response.Justification = "High-risk agent requires human oversight";
                    break;
                    
                case RiskLevel.Medium:
                    response.RequiresHuman = category == RiskCategory.High;
                    response.CanAutoDeploy = category != RiskCategory.High;
                    response.Actions.Add("âš¡ Medium-risk: Team lead approval recommended");
                    response.Actions.Add("Standard testing and validation");
                    response.Monitoring.Add("Standard monitoring");
                    response.Monitoring.Add("Weekly health checks");
                    response.Justification = category == RiskCategory.High 
                        ? "High-risk category requires approval despite medium risk score"
                        : "Medium risk, can proceed with standard oversight";
                    break;
                    
                case RiskLevel.Low:
                    response.RequiresHuman = false;
                    response.CanAutoDeploy = true;
                    response.Actions.Add("âœ… Low-risk: Automated deployment approved");
                    response.Actions.Add("Standard validation completed");
                    response.Monitoring.Add("Standard monitoring");
                    response.Justification = "Low risk, meets all quality thresholds";
                    break;
            }
            
            return response;
        }
        
        private bool ContainsPaymentLogic(string code) =>
            code.Contains("Payment") || code.Contains("Transaction") || 
            code.Contains("Billing") || code.Contains("Invoice");
        
        private bool ContainsPII(string code) =>
            code.Contains("SSN") || code.Contains("SocialSecurity") ||
            code.Contains("CreditCard") || code.Contains("PersonalData");
        
        private bool ContainsDatabaseMutations(string code) =>
            code.Contains("DELETE") || code.Contains("UPDATE") || 
            code.Contains("INSERT") || code.Contains("DROP");
        
        private bool ContainsDataProcessing(string code) =>
            code.Contains("Process") || code.Contains("Transform") || code.Contains("Parse");
        
        private bool ContainsExternalAPICalls(string code) =>
            code.Contains("HttpClient") || code.Contains("RestClient") || 
            code.Contains("WebRequest");
        
        private bool ContainsHardcodedSecrets(string code) =>
            code.Contains("password=\"") || code.Contains("apikey=\"") ||
            code.Contains("secret=\"") || code.Contains("token=\"");
        
        private bool ContainsInputValidation(string code) =>
            code.Contains("Validate") || code.Contains("FluentValidation") ||
            code.Contains("DataAnnotations");
        
        private bool ContainsPIIProtection(string code) =>
            code.Contains("Encrypt") || code.Contains("Hash") || 
            code.Contains("Mask") || code.Contains("Redact");
        
        private bool ContainsLogging(string code) =>
            code.Contains("ILogger") || code.Contains("Log.") || code.Contains("_logger");
        
        private void RecordRiskEvent(RiskAssessment assessment)
        {
            _riskHistory.Add(new RiskEvent
            {
                Timestamp = DateTime.UtcNow,
                AgentName = assessment.AgentName,
                RiskLevel = assessment.RiskLevel,
                Category = assessment.Category,
                Approved = assessment.CanAutoDeploy || assessment.RequiresHumanApproval
            });
        }
    }
    
    // ============================================================================
    // INTEGRATED SYSTEM (Combines all three parts)
    // ============================================================================
    
    public class EnterpriseAIQualitySystem
    {
        private readonly EnterpriseContextValidator _validator;
        private readonly EnterpriseContextProtocol _contextProtocol;
        private readonly AIRiskManagementSystem _riskSystem;
        
        public EnterpriseAIQualitySystem(SystemConfiguration config)
        {
            _validator = new EnterpriseContextValidator(
                config.CodebasePath,
                config.ApprovalHistoryPath
            );
            
            _contextProtocol = new EnterpriseContextProtocol();
            RegisterDefaultProviders(config);
            
            _riskSystem = new AIRiskManagementSystem(config.RiskPolicy);
        }
        
        private void RegisterDefaultProviders(SystemConfiguration config)
        {
            if (config.EnableMasterIndex)
                _contextProtocol.RegisterProvider(new MasterIndexContextProvider());
            
            // Add other providers as needed
            // _contextProtocol.RegisterProvider(new ExcelContextProvider());
            // _contextProtocol.RegisterProvider(new KeyVaultContextProvider());
        }
        
        public async Task<CompleteAssessment> ValidateAndAssess(string agentPath)
        {
            Console.WriteLine($"ðŸ” Analyzing: {agentPath}");
            
            // 1. Gather organizational context
            Console.WriteLine("ðŸ“š Gathering organizational context...");
            var context = await _contextProtocol.GatherContext("agent-validation");
            
            // 2. Validate code with context
            Console.WriteLine("âœ… Validating code quality...");
            var orgContext = ExtractOrganizationalContext(context);
            var validation = await _validator.ValidateAgentCode(agentPath, orgContext);
            
            // 3. Assess risks
            Console.WriteLine("âš–ï¸ Assessing risks...");
            var code = await File.ReadAllTextAsync(agentPath);
            var agentName = Path.GetFileNameWithoutExtension(agentPath);
            var riskAssessment = await _riskSystem.AssessAgentRisk(agentName, code, validation);
            
            // 4. Make final determination
            var assessment = new CompleteAssessment
            {
                AgentPath = agentPath,
                AgentName = agentName,
                ValidationResult = validation,
                RiskAssessment = riskAssessment,
                Timestamp = DateTime.UtcNow,
                ApprovalRequired = riskAssessment.RequiresHumanApproval,
                CanDeploy = riskAssessment.CanAutoDeploy && validation.OverallQuality >= 70
            };
            
            // 5. Generate summary
            assessment.Summary = GenerateSummary(assessment);
            
            return assessment;
        }
        
        private OrganizationalContext ExtractOrganizationalContext(AgentContext context)
        {
            // Extract from gathered context - simplified for example
            return new OrganizationalContext
            {
                MaxComplexity = 6,
                MaxMethodLines = 20,
                MaxClassLines = 200,
                RequireXmlDocs = true,
                NamingConventions = new Dictionary<string, string>
                {
                    ["Agent"] = "Must end with 'Agent'",
                    ["Service"] = "Must end with 'Service'"
                }
            };
        }
        
        private string GenerateSummary(CompleteAssessment assessment)
        {
            var summary = $@"
â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
â•‘  ENTERPRISE AI QUALITY ASSESSMENT                            â•‘
â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

Agent: {assessment.AgentName}
Assessment Date: {assessment.Timestamp:yyyy-MM-dd HH:mm:ss}

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
CODE QUALITY SCORES
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
Overall Quality:    {assessment.ValidationResult.OverallQuality:F1}/100
  â€¢ Semantic:       {assessment.ValidationResult.SemanticScore:F1}/100
  â€¢ Patterns:       {assessment.ValidationResult.PatternScore:F1}/100
  â€¢ Dependencies:   {assessment.ValidationResult.DependencyScore:F1}/100
  â€¢ Architecture:   {assessment.ValidationResult.ArchitectureScore:F1}/100

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
RISK ASSESSMENT
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
Risk Level:         {assessment.RiskAssessment.RiskLevel}
Risk Category:      {assessment.RiskAssessment.Category}
Security Risk:      {assessment.RiskAssessment.SecurityRisk:F1}/100
Quality Risk:       {assessment.RiskAssessment.QualityRisk:F1}/100
Compliance Risk:    {assessment.RiskAssessment.ComplianceRisk:F1}/100

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
DECISION
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
Human Approval:     {(assessment.ApprovalRequired ? "REQUIRED âš ï¸" : "Not Required âœ…")}
Auto-Deploy:        {(assessment.CanDeploy ? "APPROVED âœ…" : "BLOCKED âŒ")}

Justification: {assessment.RiskAssessment.Justification}

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
RECOMMENDATIONS
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
{string.Join("\n", assessment.ValidationResult.Recommendations)}

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
NEXT ACTIONS
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
{string.Join("\n", assessment.RiskAssessment.MitigationActions)}

â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
MONITORING REQUIREMENTS
â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”â”
{string.Join("\n", assessment.RiskAssessment.MonitoringRequirements)}

â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
";
            return summary;
        }
    }
    
    // ============================================================================
    // DATA MODELS
    // ============================================================================
    
    public class ValidationResult
    {
        public string AgentPath { get; set; } = "";
        public double SemanticScore { get; set; }
        public double PatternScore { get; set; }
        public double DependencyScore { get; set; }
        public double ArchitectureScore { get; set; }
        public double OverallQuality { get; set; }
        public List<CodeIssue> SemanticIssues { get; set; } = new();
        public List<CodeIssue> PatternViolations { get; set; } = new();
        public List<CodeIssue> DependencyIssues { get; set; } = new();
        public List<CodeIssue> ArchitectureIssues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        
        public List<CodeIssue> AllIssues() =>
            SemanticIssues.Concat(PatternViolations)
                         .Concat(DependencyIssues)
                         .Concat(ArchitectureIssues)
                         .ToList();
    }
    
    public class CodeIssue
    {
        public IssueSeverity Severity { get; set; }
        public IssueType Type { get; set; }
        public string Message { get; set; } = "";
        public int Line { get; set; }
    }
    
    public enum IssueSeverity { Critical, High, Medium, Low, Info }
    public enum IssueType { 
        NamingConvention, Complexity, MethodLength, MagicString, 
        Documentation, Architecture, Dependency, Security, Pattern 
    }
    
    public class OrganizationalContext
    {
        public int MaxComplexity { get; set; } = 6;
        public int MaxMethodLines { get; set; } = 20;
        public int MaxClassLines { get; set; } = 200;
        public bool RequireXmlDocs { get; set; } = true;
        public Dictionary<string, string> NamingConventions { get; set; } = new();
    }
    
    public class CodeFile
    {
        public string Path { get; set; } = "";
        public string Content { get; set; } = "";
        public SyntaxTree SyntaxTree { get; set; } = null!;
    }
    
    public class ApprovalRecord
    {
        public string AgentPath { get; set; } = "";
        public bool Approved { get; set; }
        public DateTime Timestamp { get; set; }
        public List<CodeIssue> Issues { get; set; } = new();
    }
    
    public class ContextResource
    {
        public string Uri { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
    }
    
    public class ToolDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, ParameterDefinition> Parameters { get; set; } = new();
    }
    
    public class ParameterDefinition
    {
        public string Type { get; set; } = "";
        public bool Required { get; set; }
    }
    
    public class AgentContext
    {
        private readonly Dictionary<string, Dictionary<string, object>> _resources = new();
        public List<string> Errors { get; set; } = new();
        
        public void AddResource(string provider, string uri, object data)
        {
            if (!_resources.ContainsKey(provider))
                _resources[provider] = new Dictionary<string, object>();
            _resources[provider][uri] = data;
        }
        
        public object? GetResource(string provider, string uri) =>
            _resources.TryGetValue(provider, out var providerResources) &&
            providerResources.TryGetValue(uri, out var data) ? data : null;
    }
    
    public class RiskAssessment
    {
        public string AgentName { get; set; } = "";
        public RiskCategory Category { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public double SecurityRisk { get; set; }
        public double QualityRisk { get; set; }
        public double ComplianceRisk { get; set; }
        public bool RequiresHumanApproval { get; set; }
        public bool CanAutoDeploy { get; set; }
        public List<string> MitigationActions { get; set; } = new();
        public List<string> MonitoringRequirements { get; set; } = new();
        public string Justification { get; set; } = "";
    }
    
    public enum RiskCategory { Low, Medium, High }
    public enum RiskLevel { Unknown, Low, Medium, High, Critical }
    
    public class RiskPolicyConfig
    {
        public string AccountableOfficer { get; set; } = "CTO";
        public int AuditRetentionYears { get; set; } = 7;
        public double SecurityThreshold { get; set; } = 90.0;
        public double QualityThreshold { get; set; } = 85.0;
    }
    
    public class RiskMeasurement
    {
        public double SecurityScore { get; set; }
        public double QualityScore { get; set; }
        public double ComplianceScore { get; set; }
        public RiskLevel OverallLevel { get; set; }
    }
    
    public class RiskResponse
    {
        public bool RequiresHuman { get; set; }
        public bool CanAutoDeploy { get; set; }
        public List<string> Actions { get; set; } = new();
        public List<string> Monitoring { get; set; } = new();
        public string Justification { get; set; } = "";
    }
    
    public class GovernanceDecision
    {
        public string AccountableOfficer { get; set; } = "";
        public bool RequiresLegalReview { get; set; }
        public bool RequiresSecurityReview { get; set; }
        public int AuditRetentionYears { get; set; }
    }
    
    public class RiskEvent
    {
        public DateTime Timestamp { get; set; }
        public string AgentName { get; set; } = "";
        public RiskLevel RiskLevel { get; set; }
        public RiskCategory Category { get; set; }
        public bool Approved { get; set; }
    }
    
    public class CompleteAssessment
    {
        public string AgentPath { get; set; } = "";
        public string AgentName { get; set; } = "";
        public ValidationResult ValidationResult { get; set; } = null!;
        public RiskAssessment RiskAssessment { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public bool ApprovalRequired { get; set; }
        public bool CanDeploy { get; set; }
        public string Summary { get; set; } = "";
    }
    
    public class SystemConfiguration
    {
        public string CodebasePath { get; set; } = "";
        public string ApprovalHistoryPath { get; set; } = "approval-history.json";
        public bool EnableMasterIndex { get; set; } = true;
        public RiskPolicyConfig RiskPolicy { get; set; } = new();
    }
}
