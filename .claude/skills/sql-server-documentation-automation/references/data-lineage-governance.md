# Data Lineage & Governance for Documentation

> **Last Updated**: December 2025  
> **Sources**: Gartner, Atlan, Thoughtworks, Industry Tools

## Overview

Data lineage has emerged as the "trust layer" essential for AI-driven decision-making. For documentation automation, lineage provides the foundation for understanding dependencies, impact analysis, and ensuring compliance.

## Gartner Perspectives (2025-2026)

### Key Predictions

**2025:**
- Data governance platforms must handle AI model lineage at scale
- 75% of hiring processes will include AI proficiency assessments
- Active metadata automation becomes non-negotiable

**2026:**
- Over 25% of Fortune 500 CDAOs accountable for top-earning D&A products
- Trust-based governance replaces control-based approaches
- AI Security Platforms become critical infrastructure

### Magic Quadrant for Data & Analytics Governance (2025)

First dedicated Magic Quadrant published. Key capabilities:
- Active metadata automation
- AI-powered governance
- Collaborative governance in workflows
- Real-time lineage tracking

## Data Lineage Tools Landscape

### Enterprise Tools Comparison

| Tool | Approach | SQL Server Support | Key Strength |
|------|----------|-------------------|--------------|
| **Octopai** | Automated discovery | Yes | SSIS lineage |
| **MANTA** | Code parsing | Yes | Deep SQL analysis |
| **Collibra** | Catalog-based | Yes | Governance integration |
| **Atlan** | Active metadata | Yes | Collaboration |
| **Secoda** | AI-powered | Yes | Natural language search |
| **Dataedo** | Documentation-first | Yes | ERD creation |
| **Microsoft Purview** | Native Azure | Yes | SSIS native support |

### Open Source Options

| Tool | Language | Features |
|------|----------|----------|
| **OpenLineage** | Python/Java | Standard lineage format |
| **OpenMetadata** | Java | Full data catalog |
| **SQLFlow** | Go | SQL parsing for lineage |
| **Tokern** | Python | Privacy-focused lineage |

## SQL Server Native Lineage

### sys.sql_expression_dependencies

The primary system view for tracking object dependencies:

```sql
-- Get all dependencies for an object
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS referencing_schema,
    OBJECT_NAME(d.referencing_id) AS referencing_object,
    o1.type_desc AS referencing_type,
    COALESCE(OBJECT_SCHEMA_NAME(d.referenced_id), d.referenced_schema_name) AS referenced_schema,
    COALESCE(OBJECT_NAME(d.referenced_id), d.referenced_entity_name) AS referenced_object,
    COALESCE(o2.type_desc, 'EXTERNAL') AS referenced_type,
    d.referenced_database_name,
    d.referenced_server_name,
    d.is_caller_dependent,
    d.is_ambiguous
FROM sys.sql_expression_dependencies d
LEFT JOIN sys.objects o1 ON d.referencing_id = o1.object_id
LEFT JOIN sys.objects o2 ON d.referenced_id = o2.object_id
WHERE d.referencing_id = OBJECT_ID('dbo.YourObject')
   OR d.referenced_id = OBJECT_ID('dbo.YourObject');
```

### Column-Level Lineage

```sql
-- Track column-level dependencies
SELECT 
    OBJECT_NAME(d.referencing_id) AS referencing_object,
    COL_NAME(d.referencing_id, d.referencing_minor_id) AS referencing_column,
    OBJECT_NAME(d.referenced_id) AS referenced_object,
    COL_NAME(d.referenced_id, d.referenced_minor_id) AS referenced_column
FROM sys.sql_expression_dependencies d
WHERE d.referenced_minor_id > 0  -- Column-level reference
  AND d.referencing_minor_id > 0;
```

### Complete Lineage Graph

