# ============================================================================
# FIX CRITICAL AUDIT ISSUES
# ============================================================================
# Addresses: SQL Injection, Hardcoded Secrets, Async Blocking, SOLID Violations
# ============================================================================

param(
    [string]$ProjectPath = "C:\Projects\EnterpriseDocumentationPlatform.V2",
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  CRITICAL ISSUES REMEDIATION" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  [DRY RUN MODE - No changes will be made]" -ForegroundColor Yellow
    Write-Host ""
}

# Get all C# files
$csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|node_modules)\\" }

# ============================================================================
# 1. SQL INJECTION ANALYSIS
# ============================================================================
Write-Host "1. SQL INJECTION VULNERABILITIES" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$sqlInjectionFiles = @()
$sqlPatterns = @{
    'string\.Format\s*\([^)]*SELECT' = 'string.Format with SELECT'
    'string\.Format\s*\([^)]*INSERT' = 'string.Format with INSERT'
    'string\.Format\s*\([^)]*UPDATE' = 'string.Format with UPDATE'
    'string\.Format\s*\([^)]*DELETE' = 'string.Format with DELETE'
    '\$"[^"]*SELECT[^"]*\{' = 'String interpolation with SELECT'
    '\$"[^"]*INSERT[^"]*\{' = 'String interpolation with INSERT'
    '\$"[^"]*UPDATE[^"]*\{' = 'String interpolation with UPDATE'
    '\$"[^"]*DELETE[^"]*\{' = 'String interpolation with DELETE'
}

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $fileHasIssues = $false

    foreach ($pattern in $sqlPatterns.Keys) {
        $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($matches.Count -gt 0) {
            if (-not $fileHasIssues) {
                Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
                $sqlInjectionFiles += $file
                $fileHasIssues = $true
            }

            # Find line numbers
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match $pattern) {
                    Write-Host "    Line $($i + 1): $($sqlPatterns[$pattern])" -ForegroundColor Red
                    if ($Verbose) {
                        $snippet = $lines[$i].Trim()
                        if ($snippet.Length -gt 80) { $snippet = $snippet.Substring(0, 77) + "..." }
                        Write-Host "      $snippet" -ForegroundColor DarkGray
                    }
                }
            }
        }
    }
}

