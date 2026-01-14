# T-SQL ScriptDom & Column-Level Lineage - Curated References

## Overview

This document contains 70+ curated, high-quality references for implementing T-SQL static analysis and column-level lineage extraction. Sources are organized by category and include official documentation, academic papers, community tutorials, and production implementation guides.

---

## Category 1: Microsoft ScriptDom Official Documentation

### 1.1 Core Documentation

| # | Title | URL | Date | Notes |
|---|-------|-----|------|-------|
| 1 | Programmatically Parsing T-SQL with ScriptDom | https://devblogs.microsoft.com/azure-sql/programmatically-parsing-transact-sql-t-sql-with-the-scriptdom-parser/ | Mar 2024 | **Start here** - Official Microsoft walkthrough by Arvind Shyamsundar |
| 2 | ScriptDom Open Source Announcement | https://techcommunity.microsoft.com/blog/azuresqlblog/scriptdom-net-library-for-t-sql-parsing-is-now-open-source/3804284 | Apr 2023 | MIT license, GitHub release announcement |
| 3 | GitHub - microsoft/SqlScriptDOM | https://github.com/microsoft/SqlScriptDOM | Current | Official open source repository |
| 4 | TSqlParser Class Reference | https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom.tsqlparser | Current | API documentation |
| 5 | TSqlFragmentVisitor Reference | https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom.tsqlfragmentvisitor | Current | Visitor pattern base class |

### 1.2 NuGet Packages

| Package | Version | Notes |
|---------|---------|-------|
| Microsoft.SqlServer.TransactSql.ScriptDom | 161.x | Standalone ScriptDom package |
| Microsoft.SqlServer.DacFx | 162.x | Includes ScriptDom + model access |

---

## Category 2: Community Tutorials & Implementation Guides

### 2.1 Getting Started Tutorials

| # | Title | URL | Author | Notes |
|---|-------|-----|--------|-------|
| 6 | Microsoft SQL Server Script DOM | https://www.dbdelta.com/microsoft-sql-server-script-dom/ | Dan Guzman | PowerShell examples, version selection |
| 7 | How to Get Started with ScriptDom | https://the.agilesql.club/2015/11/how-to-get-started-with-the-scriptdom/ | Ed Elliott | Concrete vs Fragment visitor comparison |
| 8 | Parsing T-SQL: ScriptDom vs ANTLR4 | https://dskrzypiec.dev/parsing-tsql/ | Damian Skrzypiec | Comparison with ANTLR alternative |
| 9 | GitHub Gist - TransactSqlScriptDomTest.cs | https://gist.github.com/philippwiddra/2ee47ac4f8a0248c3a0e | Philipp Widdra | Practical C# code samples |

### 2.2 SQLServerCentral Stairway Series

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 10 | Stairway Level 1 - Introduction | https://www.sqlservercentral.com/steps/stairway-to-scriptdom-level-1-an-introduction-to-scriptdom | Overview and setup |
| 11 | Stairway Level 3 - AST Exploration | https://www.sqlservercentral.com/steps/stairway-to-scriptdom-level-3 | Visitor fundamentals |
| 12 | Stairway Level 4 - Multiple Visitors | https://www.sqlservercentral.com/steps/stairway-to-scriptdom-level-4 | Advanced patterns |

### 2.3 GitHub Samples Repository

| # | Resource | URL | Notes |
|---|----------|-----|-------|
| 13 | arvindshmicrosoft/SQLScriptDomSamples | https://github.com/arvindshmicrosoft/SQLScriptDomSamples | Official Microsoft samples |

---

## Category 3: Column-Level Lineage Research

### 3.1 Key Papers & Articles

| # | Title | URL | Date | Key Insights |
|---|-------|-----|------|--------------|
| 14 | Extracting Column-Level Lineage from SQL (DataHub) | https://datahub.com/blog/extracting-column-level-lineage-from-sql/ | Aug 2025 | **Critical**: Schema-aware parsing required, 97-99% accuracy target |
| 15 | LineageX: Column Lineage Extraction System (arXiv) | https://arxiv.org/html/2505.23133v1 | May 2025 | CTEs, subqueries, SELECT * challenges, LLM augmentation |
| 16 | Column-Level Lineage: SQL Parsing Adventure (Metaplane) | https://www.metaplane.dev/blog/column-level-lineage-an-adventure-in-sql-parsing | 2024 | Parser → AST → IR pipeline design |
| 17 | How Does SQLLineage Work | https://sqllineage.readthedocs.io/en/latest/behind_the_scene/how_sqllineage_work.html | 2024 | AST traversal methodology |

