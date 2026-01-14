#!/usr/bin/env dotnet-script
#r "System.Text.Json"

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Generic;

Console.WriteLine("🔍 Enterprise Documentation Platform V2 - Quality Audit");
Console.WriteLine("=" + new string('=', 60));
Console.WriteLine();

// Configuration
var rootPath = @"c:\Projects\EnterpriseDocumentationPlatform.V2";
var srcPath = Path.Combine(rootPath, "src");

// Audit results
var auditResults = new Dictionary<string, object>();
var totalScore = 0.0;
var maxScore = 0.0;

// Helper functions
string[] GetCSharpFiles(string path) => 
    Directory.Exists(path) ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories) : new string[0];

int CountLines(string filePath) => File.Exists(filePath) ? File.ReadAllLines(filePath).Length : 0;

bool FileExists(string path) => File.Exists(path);

// 1. PROJECT STRUCTURE ANALYSIS (Weight: 15%)
Console.WriteLine("📁 1. PROJECT STRUCTURE ANALYSIS");
Console.WriteLine("-" + new string('-', 40));

var structureScore = 0.0;
var structureWeight = 15.0;

// Check core directories
var expectedDirs = new[] { 
    "src/Api", "src/Core/Domain", "src/Core/Application", "src/Core/Infrastructure", 
    "src/Core/Governance", "src/Shared/Contracts", "tests/Unit", "tests/Integration" 
};

var existingDirs = expectedDirs.Count(dir => Directory.Exists(Path.Combine(rootPath, dir)));
var dirScore = (existingDirs / (double)expectedDirs.Length) * 100;
structureScore += dirScore * 0.4;

Console.WriteLine($"   ✓ Directory Structure: {existingDirs}/{expectedDirs.Length} ({dirScore:F1}%)");

// Check key files
var keyFiles = new[] {
    "src/Api/Program.cs", "src/Api/Api.csproj",
    "src/Core/Domain/Entities/Document.cs", "src/Core/Domain/Entities/User.cs",
    "Directory.Packages.props", "EnterpriseDocumentationPlatform.sln"
};

var existingFiles = keyFiles.Count(file => FileExists(Path.Combine(rootPath, file)));
var fileScore = (existingFiles / (double)keyFiles.Length) * 100;
structureScore += fileScore * 0.6;

Console.WriteLine($"   ✓ Key Files Present: {existingFiles}/{keyFiles.Length} ({fileScore:F1}%)");
Console.WriteLine($"   📊 Structure Score: {structureScore:F1}/100");
Console.WriteLine();

totalScore += structureScore * (structureWeight / 100);
maxScore += structureWeight;

// 2. CODE QUALITY ANALYSIS (Weight: 25%)
Console.WriteLine("⚡ 2. CODE QUALITY ANALYSIS");
Console.WriteLine("-" + new string('-', 40));

var codeScore = 0.0;
var codeWeight = 25.0;

// Count total lines of code
var allCsFiles = GetCSharpFiles(srcPath);
var totalLines = allCsFiles.Sum(CountLines);
var totalFiles = allCsFiles.Length;

Console.WriteLine($"   📈 Total C# Files: {totalFiles}");
Console.WriteLine($"   📈 Total Lines of Code: {totalLines:N0}");

// Basic quality metrics
var qualityScore = 0.0;
if (totalFiles > 0)
{
    var avgLinesPerFile = totalLines / (double)totalFiles;
    var sizeScore = avgLinesPerFile < 500 ? 100 : Math.Max(0, 100 - ((avgLinesPerFile - 500) / 10));
    qualityScore += sizeScore * 0.3;
    Console.WriteLine($"   📊 Average Lines/File: {avgLinesPerFile:F1} (Score: {sizeScore:F1})");
    
    // Check for documentation
    var documentedFiles = 0;
    foreach (var file in allCsFiles.Take(Math.Min(20, allCsFiles.Length))) // Sample check
    {
        var content = File.ReadAllText(file);
        if (content.Contains("/// <summary>") || content.Contains("// <summary>"))
            documentedFiles++;
    }
    
    var docScore = (documentedFiles / (double)Math.Min(20, totalFiles)) * 100;
    qualityScore += docScore * 0.4;
    Console.WriteLine($"   📝 Documentation Coverage: {docScore:F1}% (sampled)");
    
    // Check for error handling
    var errorHandlingFiles = 0;
    foreach (var file in allCsFiles.Take(Math.Min(20, allCsFiles.Length)))
    {
        var content = File.ReadAllText(file);
        if (content.Contains("try") && content.Contains("catch") || content.Contains("throw"))
            errorHandlingFiles++;
    }
    
    var errorScore = (errorHandlingFiles / (double)Math.Min(20, totalFiles)) * 100;
    qualityScore += errorScore * 0.3;
    Console.WriteLine($"   🛡️ Error Handling: {errorScore:F1}% (sampled)");
}

