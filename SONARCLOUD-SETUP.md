# SonarCloud Configuration Guide

## Overview
This guide explains the enhanced SonarCloud setup for strict code quality analysis with full coverage reporting.

**✅ Works with FREE SonarCloud tier** - This setup uses custom quality enforcement via GitHub Actions, so you don't need a paid SonarCloud plan to enforce strict quality standards.

## What Was Fixed

### 1. Coverage Collection
**Problem**: SonarCloud wasn't receiving any code coverage data.

**Solution**:
- Added test execution with coverage collection in `.github/workflows/build.yml`
- Tests now run with OpenCover format coverage reports
- Coverage reports are uploaded to SonarCloud using `/d:sonar.cs.opencover.reportsPaths`

### 2. Pull Request Analysis
**Problem**: PRs weren't being properly decorated with quality metrics.

**Solution**:
- Added PR-specific parameters to the SonarCloud scanner
- PRs now show inline code quality issues and coverage changes
- Parameters added:
  - `/d:sonar.pullrequest.key` - PR number
  - `/d:sonar.pullrequest.branch` - PR source branch
  - `/d:sonar.pullrequest.base` - PR target branch

### 3. Quality Gate
**Problem**: Using the basic "Sonar way" quality gate (too lenient).

**Solution**: Created custom quality enforcement script (`.github/scripts/enforce-quality-gate.ps1`) that runs after SonarCloud analysis and enforces strict standards via the SonarCloud API. **Works with free tier!**

#### How It Works:
1. SonarCloud analyzes code using the standard "Sonar way" quality gate
2. Our custom PowerShell script fetches metrics from SonarCloud API
3. Script applies YOUR strict thresholds (configurable in the script)
4. Build fails if metrics don't meet your standards
5. No paid SonarCloud plan needed!

## Customizing Quality Standards (Free Tier)

Our custom quality enforcement script (`.github/scripts/enforce-quality-gate.ps1`) enforces these strict standards by default:

### Current Thresholds

**Coverage:**
- Overall code: **≥ 80%** (fails if below)
- New code: **≥ 85%** (fails if below)

**Code Duplication:**
- Overall code: **≤ 3%** duplicated lines (fails if above)
- New code: **≤ 2%** duplicated lines (fails if above)

**Critical Issues:**
- New blocker issues: **0** (fails if any found)
- New critical issues: **0** (fails if any found)
- New major issues: **≤ 5** (fails if more than 5)

**Code Smells:**
- New code smells: **≤ 10** (warning if more than 10)

**Ratings (must be "A"):**
- Maintainability rating
- Reliability rating
- Security rating

### How to Adjust Thresholds

Edit `.github/scripts/enforce-quality-gate.ps1` lines 40-63:

```powershell
$thresholds = @{
    "coverage" = @{
        "overall_min" = 80.0    # Change to 70.0 for more lenient
        "new_code_min" = 85.0   # Change to 75.0 for more lenient
    }
    "duplicated_lines_density" = @{
        "overall_max" = 3.0     # Change to 5.0 for more lenient
        "new_code_max" = 2.0    # Change to 3.0 for more lenient
    }
    "blocker_violations" = @{
        "new_max" = 0           # Keep at 0 - blockers should always fail
    }
    "critical_violations" = @{
        "new_max" = 0           # Keep at 0 or change to 1
    }
    "major_violations" = @{
        "new_max" = 5           # Change to 10 for more lenient
    }
    "code_smells" = @{
        "new_max" = 10          # This is a warning, not failure
    }
    # ... etc
}
```

### Making Quality Gate Stricter

To make quality enforcement even stricter:

```powershell
"coverage" = @{
    "overall_min" = 90.0    # Require 90% coverage
    "new_code_min" = 95.0   # Require 95% on new code
}
"critical_violations" = @{
    "new_max" = 0
}
"major_violations" = @{
    "new_max" = 0           # Zero tolerance for major issues
}
```

### Making Quality Gate More Lenient

If starting with a legacy codebase:

```powershell
"coverage" = @{
    "overall_min" = 60.0    # Start lower
    "new_code_min" = 70.0   # But still require coverage on new code
}
"major_violations" = @{
    "new_max" = 15          # Allow more issues initially
}
```

Then gradually increase thresholds over time as you improve code quality.

## (Optional) SonarCloud UI Quality Gate Setup

**Note:** This section is only for users with PAID SonarCloud plans. Free tier users should use the custom script above.

If you have a paid SonarCloud plan and want to use SonarCloud's native quality gates instead of our custom script:

### Step 1: Access Quality Gates
1. Go to https://sonarcloud.io
2. Navigate to your organization: `akfbtn1-netizen`
3. Click **Quality Gates** in the top menu
4. Click **Create** to create a new quality gate

