# T-SQL ScriptDom Lineage Implementation TODO

## Project Context
- **Platform**: Enterprise Documentation Platform V2
- **Database**: IRFS1/ibidb2003dv/daqa schema
- **Timeline**: Week 4-5 of 8-week implementation plan
- **Risk Level**: High (identified as critical path item)

---

## Phase 1: Foundation Setup (Days 1-2)

### 1.1 NuGet Package Installation
- [ ] Add `Microsoft.SqlServer.TransactSql.ScriptDom` v161.8901.0+ to DocGenerator.Api
- [ ] Add to DocGenerator.Infrastructure project
- [ ] Verify Central Package Management (CPM) in Directory.Packages.props
- [ ] Test build succeeds with no conflicts

### 1.2 Database Schema Deployment
- [ ] Review `01-lineage-schema.sql` for IRFS1 compatibility
- [ ] Create `lineage` schema in daqa database
- [ ] Deploy `lineage.NodeType` lookup table
- [ ] Deploy `lineage.TransformationType` lookup table
- [ ] Deploy `lineage.Node` table with indexes
- [ ] Deploy `lineage.Edge` table with indexes
- [ ] Deploy `lineage.ExtractionHistory` table
- [ ] Deploy `lineage.ExtractionWarning` table
- [ ] Test all constraints and indexes

### 1.3 Stored Procedures Deployment
- [ ] Deploy `lineage.UpsertNode` procedure
- [ ] Deploy `lineage.UpsertEdge` procedure
- [ ] Deploy `lineage.GetUpstreamLineage` function
- [ ] Deploy `lineage.GetDownstreamImpact` function
- [ ] Deploy `lineage.AnalyzeColumnImpact` procedure
- [ ] Deploy `lineage.TraceColumnOrigin` procedure
- [ ] Deploy `lineage.SyncNodesFromDatabase` procedure
- [ ] Test all procedures with sample data

---

## Phase 2: Core Infrastructure (Days 3-5)

### 2.1 Domain Models
- [ ] Create `LineageColumn` record in Domain/Entities
- [ ] Create `LineageEdge` record in Domain/Entities
- [ ] Create `LineageNode` entity with EF Core mapping
- [ ] Create `TransformationType` enum
- [ ] Create `DynamicSqlUsage` record
- [ ] Create `TempTableInfo` record
- [ ] Create `LineageExtractionResult` aggregate

### 2.2 Schema Metadata Provider
- [ ] Create `ISchemaMetadataProvider` interface
- [ ] Implement `SqlServerMetadataProvider` class
- [ ] Add column metadata caching (1-hour TTL)
- [ ] Add cache refresh mechanism
- [ ] Integrate with existing `daqa.MasterIndex` table
- [ ] Test against IRFS1 database with actual tables

### 2.3 Parsing Service
- [ ] Create `ITsqlParsingService` interface
- [ ] Implement `TsqlParsingService` class
- [ ] Add parser factory for version selection
- [ ] Add parse result caching
- [ ] Add error handling for malformed SQL
- [ ] Test with complex stored procedures from IRFS1

---

## Phase 3: Lineage Visitor Implementation (Days 6-10)

### 3.1 Base Visitor Infrastructure
- [ ] Create `LineageExtractorVisitor` extending `TSqlConcreteFragmentVisitor`
- [ ] Implement context stack for nested queries
- [ ] Implement alias map builder for FROM clauses
- [ ] Add CTE definition tracking dictionary
- [ ] Add temp table tracking dictionary

### 3.2 Statement Handlers
- [ ] Implement `Visit(InsertStatement)` for INSERT lineage
- [ ] Implement `Visit(UpdateStatement)` for UPDATE lineage
- [ ] Implement `Visit(MergeStatement)` for MERGE lineage
- [ ] Implement `Visit(SelectStatement)` for SELECT INTO
- [ ] Handle `Visit(CreateViewStatement)` for view definitions
- [ ] Handle `Visit(CreateProcedureStatement)` for proc definitions

