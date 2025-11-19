# SonarCloud Setup Guide

## Overview

SonarCloud provides comprehensive code analysis including:
- Code smells and bugs
- Security vulnerabilities
- Code coverage tracking
- Duplicate code detection
- Technical debt estimation
- Quality gates

## Setup Steps

### 1. Create SonarCloud Account

1. Go to [https://sonarcloud.io](https://sonarcloud.io)
2. Sign up with your GitHub account
3. Import your organization

### 2. Create Project in SonarCloud

1. Click **"+"** → **"Analyze new project"**
2. Select your repository: `EnterpriseDocumentationPlatform.V2`
3. Choose **"GitHub Actions"** as the analysis method
4. Note your:
   - **Organization key** (e.g., `your-org`)
   - **Project key** (e.g., `EnterpriseDocumentationPlatform`)

### 3. Configure GitHub Secrets

In your GitHub repository settings, add these secrets:

| Secret Name | Description | Where to find |
|-------------|-------------|---------------|
| `SONAR_TOKEN` | Authentication token | SonarCloud → My Account → Security → Generate Token |

Add this variable:

| Variable Name | Description | Value |
|---------------|-------------|-------|
| `SONAR_ORGANIZATION` | Your org key | From SonarCloud dashboard |

**Steps:**
1. GitHub repo → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add `SONAR_TOKEN` with the token from SonarCloud

### 4. Update Configuration Files

Edit `sonar-project.properties`:

```properties
sonar.projectKey=YOUR_PROJECT_KEY
sonar.organization=YOUR_ORGANIZATION_KEY
```

### 5. Run Analysis

The analysis runs automatically on:
- Push to `main` or `develop`
- Pull request opened/updated

To run manually:
```bash
# Install tools
dotnet tool install --global dotnet-sonarscanner

# Run analysis
dotnet sonarscanner begin /k:"EnterpriseDocumentationPlatform" /o:"your-org" /d:sonar.token="YOUR_TOKEN" /d:sonar.host.url="https://sonarcloud.io"
dotnet build
dotnet test --collect:"XPlat Code Coverage"
dotnet sonarscanner end /d:sonar.token="YOUR_TOKEN"
```

## Quality Gate Configuration

### Default Quality Gate

SonarCloud's default "Sonar way" quality gate requires:
- **Coverage** on new code ≥ 80%
- **Duplicated Lines** on new code ≤ 3%
- **Maintainability Rating** is A
- **Reliability Rating** is A
- **Security Rating** is A

### Custom Quality Gate (Recommended)

Create a custom gate in SonarCloud → Quality Gates:

| Metric | Operator | Value |
|--------|----------|-------|
| Coverage on New Code | is less than | 70% |
| Duplicated Lines on New Code | is greater than | 5% |
| Security Hotspots Reviewed | is less than | 100% |
| Maintainability Rating | is worse than | B |
| Reliability Rating | is worse than | B |
| Security Rating | is worse than | B |

## Viewing Results

### Dashboard

Access your project dashboard at:
```
https://sonarcloud.io/project/overview?id=EnterpriseDocumentationPlatform
```

### Key Sections

1. **Overview** - Summary metrics, quality gate status
2. **Issues** - All detected problems (bugs, vulnerabilities, code smells)
3. **Security Hotspots** - Code needing security review
4. **Measures** - Detailed metrics (complexity, coverage, duplications)
5. **Code** - Browse analyzed code with inline issues
6. **Activity** - Historical trends

### Pull Request Decoration

SonarCloud automatically adds analysis results to PRs:
- Quality gate status
- New issues introduced
- Coverage changes
- PR comments for specific issues

## Common Issues

### Analysis Fails

1. **Missing SONAR_TOKEN**: Check GitHub secrets
2. **Wrong project key**: Verify in sonar-project.properties
3. **Build failures**: Ensure project builds before analysis

### No Coverage Data

1. Install coverage package:
   ```bash
   dotnet add package coverlet.collector
   ```

2. Run tests with coverage:
   ```bash
   dotnet test --collect:"XPlat Code Coverage"
   ```

### Exclusions Not Working

Check `sonar-project.properties`:
```properties
sonar.exclusions=**/bin/**,**/obj/**
sonar.coverage.exclusions=**/tests/**
```

## Integration with IDE

### Visual Studio

Install "SonarLint" extension:
- Shows issues in real-time
- Syncs rules with SonarCloud
- Connected mode for team consistency

### VS Code

Install "SonarLint" extension:
1. Install from marketplace
2. Configure binding to SonarCloud project
3. View issues in Problems panel

## Badges

Add to your README:

```markdown
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=EnterpriseDocumentationPlatform&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=EnterpriseDocumentationPlatform)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=EnterpriseDocumentationPlatform&metric=coverage)](https://sonarcloud.io/summary/new_code?id=EnterpriseDocumentationPlatform)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=EnterpriseDocumentationPlatform&metric=bugs)](https://sonarcloud.io/summary/new_code?id=EnterpriseDocumentationPlatform)
```

## Cost

- **Free** for public repositories
- **Paid** for private repositories (see [pricing](https://sonarcloud.io/pricing))

## Alternative: Self-Hosted SonarQube

If you prefer self-hosted:

1. Install SonarQube Community Edition (free)
2. Change `sonar.host.url` to your server
3. Same GitHub Actions workflow works

```yaml
/d:sonar.host.url="https://your-sonarqube-server.com"
```

## Next Steps

1. [ ] Create SonarCloud account
2. [ ] Import organization and project
3. [ ] Generate and add SONAR_TOKEN to GitHub
4. [ ] Update organization key in configs
5. [ ] Push to trigger first analysis
6. [ ] Review results and configure quality gate
7. [ ] Install SonarLint in IDE
