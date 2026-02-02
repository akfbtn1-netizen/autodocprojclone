# TODO: Intelligent Document Generation v2.0.0 Implementation

> **Enterprise Documentation Platform V2 - AI Document Generation Integration**
> 
> Est. Duration: 12-15 days | Priority: HIGH | Dependencies: MasterIndex, Azure OpenAI

---

## Overview

This TODO guide covers implementing the Intelligent Document Generation v2.0.0 skill into the Enterprise Documentation Platform. The implementation adds Azure OpenAI-powered documentation generation with tiered complexity and token optimization.

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Generation success rate | ≥98% | Automated tracking |
| Tier classification accuracy | ≥95% | Manual validation of 50 procs |
| Token efficiency | ≤1,500 avg/doc | API usage tracking |
| Cost per document | ≤$0.01 avg | Azure billing |
| Generation latency | <10s Tier 1, <5s Tier 2/3 | Performance tests |

---

## Phase 1: Infrastructure Setup (Days 1-2)

### 1.1 Azure OpenAI Configuration
- [ ] Provision Azure OpenAI resource in Azure subscription
- [ ] Deploy GPT-4o model (for Tier 1 complex docs)
- [ ] Deploy GPT-4o-mini model (for Tier 2/3 standard docs)
- [ ] Configure rate limits (TPM, RPM)
- [ ] Set up managed identity for secure access
- [ ] Add connection string to Key Vault

### 1.2 NuGet/NPM Package Installation
- [ ] Add `Azure.AI.OpenAI` NuGet package to DocGen.Infrastructure
- [ ] Add `DocumentFormat.OpenXml` if not present (Shadow Metadata)
- [ ] Add `Ajv` or equivalent for JSON schema validation (.NET: `NJsonSchema`)
- [ ] Verify `docx` npm package in Next.js frontend (if client-side generation)

### 1.3 Configuration Management
- [ ] Add Azure OpenAI settings to `appsettings.json`:
  ```json
  {
    "AzureOpenAI": {
      "Endpoint": "https://xxx.openai.azure.com/",
      "DeploymentGpt4o": "gpt-4o",
      "DeploymentGpt4oMini": "gpt-4o-mini",
      "ApiVersion": "2024-02-15-preview"
    }
  }
  ```
- [ ] Create `AzureOpenAIOptions` configuration class
- [ ] Register configuration in DI container
- [ ] Add health check for Azure OpenAI connectivity

### 1.4 Database Schema Updates
- [ ] Add `DocumentationTier` column to `daqa.MasterIndex`
- [ ] Add `TokensUsed` column to `daqa.MasterIndex`
- [ ] Add `GenerationCostUSD` column to `daqa.MasterIndex`
- [ ] Add `LastAIGeneratedAt` column to `daqa.MasterIndex`
- [ ] Create `daqa.DocumentGenerationLog` table for audit trail

```sql
-- Phase 1.4: Schema updates
ALTER TABLE daqa.MasterIndex ADD DocumentationTier TINYINT NULL;
ALTER TABLE daqa.MasterIndex ADD TokensUsed INT NULL;
ALTER TABLE daqa.MasterIndex ADD GenerationCostUSD DECIMAL(10,6) NULL;
ALTER TABLE daqa.MasterIndex ADD LastAIGeneratedAt DATETIME2 NULL;

CREATE TABLE daqa.DocumentGenerationLog (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    MasterIndexId INT NOT NULL,
    ObjectName NVARCHAR(256) NOT NULL,
    Tier TINYINT NOT NULL,
    Model NVARCHAR(50) NOT NULL,
    PromptTokens INT NOT NULL,
    CompletionTokens INT NOT NULL,
    TotalTokens INT NOT NULL,
    CostUSD DECIMAL(10,6) NOT NULL,
    LatencyMs INT NOT NULL,
    Success BIT NOT NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (MasterIndexId) REFERENCES daqa.MasterIndex(MasterIndexId)
);

CREATE INDEX IX_DocumentGenerationLog_MasterIndexId ON daqa.DocumentGenerationLog(MasterIndexId);
CREATE INDEX IX_DocumentGenerationLog_GeneratedAt ON daqa.DocumentGenerationLog(GeneratedAt);
```

---

## Phase 2: Core Services (Days 3-5)

### 2.1 Azure OpenAI Service
- [ ] Create `IAzureOpenAIService` interface in DocGen.Application
- [ ] Implement `AzureOpenAIService` in DocGen.Infrastructure
- [ ] Add retry logic with Polly (exponential backoff)
- [ ] Add request/response logging
- [ ] Add token counting utility
- [ ] Unit tests for service

```csharp
// IAzureOpenAIService.cs
public interface IAzureOpenAIService
{
    Task<DocumentationResponse> GenerateDocumentationAsync(
        string systemPrompt,
        string userPrompt,
        GenerationOptions options,
        CancellationToken cancellationToken = default);
}

public record GenerationOptions(
    string Model = "gpt-4o-mini",
    int MaxTokens = 2000,
    double Temperature = 0.3,
    bool JsonMode = true);

public record DocumentationResponse(
    string Content,
    TokenUsage Tokens,
    int LatencyMs);

public record TokenUsage(int Prompt, int Completion, int Total);
```