codeScore = qualityScore;
Console.WriteLine($"   📊 Code Quality Score: {codeScore:F1}/100");
Console.WriteLine();

totalScore += codeScore * (codeWeight / 100);
maxScore += codeWeight;

// 3. ARCHITECTURE COMPLIANCE (Weight: 20%)
Console.WriteLine("🏗️ 3. ARCHITECTURE COMPLIANCE");
Console.WriteLine("-" + new string('-', 40));

var archScore = 0.0;
var archWeight = 20.0;

// Check Clean Architecture layers
var domainFiles = GetCSharpFiles(Path.Combine(srcPath, "Core", "Domain")).Length;
var applicationFiles = GetCSharpFiles(Path.Combine(srcPath, "Core", "Application")).Length;
var infrastructureFiles = GetCSharpFiles(Path.Combine(srcPath, "Core", "Infrastructure")).Length;
var apiFiles = GetCSharpFiles(Path.Combine(srcPath, "Api")).Length;

var layerScore = 0.0;
if (domainFiles > 0) layerScore += 25;
if (applicationFiles > 0) layerScore += 25;
if (infrastructureFiles > 0) layerScore += 25;
if (apiFiles > 0) layerScore += 25;

Console.WriteLine($"   🏛️ Domain Layer: {domainFiles} files");
Console.WriteLine($"   🏛️ Application Layer: {applicationFiles} files");
Console.WriteLine($"   🏛️ Infrastructure Layer: {infrastructureFiles} files");
Console.WriteLine($"   🏛️ API Layer: {apiFiles} files");
Console.WriteLine($"   📊 Layer Score: {layerScore:F1}/100");

// Check for CQRS pattern
var cqrsScore = 0.0;
var commandsPath = Path.Combine(srcPath, "Core", "Application", "Commands");
var queriesPath = Path.Combine(srcPath, "Core", "Application", "Queries");

if (Directory.Exists(commandsPath)) cqrsScore += 50;
if (Directory.Exists(queriesPath)) cqrsScore += 50;

Console.WriteLine($"   ⚡ CQRS Implementation: {cqrsScore:F1}/100");

archScore = (layerScore * 0.7) + (cqrsScore * 0.3);
Console.WriteLine($"   📊 Architecture Score: {archScore:F1}/100");
Console.WriteLine();

totalScore += archScore * (archWeight / 100);
maxScore += archWeight;

// 4. DATABASE & PERSISTENCE (Weight: 15%)
Console.WriteLine("💾 4. DATABASE & PERSISTENCE");
Console.WriteLine("-" + new string('-', 40));

var dbScore = 0.0;
var dbWeight = 15.0;

// Check for EF Core setup
var dbContextPath = Path.Combine(srcPath, "Core", "Infrastructure", "Persistence");
var hasDbContext = Directory.Exists(dbContextPath) && GetCSharpFiles(dbContextPath).Any(f => f.Contains("DbContext"));
var hasMigrations = Directory.Exists(Path.Combine(srcPath, "Core", "Infrastructure", "Migrations"));
var hasRepositories = Directory.Exists(Path.Combine(srcPath, "Core", "Infrastructure", "Persistence", "Repositories"));

if (hasDbContext) dbScore += 40;
if (hasMigrations) dbScore += 30;
if (hasRepositories) dbScore += 30;

Console.WriteLine($"   🗄️ DbContext Present: {hasDbContext}");
Console.WriteLine($"   🔄 Migrations Present: {hasMigrations}");
Console.WriteLine($"   📚 Repositories Present: {hasRepositories}");
Console.WriteLine($"   📊 Database Score: {dbScore:F1}/100");
Console.WriteLine();

totalScore += dbScore * (dbWeight / 100);
maxScore += dbWeight;