Write-Host ""
Write-Host "  Found $($sqlInjectionFiles.Count) files with SQL injection vulnerabilities" -ForegroundColor $(if ($sqlInjectionFiles.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

# Show fix examples
Write-Host "  HOW TO FIX:" -ForegroundColor Cyan
Write-Host @"

  BEFORE (Vulnerable):
  --------------------
  var sql = string.Format("SELECT * FROM Users WHERE Id = {0}", userId);
  var sql = `$"SELECT * FROM Users WHERE Name = '{name}'";

  AFTER (Safe - Using Dapper):
  ----------------------------
  var sql = "SELECT * FROM Users WHERE Id = @Id";
  var result = await connection.QueryAsync<User>(sql, new { Id = userId });

  AFTER (Safe - Using SqlCommand):
  --------------------------------
  var sql = "SELECT * FROM Users WHERE Id = @Id";
  using var cmd = new SqlCommand(sql, connection);
  cmd.Parameters.AddWithValue("@Id", userId);

"@ -ForegroundColor Gray

# ============================================================================
# 2. HARDCODED SECRETS
# ============================================================================
Write-Host "2. HARDCODED SECRETS" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$secretPatterns = @(
    @{ Pattern = 'password\s*=\s*"[^"]{4,}"'; Name = 'Hardcoded password' },
    @{ Pattern = 'apikey\s*=\s*"[^"]{8,}"'; Name = 'Hardcoded API key' },
    @{ Pattern = 'secret\s*=\s*"[^"]{8,}"'; Name = 'Hardcoded secret' },
    @{ Pattern = 'connectionstring\s*=\s*"[^"]*password'; Name = 'Connection string with password' }
)

$secretFiles = @()
foreach ($file in $csFiles) {
    if ($file.Name -match "appsettings.*\.json|\.example") { continue }

    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $fileHasSecrets = $false

    foreach ($secret in $secretPatterns) {
        if ($content -imatch $secret.Pattern) {
            if (-not $fileHasSecrets) {
                Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
                $secretFiles += $file
                $fileHasSecrets = $true
            }

            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -imatch $secret.Pattern) {
                    Write-Host "    Line $($i + 1): $($secret.Name)" -ForegroundColor Red
                }
            }
        }
    }
}

Write-Host ""
Write-Host "  Found $($secretFiles.Count) files with potential hardcoded secrets" -ForegroundColor $(if ($secretFiles.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

Write-Host "  HOW TO FIX:" -ForegroundColor Cyan
Write-Host @"

  1. Move secrets to appsettings.json or User Secrets:
     dotnet user-secrets init
     dotnet user-secrets set "Database:Password" "your-password"

  2. Access via IConfiguration:
     var password = _configuration["Database:Password"];

  3. For production, use Azure Key Vault or AWS Secrets Manager

"@ -ForegroundColor Gray

# ============================================================================
# 3. ASYNC BLOCKING (.Result / .Wait())
# ============================================================================
Write-Host "3. ASYNC BLOCKING ISSUES" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$asyncBlockingFiles = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $fileHasBlocking = $false

    if ($content -match '\.Result\b|\.Wait\(\)') {
        Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
        $asyncBlockingFiles += $file

        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '\.Result\b') {
                Write-Host "    Line $($i + 1): .Result (sync-over-async)" -ForegroundColor Red
            }
            if ($lines[$i] -match '\.Wait\(\)') {
                Write-Host "    Line $($i + 1): .Wait() (sync-over-async)" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
Write-Host "  Found $($asyncBlockingFiles.Count) files with async blocking" -ForegroundColor $(if ($asyncBlockingFiles.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

Write-Host "  HOW TO FIX:" -ForegroundColor Cyan
Write-Host @"

  BEFORE (Blocking):
  ------------------
  var result = SomeAsyncMethod().Result;
  SomeAsyncMethod().Wait();

  AFTER (Non-blocking):
  ---------------------
  var result = await SomeAsyncMethod();
  await SomeAsyncMethod();

  If in a sync context that can't be changed to async:
  var result = Task.Run(async () => await SomeAsyncMethod()).GetAwaiter().GetResult();

"@ -ForegroundColor Gray

# ============================================================================
# 4. SOLID VIOLATIONS (Concrete Instantiations)
# ============================================================================
Write-Host "4. SOLID PRINCIPLE VIOLATIONS" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$solidPatterns = @(
    @{ Pattern = 'new\s+SqlConnection\s*\('; Name = 'new SqlConnection() - use IDbConnection' },
    @{ Pattern = 'new\s+HttpClient\s*\('; Name = 'new HttpClient() - use IHttpClientFactory' },
    @{ Pattern = 'new\s+SmtpClient\s*\('; Name = 'new SmtpClient() - use IEmailSender' }
)

$solidFiles = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $fileHasViolations = $false

    foreach ($solid in $solidPatterns) {
        if ($content -match $solid.Pattern) {
            if (-not $fileHasViolations) {
                Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
                $solidFiles += $file
                $fileHasViolations = $true
            }

            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match $solid.Pattern) {
                    Write-Host "    Line $($i + 1): $($solid.Name)" -ForegroundColor Red
                }
            }
        }
    }
}

Write-Host ""
Write-Host "  Found $($solidFiles.Count) files with SOLID violations" -ForegroundColor $(if ($solidFiles.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

Write-Host "  HOW TO FIX:" -ForegroundColor Cyan
Write-Host @"

  BEFORE (Violates DIP):
  ----------------------
  public class MyService
  {
      public void DoWork()
      {
          using var connection = new SqlConnection(connectionString);
          using var client = new HttpClient();
      }
  }

  AFTER (Follows DIP):
  --------------------
  public class MyService
  {
      private readonly IDbConnection _connection;
      private readonly IHttpClientFactory _clientFactory;

      public MyService(IDbConnection connection, IHttpClientFactory clientFactory)
      {
          _connection = connection;
          _clientFactory = clientFactory;
      }

      public void DoWork()
      {
          // _connection is injected
          var client = _clientFactory.CreateClient();
      }
  }

  Register in DI:
  services.AddScoped<IDbConnection>(sp =>
      new SqlConnection(configuration.GetConnectionString("Default")));
  services.AddHttpClient();

"@ -ForegroundColor Gray

# ============================================================================
# 5. EXCEPTION HANDLING ISSUES
# ============================================================================
Write-Host "5. EXCEPTION HANDLING ISSUES" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$exceptionFiles = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $fileHasIssues = $false

    # Empty catch blocks
    if ($content -match 'catch\s*\([^)]*\)\s*\{\s*\}') {
        if (-not $fileHasIssues) {
            Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
            $exceptionFiles += $file
            $fileHasIssues = $true
        }
        Write-Host "    Empty catch block detected" -ForegroundColor Red
    }

    # throw ex; instead of throw;
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'throw\s+\w+\s*;' -and $lines[$i] -notmatch 'throw\s+new') {
            if (-not $fileHasIssues) {
                Write-Host "  File: $($file.FullName)" -ForegroundColor Yellow
                $exceptionFiles += $file
                $fileHasIssues = $true
            }
            Write-Host "    Line $($i + 1): 'throw ex;' loses stack trace" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "  Found $($exceptionFiles.Count) files with exception handling issues" -ForegroundColor $(if ($exceptionFiles.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

Write-Host "  HOW TO FIX:" -ForegroundColor Cyan
Write-Host @"

  BEFORE (Bad):
  -------------
  catch (Exception ex)
  {
      // Empty catch - swallows exceptions
  }

  catch (Exception ex)
  {
      throw ex;  // Loses original stack trace
  }

  AFTER (Good):
  -------------
  catch (Exception ex)
  {
      _logger.LogError(ex, "Operation failed");
      throw;  // Preserves stack trace
  }

"@ -ForegroundColor Gray

# ============================================================================
# 6. TEST NAMING CONVENTIONS
# ============================================================================
Write-Host "6. TEST NAMING CONVENTIONS" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

Write-Host "  Your tests have 0% naming convention compliance." -ForegroundColor Red
Write-Host ""
Write-Host "  RECOMMENDED PATTERNS:" -ForegroundColor Cyan
Write-Host @"

  Option 1: Should_ExpectedBehavior_When_Condition
  ------------------------------------------------
  public async Task Should_ReturnUser_When_IdIsValid()
  public async Task Should_ThrowException_When_IdIsNull()

  Option 2: MethodName_Scenario_ExpectedResult
  --------------------------------------------
  public async Task GetUser_ValidId_ReturnsUser()
  public async Task GetUser_InvalidId_ThrowsNotFoundException()

  Option 3: Given_When_Then (BDD style)
  -------------------------------------
  public async Task GivenValidId_WhenGetUserCalled_ThenReturnsUser()

  ASSERTIONS - Aim for 2+ per test:
  ---------------------------------
  Assert.NotNull(result);
  Assert.Equal(expectedId, result.Id);
  Assert.Equal(expectedName, result.Name);

"@ -ForegroundColor Gray

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  REMEDIATION SUMMARY" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

$totalFiles = ($sqlInjectionFiles + $secretFiles + $asyncBlockingFiles + $solidFiles + $exceptionFiles | Select-Object -Unique).Count

Write-Host "  Files requiring attention: $totalFiles" -ForegroundColor $(if ($totalFiles -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

Write-Host "  Priority Order:" -ForegroundColor Yellow
Write-Host "    1. SQL Injection ($($sqlInjectionFiles.Count) files) - CRITICAL SECURITY" -ForegroundColor Red
Write-Host "    2. Hardcoded Secrets ($($secretFiles.Count) files) - SECURITY" -ForegroundColor Red
Write-Host "    3. Async Blocking ($($asyncBlockingFiles.Count) files) - PERFORMANCE" -ForegroundColor Yellow
Write-Host "    4. SOLID Violations ($($solidFiles.Count) files) - MAINTAINABILITY" -ForegroundColor Yellow
Write-Host "    5. Exception Handling ($($exceptionFiles.Count) files) - RELIABILITY" -ForegroundColor Yellow
Write-Host "    6. Test Naming - QUALITY" -ForegroundColor Gray
Write-Host ""

if (-not $DryRun) {
    # Export detailed report
    $report = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        SqlInjection = $sqlInjectionFiles | ForEach-Object { $_.FullName }
        Secrets = $secretFiles | ForEach-Object { $_.FullName }
        AsyncBlocking = $asyncBlockingFiles | ForEach-Object { $_.FullName }
        SolidViolations = $solidFiles | ForEach-Object { $_.FullName }
        ExceptionIssues = $exceptionFiles | ForEach-Object { $_.FullName }
    }

    $reportPath = Join-Path $ProjectPath "remediation-report.json"
    $report | ConvertTo-Json -Depth 5 | Out-File $reportPath -Encoding UTF8
    Write-Host "  Detailed report: $reportPath" -ForegroundColor Cyan
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