```sql
-- Recursive CTE for full dependency tree
WITH DependencyGraph AS (
    -- Base: Direct dependencies
    SELECT 
        OBJECT_NAME(referencing_id) AS object_name,
        OBJECT_NAME(referenced_id) AS depends_on,
        1 AS depth,
        CAST(OBJECT_NAME(referencing_id) + ' -> ' + OBJECT_NAME(referenced_id) AS NVARCHAR(MAX)) AS path
    FROM sys.sql_expression_dependencies
    WHERE referenced_id IS NOT NULL
    
    UNION ALL
    
    -- Recursive: Indirect dependencies
    SELECT 
        dg.object_name,
        OBJECT_NAME(d.referenced_id),
        dg.depth + 1,
        dg.path + ' -> ' + OBJECT_NAME(d.referenced_id)
    FROM DependencyGraph dg
    INNER JOIN sys.sql_expression_dependencies d 
        ON OBJECT_NAME(d.referencing_id) = dg.depends_on
    WHERE dg.depth < 10  -- Prevent infinite loops
      AND d.referenced_id IS NOT NULL
)
SELECT DISTINCT object_name, depends_on, depth, path
FROM DependencyGraph
ORDER BY object_name, depth;
```

## Lineage for Documentation

### Impact Analysis

```sql
CREATE PROCEDURE dbo.usp_GetDocumentationImpact
    @ObjectName NVARCHAR(256)
AS
BEGIN
    -- Find all objects that would need doc updates if this object changes
    
    WITH ImpactedObjects AS (
        SELECT 
            OBJECT_NAME(d.referencing_id) AS impacted_object,
            o.type_desc AS object_type,
            1 AS impact_level
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referencing_id = o.object_id
        WHERE d.referenced_id = OBJECT_ID(@ObjectName)
        
        UNION ALL
        
        SELECT 
            OBJECT_NAME(d.referencing_id),
            o.type_desc,
            io.impact_level + 1
        FROM ImpactedObjects io
        INNER JOIN sys.sql_expression_dependencies d 
            ON d.referenced_id = OBJECT_ID(io.impacted_object)
        INNER JOIN sys.objects o ON d.referencing_id = o.object_id
        WHERE io.impact_level < 5
    )
    SELECT 
        impacted_object,
        object_type,
        MIN(impact_level) AS closest_impact,
        CASE 
            WHEN MIN(impact_level) = 1 THEN 'Direct - Immediate update required'
            WHEN MIN(impact_level) = 2 THEN 'Secondary - Review recommended'
            ELSE 'Tertiary - Monitor for issues'
        END AS impact_assessment
    FROM ImpactedObjects
    GROUP BY impacted_object, object_type
    ORDER BY MIN(impact_level), impacted_object;
END;
```

### Lineage Visualization Export

```sql
-- Export for Mermaid.js visualization
CREATE PROCEDURE dbo.usp_ExportLineageMermaid
    @RootObject NVARCHAR(256) = NULL
AS
BEGIN
    DECLARE @mermaid NVARCHAR(MAX) = 'flowchart TD' + CHAR(10);
    
    SELECT @mermaid = @mermaid + 
        '    ' + 
        REPLACE(OBJECT_NAME(d.referencing_id), ' ', '_') + 
        '[' + OBJECT_NAME(d.referencing_id) + ']' +
        ' --> ' + 
        REPLACE(OBJECT_NAME(d.referenced_id), ' ', '_') + 
        '[' + OBJECT_NAME(d.referenced_id) + ']' +
        CHAR(10)
    FROM sys.sql_expression_dependencies d
    INNER JOIN sys.objects o ON d.referenced_id = o.object_id
    WHERE o.is_ms_shipped = 0
      AND (@RootObject IS NULL OR 
           d.referencing_id = OBJECT_ID(@RootObject) OR
           d.referenced_id = OBJECT_ID(@RootObject));
    
    SELECT @mermaid AS MermaidDiagram;
END;
```

### D3.js Force Graph Export

