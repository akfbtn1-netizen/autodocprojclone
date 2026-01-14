using Enterprise.Documentation.Core.Domain.Entities;
using Core.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Core.Application.Services;

/// <summary>
/// Service for executing Node.js-based document generation templates
/// </summary>
public class NodeJsTemplateExecutor : Core.Application.Interfaces.INodeJsTemplateExecutor
{
    private readonly ILogger<NodeJsTemplateExecutor> _logger;
    private readonly string _nodeJsPath;
    private readonly string _templatesDirectory;

    public NodeJsTemplateExecutor(ILogger<NodeJsTemplateExecutor> logger, IConfiguration configuration)
    {
        _logger = logger;
        _nodeJsPath = configuration["NodeJs:ExecutablePath"] ?? "node";
        _templatesDirectory = configuration["Templates:Directory"] ?? "templates";
    }

    /// <summary>
    /// Executes a Node.js template with the provided data to generate a document
    /// </summary>
    public async Task<DocumentGenerationResult> ExecuteTemplateAsync(
        TemplateExecutionRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing template {TemplateName} for entry {JiraNumber}", 
                request.TemplateName, request.Data.JiraNumber);

            // Validate template exists
            var templatePath = Path.Combine(_templatesDirectory, request.TemplateName);
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template not found: {templatePath}");
            }

            // Prepare execution data
            var executionData = await PrepareExecutionDataAsync(request);
            var tempDataFile = await CreateTempDataFileAsync(executionData);

            try
            {
                // Execute Node.js template
                var result = await ExecuteNodeJsProcessAsync(templatePath, tempDataFile, cancellationToken);
                
                _logger.LogInformation("Successfully executed template {TemplateName}, generated {Size} bytes", 
                    request.TemplateName, result.GeneratedContent?.Length ?? 0);

                return result;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempDataFile))
                {
                    File.Delete(tempDataFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing template {TemplateName}", request.TemplateName);
            throw;
        }
    }

    /// <summary>
    /// Validates that Node.js runtime is available and templates are accessible
    /// </summary>
    public async Task<bool> ValidateEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check Node.js availability
            var nodeVersion = await GetNodeJsVersionAsync(cancellationToken);
            if (string.IsNullOrEmpty(nodeVersion))
            {
                _logger.LogError("Node.js runtime not found at: {NodePath}", _nodeJsPath);
                return false;
            }

            // Check templates directory
            if (!Directory.Exists(_templatesDirectory))
            {
                _logger.LogError("Templates directory not found: {TemplatesDir}", _templatesDirectory);
                return false;
            }

            _logger.LogInformation("Environment validated - Node.js {Version}, Templates: {TemplatesDir}", 
                nodeVersion, _templatesDirectory);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Node.js environment");
            return false;
        }
    }

    /// <summary>
    /// Gets list of available Node.js templates
    /// </summary>
    public async Task<List<string>> GetAvailableTemplatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(_templatesDirectory))
            {
                return new List<string>();
            }

            var jsFiles = Directory.GetFiles(_templatesDirectory, "*.js", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_templatesDirectory, f))
                .ToList();

            _logger.LogInformation("Found {Count} Node.js templates", jsFiles.Count);
            return jsFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available templates");
            return new List<string>();
        }
    }

    private async Task<TemplateExecutionData> PrepareExecutionDataAsync(TemplateExecutionRequest request)
    {
        return new TemplateExecutionData
        {
            Entry = request.Data,
            Metadata = request.Metadata,
            TemplateConfig = request.TemplateConfig,
            GenerationTimestamp = DateTime.UtcNow,
            ExecutionId = Guid.NewGuid().ToString()
        };
    }

    private async Task<string> CreateTempDataFileAsync(TemplateExecutionData data)
    {
        var tempFile = Path.GetTempFileName();
        var jsonData = JsonSerializer.Serialize(data, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(tempFile, jsonData);
        return tempFile;
    }

    private async Task<DocumentGenerationResult> ExecuteNodeJsProcessAsync(
        string templatePath, 
        string dataFilePath, 
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _nodeJsPath,
            Arguments = $"\"{templatePath}\" \"{dataFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        
        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                outputBuilder.AppendLine(args.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                errorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync(cancellationToken);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Node.js template execution failed (Exit code: {process.ExitCode}): {error}");
        }

        // Parse the output as JSON result
        try
        {
            var result = JsonSerializer.Deserialize<NodeJsExecutionOutput>(output, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return new DocumentGenerationResult
            {
                Success = result?.Success ?? false,
                GeneratedContent = result?.Content,
                OutputFilePath = result?.FilePath,
                GenerationTime = TimeSpan.FromMilliseconds(result?.ExecutionTimeMs ?? 0),
                Errors = result?.Errors ?? new List<string>(),
                Warnings = result?.Warnings ?? new List<string>(),
                Metadata = result?.Metadata ?? new Dictionary<string, object>()
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Node.js template output as JSON");
            
            // Fallback to treating output as plain content
            return new DocumentGenerationResult
            {
                Success = !string.IsNullOrWhiteSpace(output),
                GeneratedContent = output,
                GenerationTime = TimeSpan.Zero,
                Errors = string.IsNullOrEmpty(error) ? new List<string>() : new List<string> { error }
            };
        }
    }

    private async Task<string> GetNodeJsVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _nodeJsPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return string.Empty;

            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode == 0)
            {
                var version = await process.StandardOutput.ReadToEndAsync();
                return version.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get Node.js version");
        }

        return string.Empty;
    }

    /// <summary>
    /// Generates a document using the specified template request (alias for ExecuteTemplateAsync)
    /// </summary>
    public async Task<DocumentGenerationResult> GenerateDocumentAsync(TemplateExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteTemplateAsync(request, cancellationToken);
    }
}

// Internal classes used by NodeJsTemplateExecutor

/// <summary>
/// Template execution data passed to Node.js
/// </summary>
internal class TemplateExecutionData
{
    public ExcelChangeEntry Entry { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public Dictionary<string, object> TemplateConfig { get; set; } = new();
    public DateTime GenerationTimestamp { get; set; }
    public string ExecutionId { get; set; } = string.Empty;
}

/// <summary>
/// Node.js template execution output
/// </summary>
internal class NodeJsExecutionOutput
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public long ExecutionTimeMs { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}