### Step 2: Create "Enterprise Strict" Quality Gate

Name: **Enterprise Strict Quality Gate**

#### Add These Conditions:

**On Overall Code:**
- **Coverage** < 80% → FAILED
- **Duplicated Lines (%)** > 3% → FAILED
- **Maintainability Rating** worse than A → FAILED
- **Reliability Rating** worse than A → FAILED
- **Security Rating** worse than A → FAILED
- **Security Hotspots Reviewed** < 100% → FAILED

**On New Code (most important for PRs):**
- **Coverage on New Code** < 85% → FAILED
- **Duplicated Lines (%) on New Code** > 2% → FAILED
- **Maintainability Rating on New Code** worse than A → FAILED
- **Reliability Rating on New Code** worse than A → FAILED
- **Security Rating on New Code** worse than A → FAILED
- **New Blocker Issues** > 0 → FAILED
- **New Critical Issues** > 0 → FAILED
- **New Major Issues** > 5 → FAILED

### Step 3: Apply Quality Gate to Project
1. Go to **Projects** in SonarCloud
2. Select `autodocprojclone`
3. Click **Project Settings** → **Quality Gate**
4. Select **Enterprise Strict Quality Gate**
5. Click **Save**

### Step 4: Disable Custom Script (Optional)

If using SonarCloud's native quality gate, you can remove or comment out the "Enforce strict quality gate" step from `.github/workflows/build.yml`

## Understanding the Workflow Changes

### Build.yml Workflow Structure

```yaml
1. Setup JDK 17 (required for SonarScanner)
2. Setup .NET 8.0
3. Checkout code with full history (fetch-depth: 0)
4. Cache SonarCloud packages and scanner
5. Restore NuGet dependencies
6. Begin SonarCloud analysis with parameters
7. Build solution
8. Run unit tests with coverage
9. Run integration tests with coverage
10. End SonarCloud analysis (uploads results)
11. Enforce strict quality gate (custom script) ⭐ NEW
12. Upload coverage artifacts
```

**Step 11 Explained:** This runs our custom PowerShell script that:
- Fetches metrics from SonarCloud API
- Compares them against your strict thresholds
- Fails the build if standards aren't met
- Provides detailed colored output showing what passed/failed

### Key Parameters Explained

| Parameter | Purpose |
|-----------|---------|
| `/d:sonar.cs.opencover.reportsPaths` | Tells SonarCloud where to find coverage XML files |
| `/d:sonar.cs.vstest.reportsPaths` | Tells SonarCloud where to find test result files |
| `/d:sonar.coverage.exclusions` | Don't measure coverage on test files, migrations, etc. |
| `/d:sonar.cpd.exclusions` | Don't check for code duplication in migrations |
| `/d:sonar.exclusions` | Files to completely exclude from analysis |

### Coverage Report Format

Tests now generate **OpenCover XML** format:
```bash
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

This format is more detailed and better supported by SonarCloud than the default Cobertura format.

## Verifying the Setup

### 1. Check Coverage Collection Locally

Run these commands locally to verify coverage works:

```powershell
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" `
  --results-directory ./TestResults `
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Check that coverage files were created
Get-ChildItem -Path ./TestResults -Filter coverage.opencover.xml -Recurse
```

You should see coverage.opencover.xml files in the TestResults directory.

### 2. Check SonarCloud After PR/Push

After pushing changes or creating a PR:

1. Go to **Actions** tab in GitHub
2. Click on the **SonarQube Analysis** workflow run
3. Verify all steps complete successfully:
   - ✅ Begin SonarQube analysis
   - ✅ Build solution
   - ✅ Run unit tests with coverage
   - ✅ Run integration tests with coverage
   - ✅ End SonarQube analysis

4. Go to SonarCloud project page
5. Check the **Coverage** tab - should now show coverage %
6. Check **Pull Requests** - should show quality gate status

### 3. Verify PR Decoration

When you create a PR, you should see:
- ✅ SonarCloud check in PR status checks
- 📊 Coverage change indicators
- 🐛 Inline comments on code issues
- 📈 Quality gate pass/fail status

## Customizing Quality Standards

### Making Quality Gate Even Stricter

Edit these values in the SonarCloud UI quality gate settings:

- **Coverage on New Code**: Increase to 90% or 95%
- **New Major Issues**: Decrease to 3 or 0
- **New Code Smells**: Decrease to 5 or 0
- Add condition: **Lines of Code** on New Code > 500 → Requires extra review

### Making Quality Gate More Lenient

If the strict gate is blocking too many PRs initially:

- **Coverage on New Code**: Decrease to 70% temporarily
- **New Major Issues**: Increase to 10
- **New Code Smells**: Remove or set to 20
- Gradually tighten over time as code quality improves