### 3.3 Expression Processing
- [ ] Implement `ProcessScalarExpression` for all expression types
- [ ] Handle `ColumnReferenceExpression` (direct reference)
- [ ] Handle `FunctionCall` (aggregations and scalar functions)
- [ ] Handle `CaseExpression` (CASE WHEN)
- [ ] Handle `CastCall` and `ConvertCall`
- [ ] Handle `CoalesceExpression` and `NullIfExpression`
- [ ] Handle `BinaryExpression` (arithmetic/concatenation)

### 3.4 Column Resolution
- [ ] Implement qualified column resolution (alias.column)
- [ ] Implement unqualified column resolution (schema search)
- [ ] Handle ambiguous column warnings
- [ ] Handle CTE column references
- [ ] Handle temp table column references
- [ ] Handle derived table columns

### 3.5 Special Construct Handling
- [ ] Implement CTE tracking (`Visit(WithCtesAndXmlNamespaces)`)
- [ ] Implement temp table tracking (`Visit(CreateTableStatement)`)
- [ ] Implement table variable tracking (`Visit(DeclareTableVariableStatement)`)
- [ ] Implement SELECT * expansion with schema metadata
- [ ] Handle recursive CTEs

### 3.6 Dynamic SQL Detection
- [ ] Detect `sp_executesql` calls
- [ ] Detect `EXEC(@sql)` patterns
- [ ] Detect `OPENROWSET` usage
- [ ] Detect `OPENQUERY` usage
- [ ] Log warnings with line numbers
- [ ] Calculate confidence reduction

---

## Phase 4: Service Layer (Days 11-13)

### 4.1 Lineage Extraction Service
- [ ] Create `ILineageExtractionService` interface
- [ ] Implement `LineageExtractionService` class
- [ ] Add async extraction method
- [ ] Add batch processing for multiple objects
- [ ] Implement confidence calculation
- [ ] Add extraction history logging

### 4.2 Graph Storage Service
- [ ] Create `ILineageGraphService` interface
- [ ] Implement `LineageGraphService` class
- [ ] Implement node upsert with Dapper
- [ ] Implement edge upsert with Dapper
- [ ] Implement upstream lineage query
- [ ] Implement downstream impact query
- [ ] Add transaction support for batch operations

### 4.3 Impact Analysis Service
- [ ] Create `IImpactAnalysisService` interface
- [ ] Implement `ImpactAnalysisService` class
- [ ] Add column impact analysis
- [ ] Add table impact analysis
- [ ] Add procedure dependency analysis
- [ ] Generate impact reports

---

## Phase 5: API Integration (Days 14-16)

### 5.1 API Endpoints
- [ ] Create `LineageController` in API project
- [ ] `POST /api/lineage/extract/{objectName}` - Extract lineage for object
- [ ] `GET /api/lineage/upstream/{schema}/{table}/{column}` - Get sources
- [ ] `GET /api/lineage/downstream/{schema}/{table}/{column}` - Get impact
- [ ] `POST /api/lineage/analyze-impact` - Full impact analysis
- [ ] `POST /api/lineage/sync` - Sync nodes from database
- [ ] `GET /api/lineage/graph` - Get graph for visualization

### 5.2 MediatR Commands/Queries
- [ ] Create `ExtractLineageCommand` and handler
- [ ] Create `GetUpstreamLineageQuery` and handler
- [ ] Create `GetDownstreamImpactQuery` and handler
- [ ] Create `AnalyzeColumnImpactQuery` and handler
- [ ] Create `SyncLineageNodesCommand` and handler

### 5.3 Background Processing
- [ ] Create `LineageExtractionBackgroundService`
- [ ] Integrate with Azure Service Bus for triggers
- [ ] Add scheduled full database scan option
- [ ] Add incremental extraction on schema changes

---

## Phase 6: Frontend Integration (Days 17-19)

### 6.1 Lineage Visualization Component
- [ ] Create `LineageGraph.tsx` React component
- [ ] Integrate D3.js or React Flow for graph rendering
- [ ] Add node styling by type (table, view, column, procedure)
- [ ] Add edge styling by transformation type
- [ ] Implement zoom and pan controls
- [ ] Add node click for details panel

### 6.2 Impact Analysis UI
- [ ] Create `ImpactAnalysis.tsx` page
- [ ] Add column selector with autocomplete
- [ ] Display upstream sources table
- [ ] Display downstream impact table
- [ ] Show affected procedures list
- [ ] Add export to Excel functionality

