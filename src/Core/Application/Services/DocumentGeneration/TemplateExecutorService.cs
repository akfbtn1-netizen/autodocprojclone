using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Executes Node.js templates to generate Word documents
/// </summary>
public interface ITemplateExecutorService
{
    Task<string> GenerateDocumentAsync(
        TemplateExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public class TemplateExecutionRequest
{
    public required string TemplateType { get; set; }    // "BR", "EN", "DF"
    public required string OutputPath { get; set; }       // Full path to output .docx
    public required object TemplateData { get; set; }     // JSON data for template
}

public class TemplateExecutorService : ITemplateExecutorService
{
    private readonly ILogger<TemplateExecutorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _templatesPath;
    private readonly string _nodeExecutable;

    private static readonly Dictionary<string, string> TemplateFileMap = new()
    {
        { "BR", "TEMPLATE_BusinessRequest.py" },
        { "EN", "TEMPLATE_Enhancement.py" },
        { "DF", "TEMPLATE_DefectFix.py" },
        { "SP", "TEMPLATE_StoredProcedure.py" }
    };

    public TemplateExecutorService(
        ILogger<TemplateExecutorService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _templatesPath = configuration["DocumentGeneration:TemplatesPath"]
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Templates");

        _nodeExecutable = configuration["DocumentGeneration:PythonExecutable"] ?? "python";
    }

    public async Task<string> GenerateDocumentAsync(
        TemplateExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating document using template {TemplateType} to {OutputPath}",
                request.TemplateType, request.OutputPath);

            // Check if we should use OpenXML C# templates or Node.js templates
            var useOpenXmlTemplates = _configuration["DocumentGeneration:UseOpenXmlTemplates"] != "false"; // Default to true
            
            if (useOpenXmlTemplates)
            {
                return await GenerateDocumentWithOpenXmlAsync(request, cancellationToken);
            }
            else
            {
                return await GenerateDocumentWithNodeJsAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Document generation was cancelled for template {TemplateType}", request.TemplateType);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Document generation timed out for template {TemplateType}, trying Node.js fallback", request.TemplateType);
            // Try Node.js fallback if OpenXML times out
            return await GenerateDocumentWithNodeJsAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating document from template {TemplateType}, trying Node.js fallback", request.TemplateType);
            // Try Node.js fallback for any other OpenXML errors
            try
            {
                return await GenerateDocumentWithNodeJsAsync(request, cancellationToken);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Both OpenXML and Node.js generation failed for template {TemplateType}", request.TemplateType);
                throw new AggregateException("Document generation failed with both OpenXML and Node.js methods", ex, fallbackEx);
            }
        }
    }

    private async Task<string> GenerateDocumentWithOpenXmlAsync(
        TemplateExecutionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using OpenXML C# template for {TemplateType}", request.TemplateType);

        // Ensure output directory exists
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            _logger.LogInformation("Created output directory: {OutputDirectory}", outputDirectory);
        }