## Exclusions and Adjustments

### Excluding Specific Files

Edit `sonar-project.properties`:

```properties
# Exclude generated code
sonar.exclusions=\
  **/bin/**,\
  **/obj/**,\
  **/Migrations/**,\
  **/*.Designer.cs,\
  **/GeneratedFiles/**

# Exclude from coverage measurement
sonar.coverage.exclusions=\
  **/*Tests.cs,\
  **/Program.cs,\
  **/Migrations/**
```

### Ignoring Specific Rules

If a SonarCloud rule doesn't apply to your project:

1. Go to **Project Settings** → **Quality Profiles** → **Rules**
2. Search for the rule (e.g., "S1135")
3. Click **Deactivate** or **Change Severity**

Or add to `sonar-project.properties`:

```properties
# Ignore TODOs rule
sonar.issue.ignore.multicriteria=e1
sonar.issue.ignore.multicriteria.e1.ruleKey=csharpsquid:S1135
sonar.issue.ignore.multicriteria.e1.resourceKey=**/*.cs
```

## Troubleshooting

### Coverage Shows 0%

**Causes:**
- Tests aren't running in workflow
- Coverage files not in expected format
- Coverage path pattern doesn't match files

**Solutions:**
1. Check workflow logs to verify tests ran
2. Download coverage artifacts from Actions run
3. Verify coverage.opencover.xml files exist
4. Check file paths match the pattern in sonar config

### Quality Gate Always Failing

**Causes:**
- Quality gate too strict for current codebase
- Legacy code has many issues
- Insufficient test coverage

**Solutions:**
1. Start with "Sonar way" gate temporarily
2. Apply strict gate only to **New Code**
3. Create technical debt reduction plan
4. Gradually increase coverage and fix issues

### PR Decoration Not Working

**Causes:**
- Missing PR parameters in workflow
- SonarCloud GitHub app not installed
- Branch protection rules blocking SonarCloud

**Solutions:**
1. Install SonarCloud GitHub App: https://github.com/apps/sonarcloud
2. Grant repository access to SonarCloud app
3. Verify `/d:sonar.pullrequest.*` parameters in workflow
4. Check SonarCloud project settings → General → Pull Request Decoration

### Scanner Fails with "No coverage report found"

**Causes:**
- Coverage files in wrong location
- Coverage format mismatch
- Tests failed (no coverage generated)

**Solutions:**
1. Ensure tests pass: `dotnet test`
2. Verify OpenCover format is specified
3. Check coverage files exist: `ls **/TestResults/**/coverage.opencover.xml`
4. Adjust path pattern to match actual location

## Best Practices

### 1. Fix Issues on New Code First
- Focus on keeping new code clean
- Don't let quality gate failures block urgent fixes
- Create follow-up tasks for quality improvements

### 2. Monitor Trends
- Check **Activity** tab for quality trends over time
- Set up SonarCloud email notifications for quality gate changes
- Review security hotspots regularly

### 3. Use SonarLint in IDE
- Install SonarLint extension for Visual Studio
- Connect to SonarCloud organization
- Get real-time feedback while coding

### 4. Regular Quality Reviews
- Weekly: Review new security hotspots
- Monthly: Review technical debt trends
- Quarterly: Adjust quality gate based on team capacity

## Next Steps

### For Free Tier Users (Recommended):

1. **✅ Already done** - Workflow changes are committed and pushed
2. **Test with a PR** to verify coverage and custom quality gate work
3. **Review the quality report** in the workflow output
4. **Adjust thresholds** in `.github/scripts/enforce-quality-gate.ps1` if needed
5. **Add SonarCloud badge** to README.md:

```markdown
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=akfbtn1-netizen_autodocprojclone&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=akfbtn1-netizen_autodocprojclone)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=akfbtn1-netizen_autodocprojclone&metric=coverage)](https://sonarcloud.io/summary/new_code?id=akfbtn1-netizen_autodocprojclone)
```

### For Paid Plan Users (Optional):

1. Create custom quality gate in SonarCloud UI (see section above)
2. Apply quality gate to project
3. Optionally disable custom script step in workflow
4. Test with a PR

## Additional Resources

- [SonarCloud Documentation](https://docs.sonarcloud.io/)
- [Quality Gates Documentation](https://docs.sonarcloud.io/improving/quality-gates/)
- [Coverage & Test Data](https://docs.sonarcloud.io/enriching/test-coverage/overview/)
- [Pull Request Analysis](https://docs.sonarcloud.io/enriching/pull-request-analysis/)
- [SonarLint for Visual Studio](https://www.sonarlint.org/visualstudio)
