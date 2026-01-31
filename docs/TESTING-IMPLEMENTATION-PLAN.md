# Testing Implementation Plan

**Status:** Ready for Implementation
**Created:** 2026-01-31

---

## Sequential Thinking: What We Need

### Step 1: Identify True Gaps (Not Theoretical Coverage)

| Component | Current Tests | Actual Risk | Priority |
|-----------|--------------|-------------|----------|
| **Governance PII Detection** | 0 | Compliance/Legal | P0 |
| **Governance Security Engine** | 0 | Security breach | P0 |
| **API Controllers** | 0 (disabled) | Input validation | P1 |
| **Command Handlers** | 0 | Business logic | P1 |
| **Domain (existing)** | ~200 | Already covered | Done |

### Step 2: Tests Written (This Session)

#### Governance PII Detector Tests - 45 tests
Location: `tests/Unit/Governance/GovernancePIIDetectorTests.cs`

| Category | Count | Purpose |
|----------|-------|---------|
| Email True Positives | 8 | Detect valid emails |
| Email True Negatives | 10 | Avoid false positives |
| SSN True Positives | 4 | Detect SSN format |
| SSN True Negatives | 6 | Avoid phone confusion |
| Phone True Positives | 4 | Detect phone formats |
| Credit Card Positives | 7 | Detect card numbers |
| Address Detection | 8 | Street/ZIP patterns |
| DOB Detection | 4 | Date formats |
| Person Name Detection | 4 | Name patterns |
| Column Boosting | 8 | Confidence boost |
| Edge Cases | 3 | Null/empty handling |
| Classification | 12 | Column classification |

#### Governance Security Engine Tests - 40 tests
Location: `tests/Unit/Governance/GovernanceSecurityEngineTests.cs`

| Category | Count | Purpose |
|----------|-------|---------|
| Valid Queries | 9 | True negatives - no false positives |
| SQL Injection | 8 | Critical attack detection |
| Non-SELECT | 8 | DDL/DML blocking |
| System Tables | 6 | Catalog access blocking |
| Data Exfiltration | 3 | xp_cmdshell, OPENROWSET |
| Performance Attacks | 2 | WAITFOR, BENCHMARK |
| Query Complexity | 3 | JOIN/subquery limits |
| Concurrent Safety | 1 | Thread safety |

---

## Step 3: Remaining High-Priority Tests

### P1: Controller Tests (~30 tests)

```csharp
// tests/Integration/Controllers/DocumentsControllerTests.cs
public class DocumentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    // POST /api/documents
    [Fact] CreateDocument_ValidInput_Returns201Created
    [Fact] CreateDocument_EmptyTitle_Returns400BadRequest
    [Fact] CreateDocument_MissingCategory_Returns400BadRequest

    // GET /api/documents/{id}
    [Fact] GetDocument_ExistingId_Returns200WithDocument
    [Fact] GetDocument_NonExistentId_Returns404NotFound
    [Fact] GetDocument_InvalidIdFormat_Returns400BadRequest

    // GET /api/documents/search
    [Fact] SearchDocuments_NoParams_ReturnsFirstPage
    [Fact] SearchDocuments_WithPagination_ReturnsCorrectPage
    [Fact] SearchDocuments_InvalidPageNumber_Returns400

    // PUT /api/documents/{id}
    [Fact] UpdateDocument_ValidInput_Returns200
    [Fact] UpdateDocument_IdMismatch_Returns400
    [Fact] UpdateDocument_NonExistent_Returns404

    // POST /api/documents/{id}/approve
    [Fact] ApproveDocument_ValidTransition_Returns200
    [Fact] ApproveDocument_InvalidTransition_Returns400
}
```

### P1: Command Handler Tests (~25 tests)

```csharp
// tests/Integration/Handlers/CreateDocumentCommandHandlerTests.cs
[Fact] Handle_ValidCommand_CreatesDocumentWithCorrectProperties
[Fact] Handle_WithTemplate_InheritsSecurityClassification
[Fact] Handle_ValidationFails_ThrowsValidationException
[Fact] Handle_UnauthorizedUser_ThrowsForbidden

// tests/Integration/Handlers/ApproveDocumentCommandHandlerTests.cs
[Fact] Handle_PendingDocument_TransitionsToApproved
[Fact] Handle_AlreadyApproved_ThrowsInvalidOperation
[Fact] Handle_InsufficientClearance_ThrowsForbidden
```

---

## Step 4: Test Infrastructure (Already Configured)

### Existing Setup
- xUnit 2.9.0
- Moq 4.20.70
- FluentAssertions 6.12.0
- Coverlet for coverage
- WebApplicationFactory for integration tests

### Pattern to Follow
From existing `SecurityClassificationTests.cs`:
```csharp
[Theory]
[InlineData("Public", 0)]
[InlineData("Internal", 1)]
public void SecurityLevel_ShouldReturnCorrectNumericValue(string level, int expected)
{
    // Arrange
    SecurityClassification classification = level switch { ... };

    // Act & Assert
    classification.SecurityLevel.Should().Be(expected);
}
```

---

## Summary: Realistic Test Count

| Category | Written | Remaining | Total |
|----------|---------|-----------|-------|
| Existing Domain Tests | 200 | 0 | 200 |
| Governance PII | 45 | 0 | 45 |
| Governance Security | 40 | 0 | 40 |
| Controllers | 0 | 30 | 30 |
| Command Handlers | 0 | 25 | 25 |
| Query Handlers | 0 | 15 | 15 |
| **Total** | **285** | **70** | **~355** |

This is realistic - not 500 tests, but focused coverage on actual risk areas.
