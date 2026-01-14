# Quality Gates System

## Overview
Enterprise-grade quality gates ensure code quality, security, and maintainability standards are consistently met throughout the development lifecycle.

## Configured Gates

### 1. Code Coverage (Minimum: 80%)
- Line coverage: 80%
- Branch coverage: 75%
- Excludes: Migrations, test files

### 2. Security Scanning
- **BLOCKED**: Critical vulnerabilities
- **BLOCKED**: High vulnerabilities
- **ALLOWED**: Up to 5 medium, 10 low vulnerabilities

### 3. Code Quality
- Maintainability index: â‰¥70
- Cyclomatic complexity: â‰¤15
- Lines per method: â‰¤100

### 4. Build Performance
- Max build time: 5 minutes
- Max test time: 3 minutes
- Max deploy time: 10 minutes

## Local Development

### Pre-Commit Checks
Automatically run before each commit:
```bash
# Runs automatically via Git hook
git commit -m "Your message"
```

### Manual Validation
```powershell
# Run full build validation
./tools/build-validation.ps1

# Run security scan
./tools/security-scan.ps1 -FailOnHigh

# Run comprehensive audit
./tools/comprehensive-audit.ps1
```

## CI/CD Integration

### GitHub Actions
Quality gates are enforced in `.github/workflows/ci-cd-pipeline.yml`:
- Build & test on every PR
- Security scan on every push
- Full audit before deployment

### Pull Request Requirements
1. All tests must pass
2. Code coverage â‰¥80%
3. No critical/high security issues
4. Build completes in <5 min
5. At least 1 approval required

## Overriding Gates

### Emergency Bypass (Requires Approval)
```powershell
# Create override request
./tools/request-quality-override.ps1 -Reason "Emergency hotfix" -Approver "manager@company.com"
```

### Temporary Exemption
Add to `quality-gates-config.json`:
```json
{
  "exemptions": [
    {
      "file": "src/Legacy/OldCode.cs",
      "reason": "Legacy code - scheduled for refactor",
      "expiresOn": "2025-12-31"
    }
  ]
}
```

## Viewing Metrics

### Quality Dashboard
Open `tools/quality-dashboard.html` in your browser for real-time metrics.

### CI/CD Reports
- Build artifacts: Downloadable from GitHub Actions
- Coverage reports: Uploaded to SonarCloud
- Security scans: Available in GitHub Security tab

## Best Practices

1. **Run checks locally** before pushing
2. **Fix issues immediately** - don't let debt accumulate
3. **Monitor trends** - use the quality dashboard
4. **Ask for help** - quality is everyone's responsibility

## Troubleshooting

### "Coverage too low" Error
```powershell
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View detailed report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport
```

### "Security issue detected" Error
```powershell
# Run detailed security scan
./tools/security-scan.ps1 -GenerateReport

# Review findings
cat security-scan-report-*.json
```

## Support
Questions? Contact the DevOps team or file an issue.
