using Enterprise.Documentation.Core.Application.Services.CodeExtraction;
using Enterprise.Documentation.Core.Application.Services.SqlAnalysis;
using Enterprise.Documentation.Core.Application.Services.DraftGeneration;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates; // ✅ ADD THIS
using Enterprise.Documentation.Core.Application.Services.Metadata;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Full Pipeline Test with AI Enhancement ===\n");

// Setup configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("../../src/Api/appsettings.json", optional: true) // Try API appsettings
    .Build();

// Verify Azure OpenAI config
var endpoint = configuration["AzureOpenAI:Endpoint"];
var apiKey = configuration["AzureOpenAI:ApiKey"];

if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("❌ Azure OpenAI not configured in appsettings.json");
    Console.WriteLine("   Add AzureOpenAI section with Endpoint and ApiKey");
    return;
}

Console.WriteLine($"✅ Azure OpenAI configured: {endpoint}");

// Create loggers
using var loggerFactory = LoggerFactory.Create(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var codeLogger = loggerFactory.CreateLogger<CodeExtractionService>();
var sqlLogger = loggerFactory.CreateLogger<SqlAnalysisService>();
var draftLogger = loggerFactory.CreateLogger<DraftGenerationService>();

// Mock services
var mockWorkflow = new MockWorkflowService();
var mockTemplateSelector = new MockTemplateSelector();

// Create services
var codeExtractor = new CodeExtractionService(codeLogger, mockWorkflow, configuration);
var sqlAnalyzer = new SqlAnalysisService();
var metadataLogger = loggerFactory.CreateLogger<MetadataExtractionService>();
var metadataExtractor = new MetadataExtractionService(metadataLogger, configuration);


var persistenceLogger = loggerFactory.CreateLogger<MasterIndexPersistenceService>();
var masterIndexPersistence = new MasterIndexPersistenceService(persistenceLogger, configuration);

var docGenLogger = loggerFactory.CreateLogger<DocumentGenerationService>();
var documentGenerator = new DocumentGenerationService(docGenLogger, configuration);

var draftGenerator = new DraftGenerationService(
    draftLogger, 
    configuration, 
    mockTemplateSelector, 
    sqlAnalyzer,
    metadataExtractor,
    masterIndexPersistence,
    documentGenerator);

Console.WriteLine("\n=== Step 1: Extract Code ===");
var codeResult = await codeExtractor.ExtractMarkedCodeAsync(
    "TEST-001",
    "gwpc.usp_03500_Update_irf_policy",
    "BAS-9818",
    CancellationToken.None);

if (codeResult == null)
{
    Console.WriteLine("❌ Code extraction failed");
    return;
}

Console.WriteLine($"✅ Extracted {codeResult.ExtractedCode.Length} characters");

Console.WriteLine("\n=== Step 2: Create Change Entry ===");
var changeEntry = new DocumentChangeEntry
{
    DocId = "TEST-001",
    JiraNumber = "BAS-9818",
    Description = "Update Lapse Indicator based on policy cancellation status",
    TableName = "gwpcDaily.irf_policy",
    ColumnName = "pol_lapse_ind",
    ChangeType = "Enhancement",
    ModifiedStoredProcedures = "gwpc.usp_03500_Update_irf_policy",
    ReportedBy = "Test User",
    Date = DateTime.Now,
    Priority = "High"
};

Console.WriteLine($"✅ Change entry created for {changeEntry.JiraNumber}");

Console.WriteLine("\n=== Step 3: Generate Draft with AI Enhancement ===");
var draftResult = await draftGenerator.GenerateDraftAsync(
    changeEntry.DocId ?? "TEST",
    changeEntry,
    codeResult,
    null, // No quality result
    CancellationToken.None);

Console.WriteLine($"\n{'=',60}");
Console.WriteLine("RESULTS:");
Console.WriteLine($"{'=',60}");
Console.WriteLine($"Success: {draftResult.Success}");
Console.WriteLine($"Document Type: {draftResult.DocumentType}");
Console.WriteLine($"Template Used: {draftResult.TemplateUsed}");

if (draftResult.Success)
{
    Console.WriteLine($"\n✅ Draft generated successfully!");
    Console.WriteLine($"\nMetadata fields: {draftResult.Metadata.Count}");
    
    // Show key AI-enhanced fields
    if (draftResult.Metadata.ContainsKey("purpose"))
        Console.WriteLine($"\nPurpose: {draftResult.Metadata["purpose"]}");
    
    if (draftResult.Metadata.ContainsKey("business_impact"))
        Console.WriteLine($"\nBusiness Impact: {draftResult.Metadata["business_impact"]}");
    
    if (draftResult.Metadata.ContainsKey("complexity"))
        Console.WriteLine($"\nComplexity: {System.Text.Json.JsonSerializer.Serialize(draftResult.Metadata["complexity"])}");

    // Show extracted MasterIndexMetadata
    if (draftResult.Metadata.ContainsKey("MasterIndexMetadata"))
    {
        var metadata = draftResult.Metadata["MasterIndexMetadata"] as MasterIndexMetadata;
        Console.WriteLine($"\n{'=',60}");
        Console.WriteLine("EXTRACTED METADATA:");
        Console.WriteLine($"{'=',60}");
        Console.WriteLine($"Domain Tags: {string.Join(", ", metadata?.DomainTags ?? new List<string>())}");
        Console.WriteLine($"Business Process: {metadata?.BusinessProcess}");
        Console.WriteLine($"Impact Scope: {metadata?.ImpactScope}");
        Console.WriteLine($"Data Flow: {metadata?.DataFlow}");
        Console.WriteLine($"Keywords: {metadata?.Keywords?.Count ?? 0}");
        Console.WriteLine($"Dependent Tables: {metadata?.DependentTables?.Count ?? 0}");
        Console.WriteLine($"Semantic Embedding: {(metadata?.SemanticEmbedding != null ? "✅ Generated" : "❌ Missing")}");

        // Save metadata to JSON
        var metadataPath = @"C:\Temp\MasterIndex_Metadata.json";
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, 
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metadataPath, metadataJson);
        Console.WriteLine($"\n💾 Saved metadata to: {metadataPath}");
    }

    if (draftResult.Metadata.ContainsKey("GeneratedFilePath"))
    {
        var filePath = draftResult.Metadata["GeneratedFilePath"];
        var fileName = draftResult.Metadata["FileName"];
        var fileSize = draftResult.Metadata["FileSize"];
        Console.WriteLine($"\n📄 Document generated:");
        Console.WriteLine($"   File: {fileName}");
        Console.WriteLine($"   Path: {filePath}");
        Console.WriteLine($"   Size: {fileSize:N0} bytes");
    }

    if (draftResult.Metadata.ContainsKey("IndexID"))
    {
        var indexId = draftResult.Metadata["IndexID"];
        Console.WriteLine($"\n💾 Metadata saved to database with IndexID: {indexId}");
    }
}
else
{
    Console.WriteLine($"\n❌ Draft generation failed: {draftResult.ErrorMessage}");
}

