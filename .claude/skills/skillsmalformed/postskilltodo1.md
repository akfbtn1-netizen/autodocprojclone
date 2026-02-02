# Post-Skill TODO: Database Intelligence Engine

Now that the skill is built, here's how to leverage it for maximum impact.

---

## Immediate Actions (Today)

### 1. Populate the Lineage Table

Load your procdoc.txt data directly into `daqa.ColumnLineage`:

```sql
-- Create the table first (from skill schema)
CREATE TABLE daqa.ColumnLineage (
    LineageId INT IDENTITY(1,1) PRIMARY KEY,
    procedure_schema NVARCHAR(128) NOT NULL,
    procedure_name NVARCHAR(128) NOT NULL,
    referenced_schema NVARCHAR(128) NOT NULL,
    referenced_table NVARCHAR(128) NOT NULL,
    referenced_column NVARCHAR(128) NOT NULL,
    operation_type NVARCHAR(20) NOT NULL,
    [read] BIT DEFAULT 0,
    [update] BIT DEFAULT 0,
    [insert] BIT DEFAULT 0,
    [delete] BIT DEFAULT 0,
    merge_target BIT DEFAULT 0,
    merge_source BIT DEFAULT 0,
    detection_method NVARCHAR(20) DEFAULT 'DMV',
    last_analyzed DATETIME2 DEFAULT GETUTCDATE(),
    
    INDEX IX_ColumnLineage_Proc (procedure_schema, procedure_name),
    INDEX IX_ColumnLineage_Table (referenced_schema, referenced_table),
    INDEX IX_ColumnLineage_Column (referenced_schema, referenced_table, referenced_column),
    INDEX IX_ColumnLineage_Operation (operation_type)
);

-- Bulk load your extracted lineage data
BULK INSERT daqa.ColumnLineage
FROM 'C:\path\to\procdoc.txt'
WITH (
    FIELDTERMINATOR = '\t',
    ROWTERMINATOR = '\n',
    FIRSTROW = 2
);
```

**Result**: 27K+ column-level dependencies queryable instantly.

---

### 2. Expose via API

Add a `LineageController` endpoint to your existing API:

```csharp
[ApiController]
[Route("api/[controller]")]
public class LineageController : ControllerBase
{
    private readonly ILineageService _lineageService;

    public LineageController(ILineageService lineageService)
        => _lineageService = lineageService;

    [HttpGet("impact/{schema}/{table}/{column}")]
    public async Task<ImpactAnalysis> GetColumnImpact(
        string schema, string table, string column)
        => await _lineageService.AnalyzeImpactAsync(schema, table, column);

    [HttpGet("procedure/{schema}/{procName}")]
    public async Task<IEnumerable<ColumnLineage>> GetProcedureDependencies(
        string schema, string procName)
        => await _lineageService.GetProcedureDependenciesAsync(schema, procName);

    [HttpGet("dynamic-sql")]
    public async Task<IEnumerable<string>> GetDynamicSqlProcedures()
        => await _lineageService.GetDynamicSqlProceduresAsync();
}
```

**Use case**: Before any schema change, call `/api/lineage/impact/{schema}/{table}/{column}` to get risk score + affected procedures.

---

## This Week

### 3. Enrich Document Generation

Update `DocGeneratorService` to auto-populate lineage data:

```csharp
public async Task<DocumentMetadata> GenerateWithLineageAsync(string procSchema, string procName)
{
    var doc = await _docGenerator.GenerateAsync(procSchema, procName);
    var lineage = await _lineageService.GetProcedureDependenciesAsync(procSchema, procName);
    
    // Data Sources (tables READ from)
    doc.DataSources = lineage
        .Where(l => l.Operation == OperationType.Read)
        .Select(l => $"{l.ReferencedSchema}.{l.ReferencedTable}")
        .Distinct().ToList();
    
    // Data Targets (tables written to)
    doc.DataTargets = lineage
        .Where(l => l.Operation != OperationType.Read)
        .Select(l => $"{l.ReferencedSchema}.{l.ReferencedTable}")
        .Distinct().ToList();
    
    // Column Operations grouped by table
    doc.ColumnOperations = lineage
        .GroupBy(l => l.ReferencedTable)
        .ToDictionary(g => g.Key, g => g.ToList());
    
    // Risk Classification
    doc.RiskLevel = CalculateRisk(lineage);
    
    return doc;
}
```

**Benefits**:
- Documents now include data lineage automatically
- Risk classification based on operation types
- No manual documentation of dependencies

---

### 4. Feed the RAG System

Embed lineage relationships for semantic search:

```csharp
// Generate embeddings for lineage queries
var lineageText = $@"
Procedure {procSchema}.{procName} reads from tables: {string.Join(", ", dataSources)}.
It writes to tables: {string.Join(", ", dataTargets)}.
Columns modified: {string.Join(", ", updatedColumns)}.
Risk level: {riskLevel}.
";

await _embeddingService.IndexAsync(lineageText, $"lineage:{procSchema}.{procName}");
```

**Enables natural language queries**:
- "What procedures modify the CLAIM_KEY column?"
- "Show me the data flow for balance_financials"
- "Which procedures have DELETE operations on bal schema?"

---

## Strategic Value Matrix

| Capability | Before | After |
|------------|--------|-------|
| Impact analysis | Manual, error-prone | Automated, risk-scored |
| Document generation | Static templates | Lineage-enriched, dynamic |
| Schema changes | Risky guesswork | Pre-validated with affected list |
| Developer onboarding | "Ask Bob who's been here 15 years" | Self-service queries |
| Audit compliance | Manual tracking | Automated lineage trails |
| Debug/troubleshooting | Grep through code | Query relationships instantly |

---

## The Big Picture

**The skill becomes your database's institutional memory.**

```
┌─────────────────────────────────────────────────────────────┐
│                    Documentation Platform                    │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐       │
│  │   Lineage   │──▶│     Doc     │──▶│     RAG     │       │
│  │   Service   │   │  Generator  │   │   Search    │       │
│  └─────────────┘   └─────────────┘   └─────────────┘       │
│         │                                    │              │
│         ▼                                    ▼              │
│  ┌─────────────┐                    ┌─────────────┐        │
│  │   Impact    │                    │  Semantic   │        │
│  │  Analysis   │                    │   Queries   │        │
│  └─────────────┘                    └─────────────┘        │
├─────────────────────────────────────────────────────────────┤
│              daqa.ColumnLineage (27K+ rows)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Next Steps

- [ ] Create `daqa.ColumnLineage` table
- [ ] Bulk load procdoc.txt data
- [ ] Implement `ILineageService`
- [ ] Add `LineageController` to API
- [ ] Update `DocGeneratorService` with lineage enrichment
- [ ] Index lineage data in Qdrant for semantic search
- [ ] Build impact analysis UI in frontend

---

## Ready to Implement?

Request the complete implementation:
- `LineageController.cs` + `LineageService.cs`
- Updated `DocGeneratorService.cs` with lineage integration
- SQL scripts for table creation and data loading
