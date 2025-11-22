# SonarCloud Configuration Guide

## Overview
This guide explains the enhanced SonarCloud setup for strict code quality analysis with full coverage reporting.

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

**Solution**: Created stricter quality settings in `sonar-project.properties` and need to configure custom quality gate in SonarCloud UI (see below).

## Setting Up Strict Quality Gate in SonarCloud

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
- **New Major Issues** > 5 → FAILED (can adjust based on your tolerance)
- **New Code Smells** > 10 → WARNING (can adjust)
- **New Technical Debt Ratio** > 2% → WARNING

### Step 3: Apply Quality Gate to Project
1. Go to **Projects** in SonarCloud
2. Select `autodocprojclone`
3. Click **Project Settings** → **Quality Gate**
4. Select **Enterprise Strict Quality Gate**
5. Click **Save**

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
11. Upload coverage artifacts
```

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

1. **Commit and push** the workflow changes
2. **Create the custom quality gate** in SonarCloud UI
3. **Test with a PR** to verify coverage and decoration work
4. **Review the quality report** and adjust thresholds if needed
5. **Add SonarCloud badge** to README.md:

```markdown
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=akfbtn1-netizen_autodocprojclone&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=akfbtn1-netizen_autodocprojclone)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=akfbtn1-netizen_autodocprojclone&metric=coverage)](https://sonarcloud.io/summary/new_code?id=akfbtn1-netizen_autodocprojclone)
```

## Additional Resources

- [SonarCloud Documentation](https://docs.sonarcloud.io/)
- [Quality Gates Documentation](https://docs.sonarcloud.io/improving/quality-gates/)
- [Coverage & Test Data](https://docs.sonarcloud.io/enriching/test-coverage/overview/)
- [Pull Request Analysis](https://docs.sonarcloud.io/enriching/pull-request-analysis/)
- [SonarLint for Visual Studio](https://www.sonarlint.org/visualstudio)