        // Generate document using OpenXML templates
        try
        {
            using var stream = new FileStream(request.OutputPath, FileMode.Create, FileAccess.ReadWrite);
            
            switch (request.TemplateType.ToUpperInvariant())
            {
                case "BR":
                    var brData = JsonSerializer.Deserialize<BusinessRequestTemplate.BusinessRequestData>(
                        JsonSerializer.Serialize(request.TemplateData));
                    if (brData != null)
                    {
                        BusinessRequestTemplate.Generate(stream, brData);
                    }
                    break;
                    
                case "EN":
                    var enData = JsonSerializer.Deserialize<EnhancementTemplate.EnhancementData>(
                        JsonSerializer.Serialize(request.TemplateData));
                    if (enData != null)
                    {
                        EnhancementTemplate.Generate(stream, enData);
                    }
                    break;
                    
                case "DF":
                    var dfData = JsonSerializer.Deserialize<DefectTemplate.DefectData>(
                        JsonSerializer.Serialize(request.TemplateData));
                    if (dfData != null)
                    {
                        DefectTemplate.Generate(stream, dfData);
                    }
                    break;
                    
                // Note: StoredProcedureTemplate not yet implemented with Generate method
                // case "SP":
                //     var spData = JsonSerializer.Deserialize<StoredProcedureTemplate.StoredProcedureData>(
                //         JsonSerializer.Serialize(request.TemplateData));
                //     if (spData != null)
                //     {
                //         StoredProcedureTemplate.Generate(stream, spData);
                //     }
                //     break;
                    
                default:
                    throw new InvalidOperationException($"Unknown OpenXML template type: {request.TemplateType}");
            }

            // Use synchronous flush to avoid cancellation token issues
            stream.Flush();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OpenXML template generation was cancelled for {TemplateType}", request.TemplateType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OpenXML template generation for {TemplateType}", request.TemplateType);
            throw;
        }
        
        // Verify output file was created
        if (!File.Exists(request.OutputPath))
        {
            throw new FileNotFoundException(
                $"OpenXML template execution completed but output file not found: {request.OutputPath}");
        }

        var fileInfo = new FileInfo(request.OutputPath);
        _logger.LogInformation("Document generated successfully with OpenXML: {OutputPath} ({Size} bytes)",
            request.OutputPath, fileInfo.Length);

        return request.OutputPath;
    }

    private async Task<string> GenerateDocumentWithNodeJsAsync(
        TemplateExecutionRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Using Node.js template for {TemplateType}", request.TemplateType);

        // 1. Get template file path
        if (!TemplateFileMap.TryGetValue(request.TemplateType, out var templateFileName))
        {
            _logger.LogError("Unknown template type: {TemplateType}. Available types: {AvailableTypes}", 
                request.TemplateType, string.Join(", ", TemplateFileMap.Keys));
            throw new InvalidOperationException($"Unknown template type: {request.TemplateType}. Available: {string.Join(", ", TemplateFileMap.Keys)}");
        }

        var templateFilePath = Path.Combine(_templatesPath, templateFileName);

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException($"Template file not found: {templateFilePath}");
        }

        // 2. Create temporary JSON file with template data
        var tempJsonPath = Path.GetTempFileName();
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonContent = JsonSerializer.Serialize(request.TemplateData, jsonOptions);
            await File.WriteAllTextAsync(tempJsonPath, jsonContent, cancellationToken);

            _logger.LogDebug("Created temp JSON file: {TempJsonPath}", tempJsonPath);

            // 3. Ensure output directory exists
            var outputDirectory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                _logger.LogInformation("Created output directory: {OutputDirectory}", outputDirectory);
            }

            // 4. Execute Node.js template
            var arguments = $"\"{templateFilePath}\" \"{tempJsonPath}\" \"{request.OutputPath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _nodeExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _templatesPath
            };

            _logger.LogDebug("Executing: {FileName} {Arguments}", _nodeExecutable, arguments);

            using var process = new Process { StartInfo = processStartInfo };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("Node.js output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogWarning("Node.js error: {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var errorOutput = errorBuilder.ToString();
                throw new InvalidOperationException(
                    $"Node.js template execution failed with exit code {process.ExitCode}. Error: {errorOutput}");
            }

            // 5. Verify output file was created
            if (!File.Exists(request.OutputPath))
            {
                throw new FileNotFoundException(
                    $"Template execution completed but output file not found: {request.OutputPath}");
            }

            var fileInfo = new FileInfo(request.OutputPath);
            _logger.LogInformation("Document generated successfully: {OutputPath} ({Size} bytes)",
                request.OutputPath, fileInfo.Length);

            return request.OutputPath;
        }
        finally
        {
            // Clean up temp JSON file
            if (File.Exists(tempJsonPath))
            {
                try
                {
                    File.Delete(tempJsonPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp JSON file: {TempJsonPath}", tempJsonPath);
                }
            }
        }
    }
}