```sql
-- Export for D3.js force-directed graph
CREATE PROCEDURE dbo.usp_ExportLineageD3
AS
BEGIN
    -- Nodes
    SELECT 
        o.object_id AS id,
        s.name + '.' + o.name AS label,
        o.type_desc AS [group]
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    WHERE o.is_ms_shipped = 0
    FOR JSON PATH;
    
    -- Links
    SELECT 
        d.referencing_id AS source,
        d.referenced_id AS target,
        1 AS value
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_id IS NOT NULL
    FOR JSON PATH;
END;
```

## Governance Integration

### Gartner Trust Models

Based on Gartner Data & Analytics Summit 2025 guidance:

**Trust Model Components:**
1. **Value Assessment**: Business criticality of data
2. **Lineage Tracking**: Full provenance chain
3. **Risk Evaluation**: Sensitivity and compliance impact

```sql
CREATE TABLE dbo.DataTrustModel (
    ObjectId INT PRIMARY KEY,
    SchemaName NVARCHAR(128),
    ObjectName NVARCHAR(128),
    
    -- Value Assessment
    BusinessCriticality NVARCHAR(20),  -- 'Critical', 'High', 'Medium', 'Low'
    UsageFrequency INT,  -- Monthly access count
    DependentObjectCount INT,
    
    -- Lineage
    UpstreamSourceCount INT,
    DownstreamConsumerCount INT,
    HasExternalDependency BIT,
    
    -- Risk
    ContainsPII BIT,
    DataClassification NVARCHAR(50),
    ComplianceRequirements NVARCHAR(MAX),  -- JSON array
    
    -- Trust Score (calculated)
    TrustScore AS (
        CASE BusinessCriticality 
            WHEN 'Critical' THEN 40 
            WHEN 'High' THEN 30 
            WHEN 'Medium' THEN 20 
            ELSE 10 
        END +
        CASE WHEN DependentObjectCount > 50 THEN 20 
             WHEN DependentObjectCount > 10 THEN 10 
             ELSE 5 
        END +
        CASE WHEN ContainsPII = 0 THEN 20 ELSE 0 END +
        CASE WHEN HasExternalDependency = 0 THEN 20 ELSE 10 END
    ),
    
    LastAssessed DATETIME2 DEFAULT GETDATE()
);
```

### Active Metadata Management

Following Gartner's recommendation for active metadata:

```sql
-- Active metadata event table
CREATE TABLE dbo.MetadataEvents (
    EventId BIGINT IDENTITY PRIMARY KEY,
    EventType NVARCHAR(50),  -- 'ACCESS', 'MODIFICATION', 'QUERY', 'DOCUMENTATION'
    ObjectId INT,
    UserId NVARCHAR(128),
    EventTimestamp DATETIME2 DEFAULT GETDATE(),
    EventDetails NVARCHAR(MAX),  -- JSON
    INDEX IX_MetadataEvents_Time (EventTimestamp DESC),
    INDEX IX_MetadataEvents_Object (ObjectId, EventTimestamp DESC)
);

-- Trigger to capture query patterns
CREATE TRIGGER TR_CaptureQueryMetadata
ON DATABASE
FOR SELECT
AS
BEGIN
    -- Capture query patterns for active metadata
    -- (Simplified - real implementation would use Query Store)
    INSERT INTO dbo.MetadataEvents (EventType, ObjectId, UserId, EventDetails)
    SELECT 
        'QUERY',
        OBJECT_ID(EVENTDATA().value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(256)')),
        ORIGINAL_LOGIN(),
        EVENTDATA().value('(/EVENT_INSTANCE/TSQLCommand/CommandText)[1]', 'NVARCHAR(MAX)');
END;
```

### Compliance Documentation

