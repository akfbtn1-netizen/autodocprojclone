# Batch Processing System - Setup Guide

## Overview

This guide explains how to set up the complete batch processing system with confidence tracking, multi-source support, and vector indexing.

## Table of Contents

1. [Service Registration](#service-registration)
2. [Configuration](#configuration)
3. [Database Setup](#database-setup)
4. [Testing](#testing)
5. [API Endpoints](#api-endpoints)

---

## Service Registration

### Step 1: Add to Program.cs

Add the following service registrations to `src/Api/Program.cs` **after line 104** (after `AddPersistence`):

```csharp
using Enterprise.Documentation.Core.Application.Services.Batch;
using Enterprise.Documentation.Core.Application.Services.MetadataExtraction;
using Enterprise.Documentation.Core.Application.Services.VectorIndexing;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Api.Configuration;

// ... existing code ...

// =============================================
// BATCH PROCESSING SERVICES
// =============================================

// Metadata extraction with confidence scoring
builder.Services.AddScoped<IMetadataExtractionService, MetadataExtractionService>();

// Batch processing orchestrator
builder.Services.AddScoped<IBatchProcessingOrchestrator, BatchProcessingOrchestrator>();

// Vector indexing for GraphRAG
builder.Services.AddHttpClient<IVectorIndexingService, VectorIndexingService>();
builder.Services.AddScoped<IVectorIndexingService, VectorIndexingService>();

// =============================================
// DOCUMENT GENERATION SERVICES (if not already added)
// =============================================

// DocId generator
builder.Services.AddScoped<IDocIdGeneratorService, DocIdGeneratorService>();

// Template executor
builder.Services.AddScoped<ITemplateExecutorService, TemplateExecutorService>();

// Auto-draft orchestrator
builder.Services.AddScoped<IAutoDraftService, AutoDraftService>();

// OpenAI enhancement (requires HttpClient)
builder.Services.AddHttpClient<IOpenAIEnhancementService, OpenAIEnhancementService>();

// =============================================
// MASTER INDEX SERVICES (if not already added)
// =============================================

// MasterIndex population
builder.Services.AddScoped<IMasterIndexService, MasterIndexService>();

// =============================================
// HANGFIRE BACKGROUND PROCESSING
// =============================================

// Add Hangfire services
builder.Services.AddHangfireServices(builder.Configuration);
```

### Step 2: Configure Hangfire Dashboard

Add **after line 152** (after `app.UseAuthorization()`):

```csharp
// Configure Hangfire dashboard and recurring jobs
app.UseHangfireConfiguration(builder.Configuration);
```

---

## Configuration

### Step 1: Update appsettings.json

Add to `src/Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=IRFS1;Trusted_Connection=true;MultipleActiveResultSets=true"
  },

  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-4",
    "EmbeddingModel": "text-embedding-ada-002",
    "EmbeddingDimensions": 1536,
    "Temperature": 0.3,
    "MaxTokens": 1500
  },

  "VectorDB": {
    "Provider": "Pinecone",
    "ApiKey": "your-pinecone-api-key",
    "Endpoint": "https://your-index.pinecone.io",
    "IndexName": "documentation-index"
  },

  "Hangfire": {
    "DashboardEnabled": true,
    "AllowAnonymousAccess": true,
    "EnableOldBatchCleanup": true,
    "EnableVectorStatsUpdate": false,
    "EnableWeeklyReports": false
  },

  "BatchProcessing": {
    "DefaultConfidenceThreshold": 0.85,
    "DefaultMaxParallelTasks": 4,
    "EnableAutoProcessing": true
  },

  "DocumentGeneration": {
    "BaseOutputPath": "C:\\Temp\\Documentation-Catalog",
    "TemplatesPath": "C:\\Projects\\EnterpriseDocumentationPlatform.V2\\Templates",
    "NodeExecutable": "node"
  },

  "ExcelSync": {
    "LocalFilePath": "C:\\Users\\Alexander.Kirby\\Desktop\\Change Spreadsheet\\BI Analytics Change Spreadsheet.xlsx",
    "SyncIntervalSeconds": 60
  }
}
```

### Step 2: Environment-Specific Configuration

**appsettings.Development.json**:
```json
{
  "OpenAI": {
    "ApiKey": "sk-dev-key-here"
  },
  "VectorDB": {
    "Provider": "Pinecone",
    "ApiKey": "dev-pinecone-key",
    "Endpoint": "https://dev-index.pinecone.io"
  },
  "Hangfire": {
    "AllowAnonymousAccess": true
  }
}
```

**appsettings.Production.json**:
```json
{
  "OpenAI": {
    "ApiKey": "sk-prod-key-here"
  },
  "VectorDB": {
    "Provider": "Pinecone",
    "ApiKey": "prod-pinecone-key",
    "Endpoint": "https://prod-index.pinecone.io"
  },
  "Hangfire": {
    "AllowAnonymousAccess": false
  },
  "DocumentGeneration": {
    "BaseOutputPath": "\\\\SharePointPath\\Documentation-Catalog"
  }
}
```

---

## Database Setup

### Step 1: Run SQL Migration

Execute in SSMS against IRFS1 database:

```powershell
# From project root
sqlcmd -S (localdb)\mssqllocaldb -d IRFS1 -i sql/CREATE_BatchProcessing_Tables.sql
```

Or manually:
1. Open `sql/CREATE_BatchProcessing_Tables.sql` in SSMS
2. Connect to IRFS1 database
3. Execute the entire script

### Step 2: Verify Tables Created

```sql
-- Check tables exist
SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('DaQa')
  AND name IN ('BatchJobs', 'BatchJobItems');

-- Check views exist
SELECT * FROM sys.views WHERE schema_id = SCHEMA_ID('DaQa')
  AND name LIKE 'vw_Batch%';

-- Check stored procedures exist
SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('DaQa')
  AND name LIKE 'usp_%';
```

### Step 3: Initialize Hangfire Schema

The Hangfire schema will be created automatically on first run. To verify:

```sql
SELECT * FROM sys.schemas WHERE name = 'Hangfire';
SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Hangfire');
```

---

## Testing

### Test 1: Start Schema Processing

```bash
curl -X POST http://localhost:5195/api/batchprocessing/schema \
  -H "Content-Type: application/json" \
  -d '{
    "database": "IRFS1",
    "schema": "gwpc",
    "userId": "00000000-0000-0000-0000-000000000000",
    "options": {
      "confidenceThreshold": 0.85,
      "requireHumanReviewBelowThreshold": true,
      "generateDocuments": true,
      "populateMasterIndex": true,
      "generateEmbeddings": true,
      "maxParallelTasks": 4
    }
  }'
```

Expected response:
```json
{
  "batchId": "12345678-1234-1234-1234-123456789abc",
  "message": "Schema processing started for IRFS1.gwpc",
  "statusUrl": "/api/batchprocessing/12345678-1234-1234-1234-123456789abc"
}
```

### Test 2: Check Batch Status

```bash
curl http://localhost:5195/api/batchprocessing/12345678-1234-1234-1234-123456789abc
```

Expected response:
```json
{
  "batchId": "12345678-1234-1234-1234-123456789abc",
  "sourceType": "DatabaseSchema",
  "databaseName": "IRFS1",
  "schemaName": "gwpc",
  "status": "Processing",
  "totalItems": 150,
  "processedCount": 45,
  "successCount": 40,
  "failedCount": 2,
  "requiresReviewCount": 3,
  "progressPercentage": 30.0,
  "highConfidenceCount": 37,
  "mediumConfidenceCount": 3,
  "lowConfidenceCount": 3,
  "averageConfidence": 0.87,
  "startedAt": "2025-11-21T10:00:00Z",
  "estimatedTimeRemaining": "00:15:30"
}
```

### Test 3: Get Items Requiring Review

```bash
curl http://localhost:5195/api/batchprocessing/review?batchId=12345678-1234-1234-1234-123456789abc
```

Expected response:
```json
[
  {
    "itemId": "abcd1234-5678-90ab-cdef-1234567890ab",
    "batchId": "12345678-1234-1234-1234-123456789abc",
    "objectName": "usp_ProcessPayment",
    "objectType": "Procedure",
    "status": "ValidationRequired",
    "confidenceScore": 0.72,
    "confidenceLevel": "Medium",
    "requiresHumanReview": true,
    "validationWarnings": [
      "Table 'Payments' not found in schema - did you mean 'Payment'?"
    ]
  }
]
```

### Test 4: Approve Items

```bash
curl -X POST http://localhost:5195/api/batchprocessing/review/approve \
  -H "Content-Type: application/json" \
  -d '{
    "itemIds": [
      "abcd1234-5678-90ab-cdef-1234567890ab"
    ],
    "reviewedBy": "00000000-0000-0000-0000-000000000000"
  }'
```

### Test 5: Access Hangfire Dashboard

Open browser: `http://localhost:5195/hangfire`

You should see:
- Active jobs
- Succeeded/failed jobs
- Recurring jobs
- Job queues (default, batch-processing, vector-indexing)

---

## API Endpoints

### Batch Operations

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/batchprocessing/schema` | POST | Start schema processing |
| `/api/batchprocessing/folder` | POST | Start folder processing |
| `/api/batchprocessing/excel` | POST | Start Excel import |
| `/api/batchprocessing/{batchId}` | GET | Get batch status |
| `/api/batchprocessing` | GET | Get all batches (paginated) |
| `/api/batchprocessing/{batchId}/cancel` | POST | Cancel batch |
| `/api/batchprocessing/{batchId}/retry` | POST | Retry failed items |

### Review Workflow

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/batchprocessing/review` | GET | Get items requiring review |
| `/api/batchprocessing/review/approve` | POST | Approve items |
| `/api/batchprocessing/review/reject` | POST | Reject items |

### Hangfire Dashboard

| Endpoint | Description |
|----------|-------------|
| `/hangfire` | Background job dashboard |
| `/hangfire/jobs/enqueued` | View enqueued jobs |
| `/hangfire/jobs/processing` | View processing jobs |
| `/hangfire/jobs/succeeded` | View succeeded jobs |
| `/hangfire/jobs/failed` | View failed jobs |
| `/hangfire/recurring` | View recurring jobs |

---

## Monitoring

### Key Queries

```sql
-- Recent batches
SELECT * FROM DaQa.vw_BatchJobSummary
ORDER BY StartedAt DESC;

-- Items requiring review
SELECT * FROM DaQa.vw_ItemsRequiringReview
ORDER BY ConfidenceScore ASC;

-- Processing metrics by source
SELECT * FROM DaQa.vw_BatchProcessingMetrics;

-- Confidence distribution
SELECT * FROM DaQa.vw_ConfidenceDistribution
WHERE Status = 'Completed'
ORDER BY AverageConfidence DESC;

-- Vector indexing status
SELECT * FROM DaQa.vw_VectorIndexingStatus
ORDER BY VectorIndexedPercentage DESC;
```

### Logs to Monitor

```
// Batch started
[Information] Starting schema processing for IRFS1.gwpc

// Processing items
[Information] Processing item {ItemId}: {ObjectName}
[Information] Metadata extracted for {ObjectName}: Confidence={Confidence:F2}, Method={Method}

// Low confidence
[Warning] Item {ItemId} requires human review (confidence: 0.72)

// Completion
[Information] Batch job {BatchId} completed: 145/150 successful, 3 require review, Avg confidence: 0.87

// Errors
[Error] Failed to process item {ItemId}: {ErrorMessage}
```

---

## Troubleshooting

### Issue: Hangfire tables not created

**Solution**: Check connection string and ensure SQL Server has permissions:

```sql
-- Grant permissions
GRANT CREATE TABLE TO [YourUser];
GRANT CREATE SCHEMA TO [YourUser];
```

### Issue: Vector indexing fails

**Solution**: Check VectorDB configuration:
1. Verify API key is valid
2. Check endpoint URL is correct
3. Ensure index exists in Pinecone/Weaviate
4. Check network connectivity

### Issue: Confidence scores always low

**Solution**: Check OpenAI configuration:
1. Verify API key is valid
2. Check model is "gpt-4" (not gpt-3.5-turbo)
3. Ensure database connection works for INFORMATION_SCHEMA queries

### Issue: Jobs stuck in processing

**Solution**: Check Hangfire workers:
1. View `/hangfire/servers` to see active workers
2. Check worker count: should be `ProcessorCount * 2`
3. Restart application to restart workers

---

## Next Steps

1. ✅ Database setup complete
2. ✅ Services registered
3. ✅ Configuration added
4. ⏳ Test schema processing
5. ⏳ Test folder processing
6. ⏳ Set up Pinecone/Weaviate
7. ⏳ Configure OpenAI API key
8. ⏳ Build validation dashboard UI
9. ⏳ Set up Teams notifications for batch completion
10. ⏳ Train AI with approval feedback

---

## Support

- **Logs**: Check console output and application logs
- **Database**: Query `DaQa.vw_*` views for insights
- **Hangfire**: Access `/hangfire` dashboard for job status
- **API**: Use Swagger UI at `/swagger` for testing

---

**Version**: 1.0
**Last Updated**: November 2025
**Owner**: Enterprise Documentation Platform Team