### 3.2 Open Source Lineage Tools

| # | Tool | URL | Language | Notes |
|---|------|-----|----------|-------|
| 18 | sqllineage | https://github.com/reata/sqllineage | Python | CLI with column lineage, metadata integration |
| 19 | sqlglot | https://github.com/tobymao/sqlglot | Python | Multi-dialect, built-in lineage module |
| 20 | sqlglot lineage.py | https://github.com/tobymao/sqlglot/blob/main/sqlglot/lineage.py | Python | Lineage implementation source |
| 21 | General SQL Parser | http://support.sqlparser.com/tutorials/gsp-demo-data-lineage/ | Commercial | 20+ databases, XML output |

### 3.3 Commercial & Enterprise Tools

| # | Tool | URL | Notes |
|---|------|-----|-------|
| 22 | SQLFlow | https://www.gudusoft.com/sqlflow/ | 20+ databases, visualization |
| 23 | DataHub SQL Parser | https://docs.datahub.com/docs/lineage/sql_parsing | Built on sqlglot, 97-99% accuracy |
| 24 | MANTA | https://www.getmanta.com/ | Enterprise lineage platform |
| 25 | Dataedo | https://dataedo.com/ | Database documentation with lineage |

---

## Category 4: SQL Server Metadata & Dependencies

### 4.1 System Catalog Views

| # | Resource | URL | Notes |
|---|----------|-----|-------|
| 26 | sys.sql_expression_dependencies | https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-sql-expression-dependencies-transact-sql | Main dependency view |
| 27 | sys.dm_sql_referenced_entities | https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-sql-referenced-entities-transact-sql | Column-level DMF |
| 28 | sys.dm_sql_referencing_entities | https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-sql-referencing-entities-transact-sql | Reverse dependency lookup |
| 29 | sys.columns | https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-columns-transact-sql | Column metadata |
| 30 | INFORMATION_SCHEMA.COLUMNS | https://learn.microsoft.com/en-us/sql/relational-databases/system-information-schema-views/columns-transact-sql | ANSI-standard view |

### 4.2 Dependency Analysis Articles

| # | Title | URL | Author | Notes |
|---|-------|-----|--------|-------|
| 31 | Finding SQL Server Object Dependencies with DMVs | https://www.mssqltips.com/sqlservertip/4868/finding-sql-server-object-dependencies-with-dmvs/ | MSSQLTips | is_caller_dependent handling |
| 32 | Different Ways to Find SQL Server Object Dependencies | https://www.mssqltips.com/sqlservertip/2999/different-ways-to-find-sql-server-object-dependencies/ | MSSQLTips | Cross-database dependencies |
| 33 | Dependencies and References in SQL Server | https://www.red-gate.com/simple-talk/databases/sql-server/t-sql-programming-sql-server/dependencies-and-references-in-sql-server/ | Red-Gate | Soft vs hard dependencies |
| 34 | Where Is that Table Used? | https://www.sommarskog.se/sqlutil/SearchCode.html | Erland Sommarskog | Plan cache approach, AbaPerls |

---

## Category 5: Dynamic SQL & Security

### 5.1 Dynamic SQL Best Practices

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 35 | sp_executesql Documentation | https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-executesql-transact-sql | Parameterized execution |
| 36 | The Curse and Blessings of Dynamic SQL | https://www.sommarskog.se/dynamic_sql.html | Erland Sommarskog - **comprehensive** |
| 37 | Dynamic SQL & SQL Injection Prevention | https://www.datacamp.com/tutorial/dynamic-sql | DataCamp tutorial |
| 38 | QUOTENAME for SQL Injection Prevention | https://www.geeksforgeeks.org/quotename-function-in-sql-server/ | Safe identifier handling |

### 5.2 Security Considerations

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 39 | Execute As Owner Pattern | https://www.sqlserverscience.com/security/grant-permissions-on-sp_executesql/ | Certificate-based permissions |
| 40 | BP013: Dynamic SQL Code Analysis | https://www.red-gate.com/hub/product-learning/sql-prompt/sql-prompt-code-analysis-dynamic-sql-usage | Redgate rule |

---

## Category 6: CTE & Temp Table Handling