// 5. API & DOCUMENTATION (Weight: 15%)
Console.WriteLine("🌐 5. API & DOCUMENTATION");
Console.WriteLine("-" + new string('-', 40));

var apiScore = 0.0;
var apiWeight = 15.0;

// Check API setup
var controllersPath = Path.Combine(srcPath, "Api", "Controllers");
var hasControllers = Directory.Exists(controllersPath) && GetCSharpFiles(controllersPath).Length > 0;
var programPath = Path.Combine(srcPath, "Api", "Program.cs");
var hasSwagger = FileExists(programPath) && File.ReadAllText(programPath).Contains("AddSwaggerGen");
var hasAuth = FileExists(programPath) && File.ReadAllText(programPath).Contains("AddAuthentication");

if (hasControllers) apiScore += 40;
if (hasSwagger) apiScore += 30;
if (hasAuth) apiScore += 30;

Console.WriteLine($"   🎮 Controllers Present: {hasControllers}");
Console.WriteLine($"   📖 Swagger Documentation: {hasSwagger}");
Console.WriteLine($"   🔐 Authentication Setup: {hasAuth}");
Console.WriteLine($"   📊 API Score: {apiScore:F1}/100");
Console.WriteLine();

totalScore += apiScore * (apiWeight / 100);
maxScore += apiWeight;

// 6. TESTING & QUALITY ASSURANCE (Weight: 10%)
Console.WriteLine("🧪 6. TESTING & QUALITY ASSURANCE");
Console.WriteLine("-" + new string('-', 40));

var testScore = 0.0;
var testWeight = 10.0;

var unitTestPath = Path.Combine(rootPath, "tests", "Unit");
var integrationTestPath = Path.Combine(rootPath, "tests", "Integration");
var hasUnitTests = Directory.Exists(unitTestPath) && GetCSharpFiles(unitTestPath).Length > 0;
var hasIntegrationTests = Directory.Exists(integrationTestPath) && GetCSharpFiles(integrationTestPath).Length > 0;

if (hasUnitTests) testScore += 50;
if (hasIntegrationTests) testScore += 50;

Console.WriteLine($"   🔬 Unit Tests: {hasUnitTests}");
Console.WriteLine($"   🔗 Integration Tests: {hasIntegrationTests}");
Console.WriteLine($"   📊 Testing Score: {testScore:F1}/100");
Console.WriteLine();

totalScore += testScore * (testWeight / 100);
maxScore += testWeight;

// FINAL AUDIT RESULTS
Console.WriteLine("🎯 AUDIT SUMMARY");
Console.WriteLine("=" + new string('=', 60));

var finalScore = (totalScore / maxScore) * 100;

Console.WriteLine($"📊 Overall Quality Score: {finalScore:F1}/100");
Console.WriteLine();

// Grade calculation
string grade;
string emoji;
if (finalScore >= 90) { grade = "A+"; emoji = "🏆"; }
else if (finalScore >= 80) { grade = "A"; emoji = "🥇"; }
else if (finalScore >= 70) { grade = "B+"; emoji = "🥈"; }
else if (finalScore >= 60) { grade = "B"; emoji = "🥉"; }
else if (finalScore >= 50) { grade = "C"; emoji = "📈"; }
else { grade = "D"; emoji = "⚠️"; }

Console.WriteLine($"{emoji} GRADE: {grade}");
Console.WriteLine();

// Recommendations
Console.WriteLine("💡 RECOMMENDATIONS");
Console.WriteLine("-" + new string('-', 40));

var recommendations = new List<string>();

if (finalScore < 90)
{
    if (testScore < 50) recommendations.Add("• Implement comprehensive unit and integration tests");
    if (codeScore < 80) recommendations.Add("• Improve code documentation and error handling");
    if (archScore < 85) recommendations.Add("• Enhance architectural patterns and CQRS implementation");
    if (dbScore < 90) recommendations.Add("• Complete database migration and repository patterns");
    if (apiScore < 85) recommendations.Add("• Enhance API documentation and authentication");
}

if (recommendations.Count == 0)
{
    Console.WriteLine("✨ Excellent! The system meets enterprise quality standards.");
}
else
{
    foreach (var rec in recommendations)
    {
        Console.WriteLine(rec);
    }
}

Console.WriteLine();
Console.WriteLine($"📋 Audit completed for {totalFiles} files ({totalLines:N0} lines of code)");
Console.WriteLine($"⏰ Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

return (int)finalScore;