### 2.2 Tier Classifier Service
- [ ] Create `ITierClassifierService` interface
- [ ] Implement `TierClassifierService`
- [ ] Add complexity scoring algorithm (from SKILL.md)
- [ ] Add SQL analysis helpers (detect cursors, transactions, dynamic SQL)
- [ ] Unit tests with sample procedures

```csharp
// ITierClassifierService.cs
public interface ITierClassifierService
{
    TierClassification Classify(ObjectAnalysis analysis);
    ObjectAnalysis AnalyzeStoredProcedure(string definition, int paramCount, int tableCount);
}

public record TierClassification(
    int Tier,
    string Reason,
    string Model,
    int MaxTokens);

public record ObjectAnalysis(
    int LineCount,
    int TablesAccessed,
    int ParameterCount,
    bool HasNestedConditions,
    bool HasCursors,
    bool HasExplicitTransactions,
    bool HasDynamicSQL,
    bool HasTryCatch);
```

### 2.3 Prompt Builder Service
- [ ] Create `IPromptBuilderService` interface
- [ ] Implement `PromptBuilderService`
- [ ] Add stored procedure prompt template
- [ ] Add table prompt template
- [ ] Add view prompt template
- [ ] Add prompt compression (remove comments, normalize whitespace)
- [ ] Unit tests for prompt generation

### 2.4 Response Validator Service
- [ ] Create `IResponseValidatorService` interface
- [ ] Implement `ResponseValidatorService`
- [ ] Add JSON schema for stored procedure response
- [ ] Add JSON schema for table response
- [ ] Add JSON schema for view response
- [ ] Unit tests for validation

---

## Phase 3: Document Generation Pipeline (Days 6-8)

### 3.1 DOCX Generator Service
- [ ] Create `IDocxGeneratorService` interface
- [ ] Implement `DocxGeneratorService` using Open XML SDK
- [ ] Add stored procedure document template
- [ ] Add table document template
- [ ] Add view document template
- [ ] Add parameter table generation
- [ ] Add tables accessed table generation
- [ ] Add styling (headers, colors, fonts)
- [ ] Integration tests

### 3.2 Shadow Metadata Service
- [ ] Verify existing `ShadowMetadataService` works
- [ ] Add new properties: `Documentation_Tier`, `Tokens_Used`, `Generation_Cost_USD`
- [ ] Add content hash generation for drift detection
- [ ] Add sync status checking
- [ ] Unit tests

### 3.3 Documentation Cache Service
- [ ] Create `IDocumentationCacheService` interface
- [ ] Implement `DocumentationCacheService` (in-memory or Redis)
- [ ] Add cache key generation (object name + definition hash)
- [ ] Add TTL configuration (default 24 hours)
- [ ] Add cache hit/miss metrics
- [ ] Unit tests

### 3.4 Main Pipeline Orchestrator
- [ ] Create `IDocumentGenerationPipeline` interface
- [ ] Implement `DocumentGenerationPipeline`
- [ ] Wire up all services (classify → prompt → generate → validate → docx → cache)
- [ ] Add metrics collection
- [ ] Add error handling and retry logic
- [ ] Integration tests with real Azure OpenAI

```csharp
// IDocumentGenerationPipeline.cs
public interface IDocumentGenerationPipeline
{
    Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default);
    
    Task<BatchGenerationResult> GenerateBatchAsync(
        IEnumerable<GenerationRequest> requests,
        CancellationToken cancellationToken = default);
}

public record GenerationRequest(
    int MasterIndexId,
    string ObjectName,
    string SchemaName,
    string ObjectType,
    string Definition,
    List<ParameterInfo> Parameters,
    List<TableAccessInfo> TablesAccessed);

public record GenerationResult(
    bool Success,
    StoredProcedureDocumentation? Documentation,
    byte[]? DocxBuffer,
    int Tier,
    GenerationMetrics Metrics,
    string? Error);
```

---

## Phase 4: API Integration (Days 9-10)

### 4.1 MediatR Commands/Queries
- [ ] Create `GenerateDocumentationCommand` and handler
- [ ] Create `GenerateBatchDocumentationCommand` and handler
- [ ] Create `GetGenerationHistoryQuery` and handler
- [ ] Create `GetCostSummaryQuery` and handler
- [ ] Add FluentValidation for requests

### 4.2 DocumentsController Updates
- [ ] Add `POST /api/documents/{id}/generate` endpoint
- [ ] Add `POST /api/documents/generate-batch` endpoint
- [ ] Add `GET /api/documents/{id}/generation-history` endpoint
- [ ] Add `GET /api/documents/cost-summary` endpoint
- [ ] Add Swagger documentation
- [ ] Integration tests