### 6.1 CTE Documentation

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 41 | WITH common_table_expression | https://learn.microsoft.com/en-us/sql/t-sql/queries/with-common-table-expression-transact-sql | Official syntax reference |
| 42 | CTE vs Temp Table vs Table Variable Performance | https://www.mssqltips.com/sqlservertip/5118/sql-server-cte-vs-temp-table-vs-table-variable-performance-test/ | Performance comparison |
| 43 | When CTEs Outperform Temp Tables | https://www.brentozar.com/archive/2019/06/whats-better-ctes-or-temp-tables/ | Brent Ozar analysis |
| 44 | Recursive CTEs for Hierarchical Data | https://learn.microsoft.com/en-us/sql/t-sql/queries/with-common-table-expression-transact-sql#guidelines-for-defining-and-using-recursive-common-table-expressions | Recursive patterns |

### 6.2 Temp Table Tracking

| # | Topic | Notes |
|---|-------|-------|
| 45 | Temp tables in procedures | Scope: procedure session, visible in sys.tables (tempdb) |
| 46 | Table variables | Scope: batch, minimal logging, no statistics |
| 47 | Global temp tables (##) | Scope: all sessions, persist until creator disconnects |

---

## Category 7: MERGE Statement Analysis

### 7.1 MERGE Documentation

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 48 | MERGE (Transact-SQL) | https://learn.microsoft.com/en-us/sql/t-sql/statements/merge-transact-sql | Official syntax |
| 49 | SQL Server MERGE to Insert, Update, Delete | https://www.mssqltips.com/sqlservertip/1704/using-merge-in-sql-server-to-insert-update-and-delete-at-the-same-time/ | Comprehensive tutorial |
| 50 | SQL MERGE Performance Comparison | https://www.mssqltips.com/sqlservertip/7590/sql-merge-performance-vs-insert-update-delete/ | 19% slower than separate statements |
| 51 | Understanding SQL MERGE Statement | https://www.sqlshack.com/understanding-the-sql-merge-statement/ | SCD patterns |

---

## Category 8: Graph Storage & Lineage Architecture

### 8.1 Graph Database Approaches

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 52 | Data Lineage is a Graph Problem (Memgraph) | https://memgraph.com/blog/join-the-dots-data-lineage-is-a-graph-problem-heres-why | O(1) traversal vs O(log N) joins |
| 53 | Data Lineage Graph Analysis (Memgraph) | https://memgraph.com/blog/better-data-management-get-solutions-by-analyzing-the-data-lineage-graph | Betweenness centrality, impact analysis |
| 54 | What is Data Lineage (Neo4j) | https://neo4j.com/blog/graph-database/what-is-data-lineage/ | Cypher queries for lineage |
| 55 | Data Lineage with Python NetworkX | https://www.rittmanmead.com/blog/2024/08/data-lineage-analysis-with-python-and-networkx/ | Lightweight Python approach |

### 8.2 Lineage Platform Architecture

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 56 | OpenLineage Specification | https://github.com/OpenLineage/OpenLineage/blob/main/spec/OpenLineage.md | Open standard, facet model |
| 57 | OpenLineage Official Site | https://openlineage.io/ | Marquez reference implementation |
| 58 | OpenLineage for Streaming (Kafka Summit) | https://www.kai-waehner.de/blog/2024/05/13/open-standards-for-data-lineage-openlineage-for-batch-and-streaming/ | Streaming lineage patterns |
| 59 | dbt Lineage Guide | https://www.getdbt.com/blog/getting-started-with-data-lineage | DAG-based lineage |
| 60 | Ultimate Guide to Data Lineage (Monte Carlo) | https://www.montecarlodata.com/blog-data-lineage/ | Enterprise lineage practices |

---

## Category 9: Static Code Analysis & Linting

### 9.1 SSDT Code Analysis

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 61 | Code Analysis Rules Extensibility | https://learn.microsoft.com/en-us/sql/ssdt/overview-of-extensibility-for-database-code-analysis-rules | Creating custom rules |
| 62 | Walkthrough: Custom Static Code Analysis Rule | https://learn.microsoft.com/en-us/sql/ssdt/walkthrough-author-custom-static-code-analysis-rule-assembly | Step-by-step tutorial |
| 63 | SSDT Analysis Extensions | https://the.agilesql.club/2014/12/enforcing-t-sql-quality-with-ssdt-analysis-extensions/ | Ed Elliott's guide |
| 64 | DACPAC Code Analysis Rules Review | https://erikdarling.com/reviewing-the-new-dacpac-code-analysis-rules-for-t-sql/ | Erik Darling analysis |

### 9.2 Open Source Linters

| # | Tool | URL | Notes |
|---|------|-----|-------|
| 65 | SqlServer.Rules | https://github.com/tcartwright/sqlserver.rules | SSDT extended rules |
| 66 | TSqlRules | https://github.com/ashleyglee/TSqlRules | Custom SSDT rules |
| 67 | SQLFluff | https://docs.sqlfluff.com/en/stable/ | Multi-dialect linter |
| 68 | tsqllint | https://github.com/tsqllint/tsqllint | T-SQL specific linter |

### 9.3 Analysis Tools Overview

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 69 | Database Code Analysis (Red-Gate) | https://www.red-gate.com/simple-talk/devops/database-devops/database-code-analysis/ | Static vs dynamic analysis |
| 70 | 22 SQL Static Analysis Tools | https://www.analysis-tools.dev/tag/sql | Tool comparison |

---

## Category 10: AST & Compiler Theory

### 10.1 Visitor Pattern

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 71 | Visiting an Abstract Syntax Tree | https://patshaughnessy.net/2022/1/22/visiting-an-abstract-syntax-tree | accept() and visit() methods |
| 72 | AST and Visitor Patterns (CS453) | https://www.cs.colostate.edu/~cs453/yr2014/Slides/10-AST-visitor.ppt.pdf | Academic slides |
| 73 | Visitor Pattern Tutorial (Washington U) | https://www.cs.wustl.edu/~cytron/cacweb/Tutorial/Visitor/ | Multiple dispatch concept |

### 10.2 AST Fundamentals

| # | Title | URL | Notes |
|---|-------|-----|-------|
| 74 | Abstract Syntax Tree (Wikipedia) | https://en.wikipedia.org/wiki/Abstract_syntax_tree | Overview and properties |
| 75 | Eclipse JDT AST Processing | https://academicworks.cuny.edu/ | Enterprise AST APIs |

---

## Category 11: Known Issues & Limitations

### 11.1 ScriptDom Issues

| # | Issue | URL | Notes |
|---|-------|-----|-------|
| 76 | Stack Overflow with Large UNIONs | https://github.com/microsoft/SqlScriptDOM/issues/49 | Use iterative processing |
| 77 | Quoted Identifier Handling | Various | Set initialQuotedIdentifiers appropriately |

### 11.2 Metadata Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| Dynamic SQL | Cannot analyze runtime-generated SQL | Log warning, partial lineage |
| Cross-database | Limited column-level info | Query sys.sql_expression_dependencies |
| Temp tables | Not in system metadata | Track during parsing |
| Table variables | No statistics, limited metadata | Track during parsing |

---

## Quick Reference: Essential Reading Order

For implementing column-level lineage extraction, read these in order:

1. **#1** - Azure SQL DevBlog ScriptDom tutorial (fundamentals)
2. **#7** - Agile SQL Club getting started (visitor pattern choice)
3. **#14** - DataHub column lineage article (schema-aware parsing requirement)
4. **#16** - Metaplane SQL parsing adventure (architecture decisions)
5. **#31** - MSSQLTips DMV dependencies (SQL Server metadata)
6. **#52** - Memgraph graph problem article (storage decisions)
7. **#61** - Microsoft code analysis extensibility (custom rules)

---

## Version Information

| Field | Value |
|-------|-------|
| Document Version | 1.0.0 |
| Last Updated | 2026-01-03 |
| Total References | 77 |
| Research Hours | 4+ |

---

## Notes for Implementation

### Critical Success Factors

1. **Schema metadata is required** - Pure parsing cannot resolve unqualified columns
2. **Iterative processing for large SQL** - Avoid stack overflow with recursive visitors
3. **Dynamic SQL creates lineage gaps** - Log warnings, don't fail silently
4. **CTE resolution order matters** - Process WITH clause before main query
5. **Temp table tracking** - Build schema from CREATE TABLE statements
6. **Confidence scoring** - Account for uncertainty in ambiguous cases

### Recommended Architecture

```
SQL Source → ScriptDom Parser → AST
                                 ↓
                          Schema Provider ← SQL Server Metadata
                                 ↓
                          Lineage Visitor → Column Resolution
                                 ↓
                          Graph Storage → Impact Analysis API
```