```sql
-- Generate compliance documentation
CREATE PROCEDURE dbo.usp_GenerateComplianceReport
    @Regulation NVARCHAR(50)  -- 'GDPR', 'HIPAA', 'SOX', 'CCPA'
AS
BEGIN
    SELECT 
        s.name AS SchemaName,
        t.name AS TableName,
        c.name AS ColumnName,
        ty.name AS DataType,
        
        -- Classification
        CASE 
            WHEN c.name LIKE '%SSN%' OR c.name LIKE '%Social%Security%' THEN 'PII-SSN'
            WHEN c.name LIKE '%Email%' THEN 'PII-Email'
            WHEN c.name LIKE '%Phone%' THEN 'PII-Phone'
            WHEN c.name LIKE '%Address%' THEN 'PII-Address'
            WHEN c.name LIKE '%DOB%' OR c.name LIKE '%Birth%' THEN 'PII-DOB'
            WHEN c.name LIKE '%Credit%Card%' THEN 'PCI'
            WHEN c.name LIKE '%Diagnosis%' OR c.name LIKE '%Medical%' THEN 'PHI'
            ELSE 'General'
        END AS DataClassification,
        
        -- Existing documentation
        CAST(ep.value AS NVARCHAR(MAX)) AS CurrentDescription,
        
        -- Lineage info
        (SELECT COUNT(*) FROM sys.sql_expression_dependencies d 
         WHERE d.referenced_id = t.object_id) AS ConsumerCount,
        
        -- Compliance requirements
        CASE @Regulation
            WHEN 'GDPR' THEN 
                CASE WHEN c.name LIKE '%Email%' OR c.name LIKE '%Name%' 
                     THEN 'Subject to Right to Erasure'
                     ELSE 'N/A'
                END
            WHEN 'HIPAA' THEN
                CASE WHEN c.name LIKE '%Diagnosis%' OR c.name LIKE '%Medical%'
                     THEN 'PHI - Requires encryption and audit'
                     ELSE 'N/A'
                END
            ELSE 'Review Required'
        END AS ComplianceNote
        
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = c.object_id 
        AND ep.minor_id = c.column_id
        AND ep.name = 'MS_Description'
    WHERE t.is_ms_shipped = 0
    ORDER BY s.name, t.name, c.column_id;
END;
```

## Thoughtworks Technology Radar Guidance

### MCP Security for Lineage Systems

From Technology Radar Vol.33 (November 2025):

**Toxic Flow Analysis for AI:**
- Map data flows through agentic systems
- Identify vulnerabilities at interaction points
- Essential as AI agents increase

**MCP-scan Tool:**
- Vulnerability detection for MCP implementations
- Check tool definitions for malicious content
- Validate cross-server interactions

```python
# Toxic flow analysis for documentation MCP
def analyze_documentation_flows(mcp_config):
    """Map data flows in documentation automation"""
    
    flows = {
        "database_to_mcp": {
            "data": ["schema", "definitions", "extended_properties"],
            "risks": ["data_exfiltration", "pii_exposure"],
            "mitigations": ["column_filtering", "pii_masking"]
        },
        "mcp_to_llm": {
            "data": ["prompts", "context", "code"],
            "risks": ["prompt_injection", "context_overflow"],
            "mitigations": ["input_validation", "token_limits"]
        },
        "llm_to_database": {
            "data": ["generated_docs", "classifications"],
            "risks": ["hallucinated_content", "sql_injection"],
            "mitigations": ["validation_pipeline", "human_review"]
        }
    }
    
    return analyze_risks(flows)
```

### Context Engineering

The systematic design of information provided to LLMs:

1. **Prompt Context**: Clear task definition
2. **Memory Context**: Relevant history
3. **Data Context**: Schema and lineage information
4. **Tool Context**: Available actions

## References

### Industry Reports
- Gartner Magic Quadrant for Data & Analytics Governance 2025
- Gartner Hype Cycle for Data & Analytics Governance 2025
- Thoughtworks Technology Radar Vol.33

### Tools Documentation
- Microsoft Purview Data Lineage
- Atlan Active Metadata Platform
- OpenLineage Specification

### Research
- "Data Lineage: The Trust Layer for AI" - Atlan, 2025
- "2025 Data Automation Trends" - WhereScape
