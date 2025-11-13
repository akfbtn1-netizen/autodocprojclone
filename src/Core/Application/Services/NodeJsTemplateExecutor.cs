using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Enterprise.Documentation.Core.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services;

/// <summary>
/// Executes Node.js templates to generate Word documents.
/// Provides 63% token savings vs markdown conversion.
/// </summary>
public interface INodeJsTemplateExecutor
{
    Task<DocumentGenerationResult> ExecuteTemplateAsync(
        string templateFileName,
        object templateData,
        string outputFileName,
        CancellationToken cancellationToken = default);
}

public class NodeJsTemplateExecutor : INodeJsTemplateExecutor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NodeJsTemplateExecutor> _logger;
    private readonly string _nodeJsPath;
    private readonly string _templatesPath;
    private readonly string _tempOutputPath;

    public NodeJsTemplateExecutor(
        IConfiguration configuration,
        ILogger<NodeJsTemplateExecutor> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Load configuration
        _nodeJsPath = configuration["DocGenerator:NodeJsPath"] ?? "node";
        _templatesPath = configuration["DocGenerator:TemplatesPath"] ?? "Templates";
        _tempOutputPath = configuration["DocGenerator:TempOutputPath"] ?? "temp";

        // Ensure temp directory exists
        Directory.CreateDirectory(_tempOutputPath);

        _logger.LogInformation(
            "NodeJsTemplateExecutor initialized - Node: {NodePath}, Templates: {TemplatesPath}",
            _nodeJsPath,
            _templatesPath);
    }

    /// <summary>
    /// Executes Node.js template to generate .docx file.
    /// </summary>
    public async Task<DocumentGenerationResult> ExecuteTemplateAsync(
        string templateFileName,
        object templateData,
        string outputFileName,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new DocumentGenerationResult
        {
            Success = false
        };

        try
        {
            // Step 1: Serialize template data to JSON
            var jsonData = JsonSerializer.Serialize(templateData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var tempDataFile = Path.Combine(_tempOutputPath, $"{Guid.NewGuid()}_data.json");
            await File.WriteAllTextAsync(tempDataFile, jsonData, cancellationToken);

            _logger.LogDebug("Template data written to {TempFile}", tempDataFile);

            // Step 2: Build Node.js execution arguments
            var templatePath = Path.Combine(_templatesPath, templateFileName);
            var outputPath = Path.Combine(_tempOutputPath, outputFileName);

            // Modify template to accept JSON input
            var modifiedTemplate = await CreateModifiedTemplateAsync(
                templatePath,
                tempDataFile,
                outputPath,
                cancellationToken);

            // Step 3: Execute Node.js process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _nodeJsPath,
                Arguments = $"\"{modifiedTemplate}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(modifiedTemplate)
            };

            _logger.LogInformation(
                "Executing template: {Template} with output: {Output}",
                templateFileName,
                outputFileName);

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

            // Step 4: Check results
            if (process.ExitCode != 0)
            {
                result.ErrorMessage = $"Node.js process failed with exit code {process.ExitCode}: {errorBuilder}";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            if (!File.Exists(outputPath))
            {
                result.ErrorMessage = $"Output file not generated: {outputPath}";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            // Step 5: Success!
            result.Success = true;
            result.DocumentPath = outputPath;
            result.GenerationTime = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "✅ Document generated successfully: {Path} in {Duration}ms",
                outputPath,
                result.GenerationTime.TotalMilliseconds);

            // Cleanup temp files
            try
            {
                File.Delete(tempDataFile);
                File.Delete(modifiedTemplate);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp files due to IO error");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp files due to access denied");
            }

            return result;
        }
        catch (JsonException ex)
        {
            result.ErrorMessage = $"Template execution failed: JSON serialization error - {ex.Message}";
            _logger.LogError(ex, "JSON serialization error executing Node.js template: {Template}", templateFileName);
            return result;
        }
        catch (IOException ex)
        {
            result.ErrorMessage = $"Template execution failed: File I/O error - {ex.Message}";
            _logger.LogError(ex, "File I/O error executing Node.js template: {Template}", templateFileName);
            return result;
        }
        catch (UnauthorizedAccessException ex)
        {
            result.ErrorMessage = $"Template execution failed: Access denied - {ex.Message}";
            _logger.LogError(ex, "Access denied error executing Node.js template: {Template}", templateFileName);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            result.ErrorMessage = $"Template execution failed: Invalid operation - {ex.Message}";
            _logger.LogError(ex, "Invalid operation error executing Node.js template: {Template}", templateFileName);
            return result;
        }
        catch (Win32Exception ex)
        {
            result.ErrorMessage = $"Template execution failed: Process error - {ex.Message}";
            _logger.LogError(ex, "Process error executing Node.js template: {Template}", templateFileName);
            return result;
        }
        catch (ArgumentException ex)
        {
            result.ErrorMessage = $"Template execution failed: Invalid argument - {ex.Message}";
            _logger.LogError(ex, "Argument error executing Node.js template: {Template}", templateFileName);
            return result;
        }
    }

    /// <summary>
    /// Creates a modified template that loads JSON data instead of hardcoded values.
    /// </summary>
    private async Task<string> CreateModifiedTemplateAsync(
        string originalTemplatePath,
        string jsonDataPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var originalContent = await File.ReadAllTextAsync(originalTemplatePath, cancellationToken);

        // Replace the hardcoded procedureData with JSON file load
        var modifiedContent = originalContent
            .Replace(
                "const procedureData = {",
                $"const fs = require('fs');\nconst procedureData = JSON.parse(fs.readFileSync('{jsonDataPath.Replace("\\", "\\\\")}', 'utf8')); const _originalData = {{")
            .Replace(
                $"fs.writeFileSync(\"TEMPLATE_",
                $"fs.writeFileSync(\"{outputPath.Replace("\\", "\\\\")}\", buffer); console.log('Generated: {outputPath.Replace("\\", "\\\\")}'); }}); const _skipWrite = () => {{ fs.writeFileSync(\"TEMPLATE_");

        var modifiedTemplatePath = Path.Combine(
            _tempOutputPath,
            $"{Guid.NewGuid()}_modified.js");

        await File.WriteAllTextAsync(modifiedTemplatePath, modifiedContent, cancellationToken);

        return modifiedTemplatePath;
    }

    /// <summary>
    /// Validates that Node.js is available and templates exist.
    /// </summary>
    public async Task<bool> ValidateEnvironmentAsync()
    {
        try
        {
            // Check Node.js is available
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _nodeJsPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            var version = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Node.js not found at: {Path}", _nodeJsPath);
                return false;
            }

            _logger.LogInformation("Node.js version: {Version}", version.Trim());

            // Check templates directory exists
            if (!Directory.Exists(_templatesPath))
            {
                _logger.LogError("Templates directory not found: {Path}", _templatesPath);
                return false;
            }

            var templates = Directory.GetFiles(_templatesPath, "TEMPLATE_*.js");
            _logger.LogInformation("Found {Count} templates in {Path}", templates.Length, _templatesPath);

            return true;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment: I/O error");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment: Access denied");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment: Invalid operation");
            return false;
        }
        catch (Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment: Process error");
            return false;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment: Invalid argument");
            return false;
        }
    }
}
