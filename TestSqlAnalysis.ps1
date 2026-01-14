# Standalone test script for SqlAnalysisService
# Tests SQL analysis without full pipeline dependencies

Write-Host "=== SQL Analysis Service Test ===" -ForegroundColor Cyan
Write-Host ""

# Test SQL code
$testSql = @"
-- Begin BAS-9999
ALTER PROCEDURE [gwpc].[usp_99999_Update_TestFlag]
(
    @OpenPeriod int,
    @TestValue varchar(10) OUTPUT,
    @Debug bit = 0
)
AS
SET NOCOUNT ON

-- Step 1: Initialize variables
DECLARE @LoadYear int = @OpenPeriod / 100
DECLARE @LoadMonth int = @OpenPeriod % 100

-- Step 2: Create temp table for policies to update
CREATE TABLE #PolicyUpdates (
    policy_nbr varchar(20),
    pol_test_flag varchar(10)
)

-- Step 3: Build staging data
;WITH PolicyCTE AS (
    SELECT policy_nbr, source_policy_id
    FROM gwpc.irf_policy
    WHERE acctg_year_month = @OpenPeriod
)
INSERT INTO #PolicyUpdates
SELECT 
    p.policy_nbr,
    @TestValue
FROM PolicyCTE p
INNER JOIN gwpcDaily.irf_filter_policy f ON f.pol_key = p.source_policy_id
LEFT JOIN gwControl.ctlMonthlyTime t ON t.period = @OpenPeriod
WHERE p.pol_test_flag IS NULL

-- Step 4: Update policy table
UPDATE p
SET pol_test_flag = u.pol_test_flag,
    LastActivityTS = GETDATE()
FROM gwpc.irf_policy p
INNER JOIN #PolicyUpdates u ON u.policy_nbr = p.policy_nbr
WHERE p.acctg_year_month = @OpenPeriod

-- End BAS-9999

-- Log results
EXEC dbo.uspDailyLog @RunID, 'Update', @@ROWCOUNT
"@

# Build and run test
$projectPath = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$testCode = @"
using System;
using System.Linq;
using Enterprise.Documentation.Core.Application.Services.SqlAnalysis;

var service = new SqlAnalysisService();
var testSql = @`"$testSql`";

Console.WriteLine("Analyzing SQL...\n");
var result = service.AnalyzeSql(testSql);

Console.WriteLine("=== SCHEMA & PROCEDURE ===");
Console.WriteLine(`$"Schema: {result.Schema}");
Console.WriteLine(`$"Procedure: {result.ProcedureName}\n");

Console.WriteLine("=== PARAMETERS ({result.Parameters.Count}) ===");
foreach (var p in result.Parameters)
{
    Console.WriteLine(`$"{p.Name} {p.Type} ({p.Direction})");
}
Console.WriteLine();

Console.WriteLine("=== DEPENDENCIES ===");
Console.WriteLine(`$"Tables: {string.Join(", ", result.Dependencies.Tables)}");
Console.WriteLine(`$"Procedures: {string.Join(", ", result.Dependencies.Procedures)}");
Console.WriteLine(`$"Temp Tables: {string.Join(", ", result.Dependencies.TempTables)}");
Console.WriteLine(`$"Control Tables: {string.Join(", ", result.Dependencies.ControlTables)}\n");

Console.WriteLine("=== COMPLEXITY ===");
Console.WriteLine(`$"Lines: {result.Complexity.LineCount}");
Console.WriteLine(`$"Temp Tables: {result.Complexity.TempTableCount}");
Console.WriteLine(`$"CTEs: {result.Complexity.CteCount}");
Console.WriteLine(`$"Joins: {result.Complexity.JoinCount}");
Console.WriteLine(`$"Level: {result.Complexity.ComplexityLevel}\n");

Console.WriteLine("=== LOGIC STEPS ({result.LogicSteps.Count}) ===");
foreach (var step in result.LogicSteps)
{
    Console.WriteLine(`$"• {step}");
}
Console.WriteLine();

Console.WriteLine("=== VALIDATION RULES ({result.ValidationRules.Count}) ===");
foreach (var rule in result.ValidationRules)
{
    Console.WriteLine(`$"• {rule.RuleText}");
}
Console.WriteLine();

if (result.BracketedChange != null)
{
    Console.WriteLine("=== BRACKETED CHANGE ===");
    Console.WriteLine(`$"Ticket: {result.BracketedChange.Ticket}");
    Console.WriteLine(`$"Lines: {result.BracketedChange.StartLine}-{result.BracketedChange.EndLine}");
    Console.WriteLine(`$"Code Length: {result.BracketedChange.Code.Length} chars\n");
}
"@

# Save test code
$testFile = Join-Path $projectPath "TestSqlAnalysisRunner.cs"
Set-Content -Path $testFile -Value $testCode

Write-Host "Running analysis..." -ForegroundColor Yellow
Write-Host ""

# Run using dotnet-script or compile inline
try {
    # Try dotnet-script first (if installed)
    dotnet script $testFile 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-script not available"
    }
} catch {
    Write-Host "dotnet-script not found, using inline compilation..." -ForegroundColor Yellow
    
    # Fallback: Create temp console project
    $tempDir = Join-Path $env:TEMP "SqlAnalysisTest"
    if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    
    # Create project reference
    dotnet new console -o $tempDir -n SqlAnalysisTest | Out-Null
    
    # Add reference to Core project
    $coreProject = Join-Path $projectPath "src\Core\Application\Application.csproj"
    dotnet add "$tempDir\SqlAnalysisTest.csproj" reference $coreProject | Out-Null
    
    # Replace Program.cs with test code
    Set-Content -Path "$tempDir\Program.cs" -Value $testCode
    
    # Build and run
    Push-Location $tempDir
    dotnet build -v q
    dotnet run
    Pop-Location
    
    # Cleanup
    Remove-Item $tempDir -Recurse -Force
}

Remove-Item $testFile -Force

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Green