### 4.3 Background Service
- [ ] Create `DocumentGenerationBackgroundService`
- [ ] Integrate with Azure Service Bus for triggers
- [ ] Add batch processing support
- [ ] Add concurrency limiting (avoid rate limits)
- [ ] Add dead letter queue handling

---

## Phase 5: Frontend Integration (Days 11-12)

### 5.1 API Client Updates
- [ ] Add `generateDocumentation` method to API client
- [ ] Add `generateBatchDocumentation` method
- [ ] Add `getGenerationHistory` method
- [ ] Add types for all DTOs

### 5.2 UI Components
- [ ] Create `GenerateDocumentButton` component
- [ ] Create `TierBadge` component (color-coded Tier 1/2/3)
- [ ] Create `GenerationProgress` component
- [ ] Create `GenerationHistory` component
- [ ] Create `CostSummaryCard` component

### 5.3 Page Updates
- [ ] Add generate button to object detail page
- [ ] Add tier display to object list
- [ ] Add generation history tab
- [ ] Add cost summary to dashboard
- [ ] Add toast notifications for generation complete

---

## Phase 6: Testing & Validation (Days 13-14)

### 6.1 Unit Tests
- [ ] TierClassifierService tests (edge cases)
- [ ] PromptBuilderService tests (all object types)
- [ ] ResponseValidatorService tests (valid/invalid responses)
- [ ] DocxGeneratorService tests (output verification)

### 6.2 Integration Tests
- [ ] Azure OpenAI connectivity test
- [ ] Full pipeline end-to-end test
- [ ] Batch generation test
- [ ] Cache hit/miss test
- [ ] Error recovery test

### 6.3 Production Validation
- [ ] Test with 10 Tier 1 (complex) procedures from IRFS1
- [ ] Test with 20 Tier 2 (standard) procedures from IRFS1
- [ ] Test with 20 Tier 3 (simple) procedures from IRFS1
- [ ] Validate tier classification accuracy (target: 95%)
- [ ] Validate documentation quality (manual review)
- [ ] Measure actual token usage and costs

---

## Phase 7: Documentation & Deployment (Day 15)

### 7.1 Documentation
- [ ] Update ARCHITECTURE.md with new services
- [ ] Add API documentation for new endpoints
- [ ] Create user guide for document generation
- [ ] Document cost estimation methodology

### 7.2 Deployment
- [ ] Create deployment script for database changes
- [ ] Update Azure Container App configuration
- [ ] Configure Azure OpenAI in production
- [ ] Set up monitoring and alerting
- [ ] Create rollback procedure

### 7.3 Monitoring
- [ ] Add Application Insights custom metrics
  - Generation success rate
  - Average latency by tier
  - Token usage by tier
  - Cost per day/week/month
- [ ] Create Azure Monitor dashboard
- [ ] Set up alerts for failures and cost thresholds

---

## Dependencies

| Dependency | Status | Notes |
|------------|--------|-------|
| Azure OpenAI resource | Required | Must be provisioned first |
| MasterIndex table | Exists | Add new columns |
| DocGen.Infrastructure project | Exists | Add new services |
| Next.js frontend | Exists | Add new components |

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Azure OpenAI rate limits | Implement retry with backoff, batch processing |
| High API costs | Tier classification, caching, GPT-4o-mini default |
| Invalid LLM responses | JSON schema validation, retry on failure |
| Long generation times | Background processing, progress indicators |

## Estimated Costs

| Item | Tier 1 | Tier 2 | Tier 3 |
|------|--------|--------|--------|
| Model | GPT-4o | GPT-4o-mini | GPT-4o-mini |
| Avg tokens | 6,000 | 3,000 | 1,500 |
| Cost/doc | $0.05-0.10 | $0.001 | $0.0005 |

**Example: 500 procedures**
- 50 Tier 1: ~$5.00
- 200 Tier 2: ~$0.20
- 250 Tier 3: ~$0.13
- **Total: ~$5.33**

---

## Checklist Summary

- [ ] **Phase 1:** Infrastructure (8 tasks)
- [ ] **Phase 2:** Core Services (16 tasks)
- [ ] **Phase 3:** Pipeline (12 tasks)
- [ ] **Phase 4:** API (10 tasks)
- [ ] **Phase 5:** Frontend (10 tasks)
- [ ] **Phase 6:** Testing (12 tasks)
- [ ] **Phase 7:** Deployment (9 tasks)

**Total: 77 tasks across 15 days**

---

## Quick Reference

### Model Selection
- **Tier 1 (Complex):** GPT-4o - deep reasoning needed
- **Tier 2 (Standard):** GPT-4o-mini - cost-effective
- **Tier 3 (Simple):** GPT-4o-mini - minimal tokens

### Key Files
- `SKILL.md` - Complete skill documentation
- `examples/DocumentGenerationPipeline.ts` - Reference implementation
- `prompts/*.md` - Prompt templates

### API Endpoints
- `POST /api/documents/{id}/generate` - Generate single doc
- `POST /api/documents/generate-batch` - Generate batch
- `GET /api/documents/{id}/generation-history` - View history
- `GET /api/documents/cost-summary` - Cost tracking
