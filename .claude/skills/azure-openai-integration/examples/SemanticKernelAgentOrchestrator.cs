using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.ComponentModel;

namespace Enterprise.AzureOpenAI.Agents;

/// <summary>
/// Multi-agent orchestration using Semantic Kernel for document analysis workflows
/// </summary>
public class DocumentAnalysisAgentOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<DocumentAnalysisAgentOrchestrator> _logger;

    public DocumentAnalysisAgentOrchestrator(
        Kernel kernel,
        ILogger<DocumentAnalysisAgentOrchestrator> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Create specialized agents for document analysis workflow
    /// </summary>
    public async Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        string documentContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document analysis with multi-agent workflow");

        // Create specialized agents
        var extractorAgent = CreateExtractorAgent();
        var analyzerAgent = CreateAnalyzerAgent();
        var writerAgent = CreateWriterAgent();

        // Create group chat with termination strategy
        var chat = new AgentGroupChat(extractorAgent, analyzerAgent, writerAgent)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                TerminationStrategy = new ApprovalTerminationStrategy
                {
                    Agents = { writerAgent },
                    MaximumIterations = 10
                },
                SelectionStrategy = new SequentialSelectionStrategy()
            }
        };

        // Add initial document for analysis
        await chat.AddChatMessageAsync(new ChatMessageContent(
            AuthorRole.User,
            $"""
            Analyze the following document and create a comprehensive summary:
            
            <document>
            {documentContent}
            </document>
            
            The workflow should:
            1. DataExtractor: Extract key facts, entities, and data points
            2. DataAnalyzer: Analyze patterns, insights, and recommendations
            3. ReportWriter: Create a professional executive summary
            
            When complete, ReportWriter should output "ANALYSIS COMPLETE" at the end.
            """
        ));

        var messages = new List<ChatMessageContent>();
        string? finalReport = null;

        await foreach (var message in chat.InvokeAsync(cancellationToken))
        {
            _logger.LogDebug("Agent {Name}: {Content}", message.AuthorName, message.Content);
            messages.Add(message);

            if (message.AuthorName == "ReportWriter" && 
                message.Content?.Contains("ANALYSIS COMPLETE") == true)
            {
                finalReport = message.Content.Replace("ANALYSIS COMPLETE", "").Trim();
            }
        }

        return new DocumentAnalysisResult
        {
            Report = finalReport ?? messages.LastOrDefault()?.Content ?? "Analysis failed",
            AgentMessages = messages.Select(m => new AgentMessage
            {
                Agent = m.AuthorName ?? "Unknown",
                Content = m.Content ?? string.Empty
            }).ToList()
        };
    }

    private ChatCompletionAgent CreateExtractorAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DataExtractor",
            Instructions = """
                You are a Data Extraction Specialist. Your role is to:
                
                1. Extract all key facts and figures from documents
                2. Identify named entities (people, organizations, locations, dates)
                3. Extract numerical data, metrics, and KPIs
                4. Identify relationships between entities
                5. Note any uncertainties or ambiguities
                
                Format your output as structured data with clear categories.
                Be thorough but concise. Focus on factual extraction, not interpretation.
                """,
            Kernel = _kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.1f,
                MaxTokens = 2000
            })
        };
    }

    private ChatCompletionAgent CreateAnalyzerAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DataAnalyzer",
            Instructions = """
                You are a Data Analysis Expert. Building on the extracted data, you:
                
                1. Identify patterns and trends in the data
                2. Draw insights from relationships between entities
                3. Compare metrics against industry benchmarks when possible
                4. Identify risks, opportunities, and areas of concern
                5. Formulate actionable recommendations
                
                Your analysis should be data-driven and evidence-based.
                Clearly distinguish between facts and interpretations.
                """,
            Kernel = _kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.3f,
                MaxTokens = 2000
            })
        };
    }

    private ChatCompletionAgent CreateWriterAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "ReportWriter",
            Instructions = """
                You are a Professional Report Writer. Using the extracted data and analysis, you:
                
                1. Create a clear, executive-level summary
                2. Structure information in a logical, readable format
                3. Highlight key findings and recommendations
                4. Use professional business language
                5. Include a brief methodology section
                
                Format:
                - Executive Summary (2-3 sentences)
                - Key Findings (bullet points)
                - Analysis Highlights
                - Recommendations
                - Conclusion
                
                When satisfied with the report, end with "ANALYSIS COMPLETE"
                """,
            Kernel = _kernel,
            Arguments = new KernelArguments(new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.5f,
                MaxTokens = 3000
            })
        };
    }
}

/// <summary>
/// Custom termination strategy that ends when specific agent approves
/// </summary>
public class ApprovalTerminationStrategy : TerminationStrategy
{
    public List<ChatCompletionAgent> Agents { get; } = new();

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();

        if (lastMessage == null) return Task.FromResult(false);

        // Terminate when the approving agent says complete
        var isApprover = Agents.Any(a => a.Name == lastMessage.AuthorName);
        var isComplete = lastMessage.Content?.Contains("ANALYSIS COMPLETE") == true;

        return Task.FromResult(isApprover && isComplete);
    }
}

#region Plugin Examples

/// <summary>
/// Database plugin for Semantic Kernel function calling
/// </summary>
public class DatabasePlugin
{
    private readonly IDbContext _context;
    private readonly ILogger<DatabasePlugin> _logger;

    public DatabasePlugin(IDbContext context, ILogger<DatabasePlugin> logger)
    {
        _context = context;
        _logger = logger;
    }

