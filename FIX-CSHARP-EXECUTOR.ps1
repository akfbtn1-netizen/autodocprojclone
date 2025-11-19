# ============================================================================
# FIX NodeJsTemplateExecutor.cs - Remove Template Hacks, Use Direct CLI
# ============================================================================
# This script updates your C# file to work with the new CLI-enabled templates
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$targetFile = Join-Path $projectRoot "src\Core\Application\Services\NodeJsTemplateExecutor.cs"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  FIXING NodeJsTemplateExecutor.cs" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Step 1: Verify file exists
Write-Host "[1/5] Checking file exists..." -ForegroundColor Yellow
if (-not (Test-Path $targetFile)) {
    Write-Host "  ERROR: File not found at $targetFile" -ForegroundColor Red
    Write-Host "  Please update the path in this script and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host "  Found: $targetFile" -ForegroundColor Green

# Step 2: Create backup
Write-Host ""
Write-Host "[2/5] Creating backup..." -ForegroundColor Yellow
$backupFile = "$targetFile.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Copy-Item $targetFile $backupFile
Write-Host "  Backup created: $backupFile" -ForegroundColor Green

# Step 3: Read current file
Write-Host ""
Write-Host "[3/5] Reading current file..." -ForegroundColor Yellow
$content = Get-Content $targetFile -Raw
Write-Host "  Current file size: $($content.Length) characters" -ForegroundColor Green

# Step 4: Apply fixes
Write-Host ""
Write-Host "[4/5] Applying fixes..." -ForegroundColor Yellow

# Define the new ExecuteTemplateAsync method
$newExecuteMethod = @'
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
'@

# Use regex to replace the old ExecuteTemplateAsync method
# Match from the method signature to the closing brace
$pattern = '(?s)public async Task<DocumentGenerationResult> ExecuteTemplateAsync\s*\([^)]+\)[^{]*\{.*?\n    \}'
$content = $content -replace $pattern, $newExecuteMethod

Write-Host "  - Replaced ExecuteTemplateAsync method" -ForegroundColor Green

# Remove the CreateModifiedTemplateAsync method entirely
# Match the entire method including comments
$createModifiedPattern = '(?s)\s*/// <summary>.*?Creates a modified template.*?</summary>.*?private async Task<string> CreateModifiedTemplateAsync\([^)]+\)[^{]*\{.*?\n    \}'
$content = $content -replace $createModifiedPattern, ''

Write-Host "  - Removed CreateModifiedTemplateAsync method" -ForegroundColor Green

# Step 5: Write updated file
Write-Host ""
Write-Host "[5/5] Writing updated file..." -ForegroundColor Yellow
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($targetFile, $content, $utf8NoBom)
Write-Host "  File updated successfully" -ForegroundColor Green

# Verify changes
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  CHANGES APPLIED SUCCESSFULLY!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Summary of changes:" -ForegroundColor White
Write-Host "  1. ExecuteTemplateAsync now calls templates directly with CLI arguments" -ForegroundColor Gray
Write-Host "     node.exe template.js data.json output.docx" -ForegroundColor Gray
Write-Host "  2. Removed CreateModifiedTemplateAsync (no longer needed)" -ForegroundColor Gray
Write-Host "  3. Simplified execution - no more template string hacks" -ForegroundColor Gray
Write-Host ""
Write-Host "Backup saved to:" -ForegroundColor Yellow
Write-Host "  $backupFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Rebuild your solution" -ForegroundColor White
Write-Host "  2. Run your document generation service" -ForegroundColor White
Write-Host "  3. Check the temp folder for generated .docx files" -ForegroundColor White
Write-Host ""
Write-Host "If something goes wrong, restore from backup:" -ForegroundColor Yellow
Write-Host "  Copy-Item '$backupFile' '$targetFile' -Force" -ForegroundColor Cyan
Write-Host ""