if (draftResult.Success)
{
    Console.WriteLine($"\n✅ Draft generated successfully!");
    
    // The draft content should have the AI-enhanced markdown
    var outputPath = @"C:\Temp\AI_Enhanced_Draft.md";
    File.WriteAllText(outputPath, draftResult.DraftContent ?? "No content");
    Console.WriteLine($"\n💾 Saved draft content to: {outputPath}");
    
    // Also save full result as JSON
    var jsonPath = @"C:\Temp\AI_Enhanced_Result.json";
    var json = System.Text.Json.JsonSerializer.Serialize(draftResult, 
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(jsonPath, json);
    Console.WriteLine($"💾 Saved full result to: {jsonPath}");
}


Console.WriteLine("\n=== Test Complete ===");

// Mock implementations
class MockWorkflowService : IWorkflowEventService
{
    public Task PublishEventAsync(WorkflowEvent workflowEvent, CancellationToken ct = default)
        => Task.CompletedTask;
    
    public Task<List<WorkflowEvent>> GetEventsAsync(int limit = 100, CancellationToken ct = default)
        => Task.FromResult(new List<WorkflowEvent>());
}

class MockTemplateSelector : ITemplateSelector
{
    public Task<IDocumentTemplate?> SelectTemplateAsync(string templateName, CancellationToken ct = default)
        => Task.FromResult<IDocumentTemplate?>(new MockTemplate());
}

class MockTemplate : IDocumentTemplate
{
    public Task<string> GenerateAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        var content = "# AI-Enhanced Documentation\n\n";
        
        // Show all the enhanced fields
        foreach (var kvp in data)
        {
            if (kvp.Value is string strValue)
            {
                content += $"**{kvp.Key}**: {strValue}\n\n";
            }
            else if (kvp.Value is Dictionary<string, string> dict)
            {
                content += $"**{kvp.Key}**:\n";
                foreach (var item in dict)
                    content += $"  - {item.Key}: {item.Value}\n";
                content += "\n";
            }
            else if (kvp.Value is List<string> list)
            {
                content += $"**{kvp.Key}**:\n";
                foreach (var item in list)
                    content += $"  - {item}\n";
                content += "\n";
            }
            else
            {
                content += $"**{kvp.Key}**: {System.Text.Json.JsonSerializer.Serialize(kvp.Value)}\n\n";
            }
        }
        
        return Task.FromResult(content);
    }
}