    [KernelFunction("search_stored_procedures")]
    [Description("Search for stored procedures by name or description")]
    public async Task<List<StoredProcedureInfo>> SearchStoredProceduresAsync(
        [Description("Search query to match procedure names or descriptions")] string query,
        [Description("Maximum number of results to return")] int limit = 10)
    {
        _logger.LogInformation("Searching stored procedures for: {Query}", query);

        return await _context.StoredProcedures
            .Where(sp => sp.Name.Contains(query) || sp.Description.Contains(query))
            .OrderBy(sp => sp.Name)
            .Take(limit)
            .Select(sp => new StoredProcedureInfo
            {
                Name = sp.Name,
                Schema = sp.Schema,
                Description = sp.Description,
                LastModified = sp.LastModified
            })
            .ToListAsync();
    }

    [KernelFunction("get_procedure_details")]
    [Description("Get detailed information about a stored procedure including parameters")]
    public async Task<StoredProcedureDetails?> GetProcedureDetailsAsync(
        [Description("Schema name of the procedure")] string schema,
        [Description("Name of the stored procedure")] string name)
    {
        _logger.LogInformation("Getting details for {Schema}.{Name}", schema, name);

        var procedure = await _context.StoredProcedures
            .Include(sp => sp.Parameters)
            .FirstOrDefaultAsync(sp => sp.Schema == schema && sp.Name == name);

        if (procedure == null) return null;

        return new StoredProcedureDetails
        {
            Name = procedure.Name,
            Schema = procedure.Schema,
            Description = procedure.Description,
            Definition = procedure.Definition,
            Parameters = procedure.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                DataType = p.DataType,
                Direction = p.Direction,
                DefaultValue = p.DefaultValue
            }).ToList()
        };
    }

    [KernelFunction("get_table_schema")]
    [Description("Get schema information for a database table")]
    public async Task<TableSchemaInfo?> GetTableSchemaAsync(
        [Description("Schema name")] string schema,
        [Description("Table name")] string tableName)
    {
        _logger.LogInformation("Getting schema for {Schema}.{Table}", schema, tableName);

        var table = await _context.Tables
            .Include(t => t.Columns)
            .Include(t => t.Indexes)
            .FirstOrDefaultAsync(t => t.Schema == schema && t.Name == tableName);

        if (table == null) return null;

        return new TableSchemaInfo
        {
            Name = table.Name,
            Schema = table.Schema,
            Description = table.Description,
            Columns = table.Columns.Select(c => new ColumnInfo
            {
                Name = c.Name,
                DataType = c.DataType,
                IsNullable = c.IsNullable,
                IsPrimaryKey = c.IsPrimaryKey,
                Description = c.Description
            }).ToList()
        };
    }
}

#endregion

#region DI Configuration

public static class SemanticKernelServiceExtensions
{
    public static IServiceCollection AddSemanticKernelAgents(
        this IServiceCollection services,
        Action<SemanticKernelOptions> configure)
    {
        var options = new SemanticKernelOptions();
        configure(options);

        services.AddSingleton(sp =>
        {
            var builder = Kernel.CreateBuilder();

            builder.AddAzureOpenAIChatCompletion(
                deploymentName: options.DeploymentName,
                endpoint: options.Endpoint,
                credentials: new DefaultAzureCredential()
            );

            // Add logging
            builder.Services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            });

            var kernel = builder.Build();

            // Register plugins
            if (options.RegisterDatabasePlugin)
            {
                var dbContext = sp.GetRequiredService<IDbContext>();
                var logger = sp.GetRequiredService<ILogger<DatabasePlugin>>();
                kernel.Plugins.AddFromObject(new DatabasePlugin(dbContext, logger), "Database");
            }

            return kernel;
        });

        services.AddScoped<DocumentAnalysisAgentOrchestrator>();

        return services;
    }
}

public class SemanticKernelOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-5";
    public bool RegisterDatabasePlugin { get; set; } = true;
}

#endregion

#region Models

public class DocumentAnalysisResult
{
    public string Report { get; set; } = string.Empty;
    public List<AgentMessage> AgentMessages { get; set; } = new();
}

public class AgentMessage
{
    public string Agent { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class StoredProcedureInfo
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class StoredProcedureDetails : StoredProcedureInfo
{
    public string Definition { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Direction { get; set; } = "IN";
    public string? DefaultValue { get; set; }
}

public class TableSchemaInfo
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? Description { get; set; }
}

// Placeholder interfaces for the example
public interface IDbContext
{
    DbSet<StoredProcedure> StoredProcedures { get; }
    DbSet<Table> Tables { get; }
}

public class StoredProcedure
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public List<Parameter> Parameters { get; set; } = new();
}

public class Parameter
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Direction { get; set; } = "IN";
    public string? DefaultValue { get; set; }
}

public class Table
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<Column> Columns { get; set; } = new();
    public List<Index> Indexes { get; set; } = new();
}

public class Column
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? Description { get; set; }
}

public class Index
{
    public string Name { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public List<string> Columns { get; set; } = new();
}

// EF Core placeholder
public class DbSet<T> where T : class
{
    public IQueryable<T> Where(Func<T, bool> predicate) => throw new NotImplementedException();
    public IQueryable<T> Include<TProperty>(Func<T, TProperty> path) => throw new NotImplementedException();
}

public static class QueryableExtensions
{
    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query) => throw new NotImplementedException();
    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> query, Func<T, bool> predicate) => throw new NotImplementedException();
}

#endregion
