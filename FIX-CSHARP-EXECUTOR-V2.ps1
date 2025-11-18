# ============================================================================
# FIX NodeJsTemplateExecutor.cs - Complete File Rewrite (Safe)
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$targetFile = Join-Path $projectRoot "src\Core\Application\Services\NodeJsTemplateExecutor.cs"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  FIXING NodeJsTemplateExecutor.cs (Complete Rewrite)" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Create backup
Write-Host "Creating backup..." -ForegroundColor Yellow
$backupFile = "$targetFile.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $targetFile $backupFile -Force
Write-Host "  Backup: $backupFile" -ForegroundColor Green

Write-Host ""
Write-Host "Writing corrected file..." -ForegroundColor Yellow

# Complete corrected file content
$newContent = @'
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
    /// Templates now accept CLI arguments: node template.js data.json output.docx
    /// </summary>
    public async Task<DocumentGenerationResult> ExecuteTemplateAsync(
        string templateFileName,
        object templateData,
        string outputFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(templateFileName);
        ArgumentNullException.ThrowIfNull(templateData);
        ArgumentNullException.ThrowIfNull(outputFileName);

        var startTime = DateTime.UtcNow;
        var result = new DocumentGenerationResult { Success = false };

        try
        {
            // Step 1: Serialize template data to JSON (UTF-8 without BOM)
            var jsonData = JsonSerializer.Serialize(templateData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var tempDataFile = Path.Combine(_tempOutputPath, $"{Guid.NewGuid()}_data.json");
            await File.WriteAllTextAsync(tempDataFile, jsonData, new System.Text.UTF8Encoding(false), cancellationToken);

            _logger.LogDebug("Template data written to {TempFile}", tempDataFile);

            // Step 2: Build paths
            var templatePath = Path.Combine(_templatesPath, templateFileName);
            var outputPath = Path.Combine(_tempOutputPath, outputFileName);

            if (!File.Exists(templatePath))
            {
                result.ErrorMessage = $"Template not found: {templatePath}";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            // Step 3: Execute Node.js process with CLI arguments
            // New templates accept: node.exe template.js data.json output.docx
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _nodeJsPath,
                Arguments = $"\"{templatePath}\" \"{tempDataFile}\" \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _templatesPath
            };

            _logger.LogInformation(
                "Executing: {Node} {Template} {Data} {Output}",
                _nodeJsPath,
                templateFileName,
                Path.GetFileName(tempDataFile),
                outputFileName);

            using var process = new Process { StartInfo = processStartInfo };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("Node.js: {Output}", e.Data);
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
                result.ErrorMessage = $"Node.js failed (exit code {process.ExitCode}): {errorBuilder}";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            if (!File.Exists(outputPath))
            {
                result.ErrorMessage = $"Output file not generated: {outputPath}\nNode output: {outputBuilder}\nErrors: {errorBuilder}";
                _logger.LogError(result.ErrorMessage);
                return result;
            }

            // Step 5: Success!
            result.Success = true;
            result.DocumentPath = outputPath;
            result.GenerationTime = DateTime.UtcNow - startTime;

            _logger.LogInformation(
                "Document generated: {Path} in {Duration}ms",
                outputPath,
                result.GenerationTime.TotalMilliseconds);

            // Cleanup temp JSON file (keep output)
            try
            {
                File.Delete(tempDataFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp file");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Template execution failed: {ex.Message}";
            _logger.LogError(ex, "Error executing template: {Template}", templateFileName);
            return result;
        }
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Node.js environment");
            return false;
        }
    }
}
'@

# Write the corrected file with UTF-8 without BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($targetFile, $newContent, $utf8NoBom)

Write-Host "  File written successfully" -ForegroundColor Green
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  FILE FIXED!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Changes made:" -ForegroundColor White
Write-Host "  - ExecuteTemplateAsync now calls templates with CLI arguments" -ForegroundColor Gray
Write-Host "  - Removed CreateModifiedTemplateAsync method" -ForegroundColor Gray
Write-Host "  - Fixed: node.exe template.js data.json output.docx" -ForegroundColor Gray
Write-Host ""
Write-Host "Next: Rebuild your solution in Visual Studio" -ForegroundColor Yellow
Write-Host ""