### 6.3 Integration with Existing UI
- [ ] Add lineage tab to column detail page
- [ ] Add "View Lineage" button to procedure documentation
- [ ] Add impact warning on MasterIndex column edits
- [ ] Integrate with approval workflow for high-impact changes

---

## Phase 7: Testing (Days 20-22)

### 7.1 Unit Tests
- [ ] Test `TsqlParsingService` with valid/invalid SQL
- [ ] Test `SqlServerMetadataProvider` with mock data
- [ ] Test `LineageExtractorVisitor` for INSERT statements
- [ ] Test `LineageExtractorVisitor` for UPDATE statements
- [ ] Test `LineageExtractorVisitor` for MERGE statements
- [ ] Test CTE resolution
- [ ] Test temp table tracking
- [ ] Test dynamic SQL detection
- [ ] Test SELECT * expansion

### 7.2 Integration Tests
- [ ] Test full extraction pipeline with TestContainers
- [ ] Test graph storage and retrieval
- [ ] Test upstream/downstream traversal
- [ ] Test API endpoints end-to-end
- [ ] Test background service processing

### 7.3 Real-World Validation
- [ ] Extract lineage from 10 complex IRFS1 procedures
- [ ] Validate against manual lineage analysis
- [ ] Measure accuracy (target: 97%+)
- [ ] Document edge cases and limitations
- [ ] Performance test with large procedures

---

## Phase 8: Documentation & Deployment (Days 23-25)

### 8.1 Documentation
- [ ] Update ARCHITECTURE.md with lineage components
- [ ] Create LineageExtraction.md developer guide
- [ ] Document API endpoints in Swagger
- [ ] Create troubleshooting guide for common issues
- [ ] Document known limitations (dynamic SQL, etc.)

### 8.2 Deployment
- [ ] Create PowerShell deployment script
- [ ] Add database migration for lineage schema
- [ ] Configure Azure Service Bus topics for lineage events
- [ ] Set up monitoring for extraction failures
- [ ] Configure alerts for low-confidence extractions

### 8.3 Operationalization
- [ ] Schedule nightly full lineage refresh
- [ ] Set up retention policy for extraction history
- [ ] Create dashboard for lineage coverage metrics
- [ ] Train team on impact analysis workflow

---

## Risk Mitigation Checklist

### High-Risk Items
- [ ] **Stack overflow with large UNION queries** - Implement iterative visitor fallback
- [ ] **Dynamic SQL prevalence** - Audit IRFS1 procedures for sp_executesql usage
- [ ] **Unqualified column ambiguity** - Ensure MasterIndex has complete column metadata
- [ ] **Performance with 27K+ ColumnLineage rows** - Add appropriate indexes
- [ ] **CTE complexity** - Test with nested and recursive CTEs from production

### Fallback Plans
- [ ] If ScriptDom fails → Use sys.sql_expression_dependencies only (object-level)
- [ ] If confidence < 80% → Flag for manual review
- [ ] If extraction timeout → Process in smaller batches

---

## Success Criteria

| Metric | Target | Measurement |
|--------|--------|-------------|
| Column lineage accuracy | ≥97% | Manual validation of 50 columns |
| Extraction success rate | ≥95% | Automated tracking |
| Processing time per proc | <5 seconds | Performance tests |
| Graph traversal latency | <100ms | API response times |
| Test coverage | ≥80% | Code coverage tools |

---

## Dependencies

### External Dependencies
- [ ] `Microsoft.SqlServer.TransactSql.ScriptDom` NuGet
- [ ] SQL Server 2019+ for lineage schema
- [ ] D3.js or React Flow for visualization

### Internal Dependencies
- [ ] MasterIndex table must be populated
- [ ] Column metadata must be complete
- [ ] Stored procedure definitions must be accessible

---

## Notes

- **Start with INSERT/UPDATE** - Most common patterns, highest ROI
- **Defer MERGE complexity** - Lower priority, more edge cases
- **Log everything** - Extraction history critical for debugging
- **Confidence scoring** - Don't hide uncertainty from users
- **Incremental approach** - Extract on-demand before full scan

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-03 | Initial TODO list |
