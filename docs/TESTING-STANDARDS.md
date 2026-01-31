# Enterprise Documentation Platform - Comprehensive Testing Standards

**Version:** 1.0
**Created:** 2026-01-31
**Status:** Implementation Ready

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Testing Philosophy & Principles](#2-testing-philosophy--principles)
3. [Test Pyramid Strategy](#3-test-pyramid-strategy)
4. [Avoiding False Positives](#4-avoiding-false-positives)
5. [Test Categories & Standards](#5-test-categories--standards)
6. [Infrastructure & Tooling](#6-infrastructure--tooling)
7. [Layer-Specific Testing Guidelines](#7-layer-specific-testing-guidelines)
8. [Implementation Roadmap](#8-implementation-roadmap)
9. [Code Examples & Templates](#9-code-examples--templates)

---

## 1. Executive Summary

### Current State
- **Coverage:** ~4% (5 test files, ~200 tests)
- **Tested:** Domain value objects, entities, validation service
- **Untested:** API controllers, command/query handlers, governance, repositories, services

### Target State
- **Coverage:** 80% overall, 90% for security-critical code, 100% for PII handling
- **Test Distribution:** 70% unit, 25% integration, 5% E2E
- **Estimated Tests Needed:** 400-500 additional tests

### Key Principles (Cross-Referenced with Project Context)
1. **Deterministic Tests** - No flaky tests, no false positives
2. **Real Database for Integration** - Use TestContainers, not InMemory (prevents SQL behavior false positives)
3. **Vertical Slice Testing** - Test complete flows through MediatR pipeline
4. **Isolation** - Each test is self-contained with no shared mutable state
5. **Consistency** - Follow existing AAA pattern and FluentAssertions style

---

## 2. Testing Philosophy & Principles

### 2.1 Determinism First

Every test must produce the same result every time, regardless of:
- Execution order
- Parallel execution
- Time of day
- Environment

**Anti-Patterns to Avoid:**
```csharp
// BAD: Non-deterministic time dependency
var document = new Document(...);
document.CreatedAt.Should().Be(DateTime.UtcNow); // Flaky!

// GOOD: Use tolerance or freeze time
document.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

// BETTER: Inject time provider for complete control
_timeProvider.SetUtcNow(new DateTime(2026, 1, 31, 12, 0, 0));
document.CreatedAt.Should().Be(new DateTime(2026, 1, 31, 12, 0, 0));
```

### 2.2 Test Isolation

Each test must:
- Create its own test data
- Clean up after itself (or use transaction rollback)
- Not depend on other tests' side effects
- Not share mutable state

**Project-Specific Pattern:**
```csharp
// Use the existing CreateTestDocument() pattern from DocumentTests.cs
private Document CreateTestDocument()
{
    return new Document(
        DocumentId.New(),  // Always new ID - no shared state
        "Test Document",
        "Technical",
        SecurityClassification.Internal(),
        UserId.New());
}
```

### 2.3 Meaningful Assertions

Tests should verify behavior, not implementation details.

**Anti-Pattern:**
```csharp
// BAD: Testing implementation details
_mockRepository.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
_mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
// This tests HOW, not WHAT
```

**Preferred Pattern:**
```csharp
// GOOD: Testing behavior/outcome
var result = await _handler.Handle(command, CancellationToken.None);

result.Should().NotBeNull();
result.Id.Should().NotBeEmpty();
result.Status.Should().Be("Draft");

// Verify the document actually exists (integration test)
var savedDocument = await _repository.GetByIdAsync(result.Id);
savedDocument.Should().NotBeNull();
```

---

## 3. Test Pyramid Strategy

### Distribution (Aligned with Project Architecture)

```
         /\
        /  \   E2E Tests (5%)
       /    \  - Complete workflows through API
      /------\
     /        \  Integration Tests (25%)
    /          \ - Handler + Repository + DB
   /            \ - API Controllers + Full Pipeline
  /--------------\
 /                \ Unit Tests (70%)
/                  \ - Domain entities, value objects
/                    \ - Validators, specifications
/______________________\ - Business rules, governance logic
```

### Layer Mapping

| Layer | Test Type | Test Location | Database |
|-------|-----------|---------------|----------|
| Domain Entities | Unit | `Tests.Unit/Entities/` | None |
| Value Objects | Unit | `Tests.Unit/ValueObjects/` | None |
| Domain Services | Unit | `Tests.Unit/Services/` | None |
| Specifications | Unit | `Tests.Unit/Specifications/` | None |
| FluentValidation | Unit | `Tests.Unit/Validators/` | None |
| Governance | Unit | `Tests.Unit/Governance/` | None |
| Command Handlers | Integration | `Tests.Integration/Handlers/` | TestContainers |
| Query Handlers | Integration | `Tests.Integration/Handlers/` | TestContainers |
| Repositories | Integration | `Tests.Integration/Repositories/` | TestContainers |
| API Controllers | Integration | `Tests.Integration/Controllers/` | TestContainers |
| MediatR Pipeline | Integration | `Tests.Integration/Pipeline/` | TestContainers |
| Workflows | E2E | `Tests.E2E/Workflows/` | TestContainers |

---

## 4. Avoiding False Positives

### 4.1 Database Testing Strategy

**Critical Decision: Do NOT use EF Core InMemory Provider for Integration Tests**

The InMemory provider causes false positives because:
1. **Case sensitivity differs** - SQL Server is case-insensitive, InMemory is case-sensitive
2. **Lazy loading behavior** - InMemory doesn't throw on missing `.Include()`
3. **Query translation** - No LINQ-to-SQL translation validation
4. **Constraint enforcement** - Unique constraints, foreign keys may not behave the same

**Solution: Use TestContainers with SQL Server**

```csharp
// Add to Tests.Integration.csproj
<PackageReference Include="Testcontainers.MsSql" Version="3.10.0" />
```

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong@Passw0rd")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Run migrations
        var options = new DbContextOptionsBuilder<DocumentationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var context = new DocumentationDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

### 4.2 Time-Related False Positives

**Problem:** Tests using `DateTime.UtcNow` can fail due to timing.

**Solution:** Use `TimeProvider` abstraction (new in .NET 8):

```csharp
// In production code
public class DocumentValidationService
{
    private readonly TimeProvider _timeProvider;

    public DocumentValidationService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsExpired(Document document)
    {
        return document.ExpiresAt < _timeProvider.GetUtcNow();
    }
}

// In tests
[Fact]
public void IsExpired_WhenPastExpiration_ReturnsTrue()
{
    // Arrange
    var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero));
    var service = new DocumentValidationService(fakeTime);
    var document = CreateDocumentExpiringAt(new DateTime(2026, 1, 30)); // Yesterday

    // Act
    var result = service.IsExpired(document);

    // Assert
    result.Should().BeTrue();
}
```

### 4.3 Async/Concurrent False Positives

**Problem:** Race conditions in tests cause intermittent failures.

**Solutions:**

```csharp
// BAD: Race condition possible
var task1 = _service.ProcessAsync(item1);
var task2 = _service.ProcessAsync(item2);
await Task.WhenAll(task1, task2);
// Assertions may fail due to timing

// GOOD: Use proper synchronization or test sequentially for state verification
await _service.ProcessAsync(item1);
await _service.ProcessAsync(item2);

// Or use explicit synchronization primitives in tests
var semaphore = new SemaphoreSlim(1, 1);
// ... controlled concurrent testing
```

### 4.4 External Service False Positives

**Problem:** Tests depending on external services (OpenAI, Teams, Azure Service Bus) are flaky.

**Solution:** Use interface mocking consistently:

```csharp
// All external services have interfaces - mock them
public class AutoDraftServiceTests
{
    private readonly Mock<IOpenAIEnhancementService> _mockOpenAI;
    private readonly Mock<ITemplateExecutorService> _mockTemplateExecutor;
    private readonly Mock<IDocIdGeneratorService> _mockDocIdGenerator;

    public AutoDraftServiceTests()
    {
        _mockOpenAI = new Mock<IOpenAIEnhancementService>();
        _mockTemplateExecutor = new Mock<ITemplateExecutorService>();
        _mockDocIdGenerator = new Mock<IDocIdGeneratorService>();

        // Configure deterministic responses
        _mockOpenAI
            .Setup(x => x.EnhanceDocumentationAsync(It.IsAny<DocumentationEnhancementRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnhancedDocumentation { Summary = "Enhanced content", Tags = new[] { "test" } });
    }
}
```

### 4.5 Governance/PII Detection False Positives

**Critical for this project:** The PII detection uses regex patterns with confidence scores.

**Problem:** Pattern matching can have false positives/negatives.

**Solution:** Comprehensive test data with known outcomes:

```csharp
public class GovernancePIIDetectorTests
{
    [Theory]
    [InlineData("john.doe@example.com", PIIType.Email, true, 0.95)]
    [InlineData("not-an-email", PIIType.Email, false, 0.0)]
    [InlineData("john@", PIIType.Email, false, 0.0)]  // Incomplete email
    [InlineData("123-45-6789", PIIType.SSN, true, 0.98)]
    [InlineData("123-456-7890", PIIType.SSN, false, 0.0)]  // Phone, not SSN
    [InlineData("4111111111111111", PIIType.CreditCard, true, 0.95)]  // Visa test number
    [InlineData("1234567890123456", PIIType.CreditCard, false, 0.0)]  // Invalid Luhn
    public async Task DetectPIIAsync_WithKnownPatterns_ReturnsExpectedResult(
        string value, PIIType expectedType, bool shouldDetect, double minConfidence)
    {
        // Arrange
        var detector = new GovernancePIIDetector();

        // Act
        var result = await detector.DetectPIIAsync("test_column", value, CancellationToken.None);

        // Assert
        if (shouldDetect)
        {
            result.PIIDetected.Should().BeTrue();
            result.DetectedTypes.Should().Contain(expectedType);
            result.Confidence.Should().BeGreaterOrEqualTo(minConfidence);
        }
        else
        {
            result.DetectedTypes.Should().NotContain(expectedType);
        }
    }
}
```

---

## 5. Test Categories & Standards

### 5.1 Unit Tests

**Characteristics:**
- No external dependencies (database, file system, network)
- Fast execution (< 100ms per test)
- Test single units of behavior
- Use mocks for dependencies

**Naming Convention (existing pattern):**
```
[MethodName]_[Scenario]_Should[ExpectedBehavior]
```

**Examples:**
```csharp
Constructor_WithValidParameters_ShouldCreateDocument
ValidateTitle_WithEmptyTitle_ShouldThrowArgumentException
CanTransitionTo_FromRejectedToPending_ShouldReturnTrue
```

**Required Coverage:**
| Component | Minimum Coverage |
|-----------|-----------------|
| Domain Entities | 80% |
| Value Objects | 90% |
| Domain Services | 80% |
| Specifications | 90% |
| Validators | 100% |
| Governance Logic | 90% |

### 5.2 Integration Tests

**Characteristics:**
- Test component interactions
- Use real database (TestContainers)
- Test MediatR pipeline end-to-end
- Test repository implementations

**Naming Convention:**
```
[Feature]_[Scenario]_Should[ExpectedBehavior]
```

**Examples:**
```csharp
CreateDocument_WithValidCommand_ShouldPersistDocument
SearchDocuments_WithPaginationParameters_ShouldReturnPagedResults
ApproveDocument_WithInsufficientClearance_ShouldThrowForbiddenAccess
```

**Required Coverage:**
| Component | Minimum Coverage |
|-----------|-----------------|
| Command Handlers | 80% |
| Query Handlers | 80% |
| Repositories | 70% |
| API Controllers | 80% |
| Pipeline Behaviors | 90% |

### 5.3 E2E Tests

**Characteristics:**
- Test complete user workflows
- API-level testing (not UI)
- Test authentication/authorization flows
- Verify cross-cutting concerns

**Naming Convention:**
```
[Workflow]_[Scenario]_Should[BusinessOutcome]
```

**Examples:**
```csharp
DocumentApprovalWorkflow_WhenAllStepsComplete_ShouldPublishDocument
BatchProcessing_WithMultipleDocuments_ShouldCompleteAllSuccessfully
GovernanceValidation_WithPIIData_ShouldMaskAppropriately
```

**Scope:** Critical paths only (5-10% of test suite)

---

## 6. Infrastructure & Tooling

### 6.1 Package Dependencies

**Current (keep):**
```xml
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
<PackageReference Include="coverlet.collector" Version="6.0.2" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.8" />
```

**Add for enhanced testing:**
```xml
<!-- TestContainers for real database testing -->
<PackageReference Include="Testcontainers.MsSql" Version="3.10.0" />

<!-- Time testing -->
<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="8.0.1" />

<!-- HTTP mocking for external services -->
<PackageReference Include="RichardSzalay.MockHttp" Version="7.0.0" />

<!-- Bogus for test data generation -->
<PackageReference Include="Bogus" Version="35.6.1" />

<!-- Respawn for database cleanup -->
<PackageReference Include="Respawn" Version="6.2.1" />
```

### 6.2 Test Project Structure

```
tests/
├── Unit/
│   ├── TestBase.cs                      # (existing) Logger mocking utilities
│   ├── TestDataBuilders/                # NEW: Fluent test data builders
│   │   ├── DocumentBuilder.cs
│   │   ├── UserBuilder.cs
│   │   └── TemplateBuilder.cs
│   ├── Entities/
│   │   └── DocumentTests.cs             # (existing)
│   ├── ValueObjects/
│   │   ├── ApprovalStatusTests.cs       # (existing)
│   │   └── SecurityClassificationTests.cs # (existing)
│   ├── Services/
│   │   ├── DocumentValidationServiceTests.cs  # (existing)
│   │   ├── DocumentGenerationServiceTests.cs  # NEW
│   │   └── TemplateValidationServiceTests.cs  # NEW
│   ├── Specifications/                   # NEW
│   │   └── DocumentSpecificationsTests.cs
│   ├── Validators/                       # NEW
│   │   ├── CreateDocumentCommandValidatorTests.cs
│   │   └── UpdateDocumentCommandValidatorTests.cs
│   └── Governance/                       # NEW
│       ├── GovernancePIIDetectorTests.cs
│       ├── GovernanceSecurityEngineTests.cs
│       └── GovernanceAuthorizationEngineTests.cs
│
├── Integration/
│   ├── Fixtures/                         # NEW
│   │   ├── DatabaseFixture.cs           # TestContainers setup
│   │   ├── IntegrationTestBase.cs       # Base class with common setup
│   │   └── TestWebApplicationFactory.cs # Custom WebApplicationFactory
│   ├── Handlers/                         # NEW
│   │   ├── Commands/
│   │   │   ├── CreateDocumentCommandHandlerTests.cs
│   │   │   ├── UpdateDocumentCommandHandlerTests.cs
│   │   │   └── ApproveDocumentCommandHandlerTests.cs
│   │   └── Queries/
│   │       ├── GetDocumentQueryHandlerTests.cs
│   │       └── SearchDocumentsQueryHandlerTests.cs
│   ├── Controllers/                      # Revive existing disabled tests
│   │   └── DocumentsControllerTests.cs
│   ├── Repositories/                     # NEW
│   │   ├── DocumentRepositoryTests.cs
│   │   └── SpecificationTests.cs
│   └── Pipeline/                         # NEW
│       ├── ValidationBehaviorTests.cs
│       ├── AuthorizationBehaviorTests.cs
│       └── LoggingBehaviorTests.cs
│
└── E2E/                                  # NEW
    ├── E2ETestBase.cs
    └── Workflows/
        ├── DocumentLifecycleTests.cs
        ├── ApprovalWorkflowTests.cs
        └── BatchProcessingTests.cs
```

### 6.3 Test Configuration

**Create `tests/Integration/Fixtures/IntegrationTestBase.cs`:**

```csharp
using Testcontainers.MsSql;
using Microsoft.EntityFrameworkCore;
using Respawn;

[Collection("Database")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly MsSqlContainer _dbContainer;
    protected DocumentationDbContext _dbContext = null!;
    protected IServiceProvider _serviceProvider = null!;
    private Respawner _respawner = null!;

    protected IntegrationTestBase()
    {
        _dbContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("TestPassword123!")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<DocumentationDbContext>();
        await _dbContext.Database.MigrateAsync();

        _respawner = await Respawner.CreateAsync(_dbContainer.GetConnectionString(), new RespawnerOptions
        {
            TablesToIgnore = new[] { "__EFMigrationsHistory" }
        });
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<DocumentationDbContext>(options =>
            options.UseSqlServer(_dbContainer.GetConnectionString()));

        // Register repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUnitOfWork, SimpleUnitOfWork>();

        // Register MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateDocumentCommand).Assembly));

        // Register validators
        services.AddValidatorsFromAssembly(typeof(CreateDocumentCommandValidator).Assembly);

        // Mock external services
        services.AddSingleton(Mock.Of<IMessageBus>());
        services.AddSingleton(Mock.Of<IOpenAIEnhancementService>());
        services.AddSingleton(Mock.Of<ITeamsNotificationService>());
    }

    protected async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
```

---

## 7. Layer-Specific Testing Guidelines

### 7.1 Domain Layer (Unit Tests)

#### Entities

**What to Test:**
- Constructor validation (null checks, boundary values)
- State transitions (status changes, workflow)
- Domain events raised correctly
- Business rule enforcement

**Pattern (from existing DocumentTests.cs):**
```csharp
[Fact]
public void UpdateApprovalStatus_WithValidTransition_ShouldUpdateStatusAndRaiseEvent()
{
    // Arrange
    var document = CreateTestDocument();
    document.ClearDomainEvents();
    var newStatus = ApprovalStatus.Approved(UserId.New());

    // Act
    document.UpdateApprovalStatus(newStatus);

    // Assert
    document.ApprovalStatus.Should().Be(newStatus);
    document.DomainEvents.Should().HaveCount(1);
    document.DomainEvents.First().Should().BeOfType<DocumentApprovalStatusChangedEvent>();
}
```

#### Value Objects

**What to Test:**
- Factory methods produce correct values
- Equality semantics (same values = equal)
- Immutability (no mutation methods)
- Business rules (e.g., security level transitions)

**Pattern (from existing SecurityClassificationTests.cs):**
```csharp
[Theory]
[InlineData("Public", 0)]
[InlineData("Internal", 1)]
[InlineData("Confidential", 2)]
[InlineData("Restricted", 3)]
public void SecurityLevel_ShouldFollowHierarchy(string level, int expectedLevel)
{
    var classification = level switch
    {
        "Public" => SecurityClassification.Public(),
        "Internal" => SecurityClassification.Internal(),
        "Confidential" => SecurityClassification.Confidential(new[] { "Group1" }),
        "Restricted" => SecurityClassification.Restricted(new[] { "Group1" }),
        _ => throw new ArgumentException()
    };

    classification.SecurityLevel.Should().Be(expectedLevel);
}
```

#### Specifications

**What to Test:**
- `IsSatisfiedBy()` returns correct boolean for various entities
- `ToExpression()` produces valid Expression
- Composition (And, Or, Not) works correctly

```csharp
[Fact]
public void DocumentsRequiringApprovalSpecification_WithPendingStatus_ShouldBeSatisfied()
{
    // Arrange
    var spec = new DocumentsRequiringApprovalSpecification();
    var document = CreateTestDocument();
    document.UpdateApprovalStatus(ApprovalStatus.Pending());

    // Act
    var result = spec.IsSatisfiedBy(document);

    // Assert
    result.Should().BeTrue();
}

[Fact]
public void CombinedSpecification_WithAndOperator_ShouldRequireBothConditions()
{
    // Arrange
    var spec1 = new DocumentsRequiringApprovalSpecification();
    var spec2 = new DocumentsByAuthorSpecification(_testUserId);
    var combinedSpec = spec1.And(spec2);

    var matchingDocument = CreateTestDocumentByAuthor(_testUserId);
    matchingDocument.UpdateApprovalStatus(ApprovalStatus.Pending());

    var nonMatchingDocument = CreateTestDocumentByAuthor(UserId.New());

    // Act & Assert
    combinedSpec.IsSatisfiedBy(matchingDocument).Should().BeTrue();
    combinedSpec.IsSatisfiedBy(nonMatchingDocument).Should().BeFalse();
}
```

### 7.2 Application Layer

#### Validators (Unit Tests)

**What to Test:**
- Each validation rule independently
- Error messages are correct
- Conditional validations trigger appropriately

```csharp
public class CreateDocumentCommandValidatorTests
{
    private readonly CreateDocumentCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyTitle_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateDocumentCommand(
            Title: "",
            Category: "Technical",
            ContentType: "markdown",
            Tags: new List<string>());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage.Contains("required"));
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("html")]
    [InlineData("text")]
    [InlineData("json")]
    public void Validate_WithValidContentType_ShouldPass(string contentType)
    {
        var command = new CreateDocumentCommand(
            Title: "Valid Title",
            Category: "Technical",
            ContentType: contentType,
            Tags: new List<string>());

        var result = _validator.Validate(command);

        result.Errors.Should().NotContain(e => e.PropertyName == "ContentType");
    }
}
```

#### Command Handlers (Integration Tests)

**What to Test:**
- Command execution creates expected state
- Validation pipeline rejects invalid input
- Authorization is enforced
- Domain events are raised
- Database state is correct after execution

```csharp
public class CreateDocumentCommandHandlerTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateDocumentInDatabase()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var command = new CreateDocumentCommand(
            Title: "Integration Test Document",
            Category: "Technical",
            ContentType: "markdown",
            Tags: new List<string> { "test", "integration" },
            CreatedBy: UserId.New());

        // Act
        var result = await mediator.Send(command);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        // Verify database state
        var savedDocument = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == DocumentId.From(result.Id));

        savedDocument.Should().NotBeNull();
        savedDocument!.Title.Should().Be("Integration Test Document");
        savedDocument.Category.Should().Be("Technical");
        savedDocument.Tags.Should().BeEquivalentTo(new[] { "test", "integration" });
    }

    [Fact]
    public async Task Handle_WithInvalidTitle_ShouldThrowValidationException()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var command = new CreateDocumentCommand(
            Title: "",  // Invalid - empty
            Category: "Technical",
            ContentType: "markdown",
            Tags: new List<string>(),
            CreatedBy: UserId.New());

        // Act
        Func<Task> act = () => mediator.Send(command);

        // Assert
        await act.Should().ThrowAsync<ApplicationValidationException>()
            .Where(e => e.Errors.ContainsKey("Title"));
    }
}
```

#### Query Handlers (Integration Tests)

```csharp
public class SearchDocumentsQueryHandlerTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange - Seed 25 documents
        for (int i = 1; i <= 25; i++)
        {
            await SeedDocumentAsync($"Document {i}");
        }

        var query = new SearchDocumentsQuery(
            SearchTerm: null,
            PageNumber: 2,
            PageSize: 10);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.HasNextPage.Should().BeTrue();  // Page 3 exists with 5 items
    }
}
```

### 7.3 API Layer (Integration Tests)

**Use WebApplicationFactory with TestContainers:**

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly MsSqlContainer _dbContainer;

    public TestWebApplicationFactory(MsSqlContainer dbContainer)
    {
        _dbContainer = dbContainer;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<DocumentationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add test database
            services.AddDbContext<DocumentationDbContext>(options =>
                options.UseSqlServer(_dbContainer.GetConnectionString()));

            // Mock external services
            services.AddSingleton(Mock.Of<IMessageBus>());
            services.AddSingleton(Mock.Of<IOpenAIEnhancementService>());
            services.AddSingleton(Mock.Of<ITeamsNotificationService>());
            services.AddSingleton(Mock.Of<IBackgroundJobClient>());
        });
    }
}

public class DocumentsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentsControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDocument_WithValidPayload_ReturnsCreated()
    {
        // Arrange
        var payload = new
        {
            title = "API Test Document",
            category = "Technical",
            contentType = "markdown",
            tags = new[] { "api", "test" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/documents", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DocumentDto>();
        result.Should().NotBeNull();
        result!.Title.Should().Be("API Test Document");
    }

    [Fact]
    public async Task GetDocument_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/documents/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

### 7.4 Governance Layer (Unit Tests - Critical)

**PII Detection (requires comprehensive test data):**

```csharp
public class GovernancePIIDetectorTests
{
    private readonly GovernancePIIDetector _detector = new();

    public static IEnumerable<object[]> EmailTestData()
    {
        // True positives - should detect
        yield return new object[] { "john.doe@example.com", true, PIIType.Email, 0.95 };
        yield return new object[] { "user+tag@subdomain.example.co.uk", true, PIIType.Email, 0.95 };
        yield return new object[] { "test.email.123@company.org", true, PIIType.Email, 0.95 };

        // True negatives - should NOT detect
        yield return new object[] { "not-an-email", false, PIIType.Email, 0.0 };
        yield return new object[] { "john@", false, PIIType.Email, 0.0 };
        yield return new object[] { "@example.com", false, PIIType.Email, 0.0 };
        yield return new object[] { "john doe at example dot com", false, PIIType.Email, 0.0 };
    }

    [Theory]
    [MemberData(nameof(EmailTestData))]
    public async Task DetectPII_EmailPatterns_MatchesExpected(
        string value, bool shouldDetect, PIIType type, double minConfidence)
    {
        var result = await _detector.DetectPIIAsync("email_column", value, CancellationToken.None);

        if (shouldDetect)
        {
            result.PIIDetected.Should().BeTrue();
            result.DetectedTypes.Should().Contain(type);
            result.Confidence.Should().BeGreaterOrEqualTo(minConfidence);
        }
        else
        {
            result.DetectedTypes.Should().NotContain(type);
        }
    }

    // Similar comprehensive tests for SSN, Credit Card, Phone, etc.
}
```

**Security Engine (SQL Injection Detection):**

```csharp
public class GovernanceSecurityEngineTests
{
    private readonly GovernanceSecurityEngine _engine = new();

    [Theory]
    [InlineData("SELECT * FROM Documents WHERE Id = 1", true, "Valid simple query")]
    [InlineData("SELECT * FROM Documents WHERE Id = 1; DROP TABLE Users;--", false, "SQL injection - DDL")]
    [InlineData("SELECT * FROM Documents WHERE 1=1 OR 'a'='a'", false, "SQL injection - boolean")]
    [InlineData("SELECT * FROM Documents UNION SELECT * FROM Users", false, "SQL injection - UNION")]
    [InlineData("SELECT * FROM sys.objects", false, "System catalog access")]
    [InlineData("SELECT * FROM Documents; EXEC xp_cmdshell 'dir'", false, "Command execution")]
    public async Task ValidateQuerySecurity_WithVariousQueries_ReturnsExpectedResult(
        string query, bool shouldPass, string testCase)
    {
        // Arrange
        var request = new GovernanceQueryRequest
        {
            Query = query,
            AgentId = "test-agent",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _engine.ValidateQuerySecurityAsync(request, CancellationToken.None);

        // Assert
        result.IsValid.Should().Be(shouldPass, because: testCase);
    }
}
```

---

## 8. Implementation Roadmap

### Phase 1: Foundation (Week 1-2)

**Priority: Critical Infrastructure**

| Task | Type | Est. Tests | Files |
|------|------|------------|-------|
| Set up TestContainers integration | Infrastructure | - | `DatabaseFixture.cs`, `IntegrationTestBase.cs` |
| Create test data builders | Infrastructure | - | `TestDataBuilders/*.cs` |
| Add Governance unit tests | Unit | 75 | `GovernancePIIDetectorTests.cs`, `GovernanceSecurityEngineTests.cs`, `GovernanceAuthorizationEngineTests.cs` |
| Add FluentValidation tests | Unit | 40 | `CreateDocumentCommandValidatorTests.cs`, etc. |

**Deliverables:**
- [ ] TestContainers working with SQL Server
- [ ] Respawn configured for database cleanup
- [ ] All governance components have 90%+ coverage
- [ ] All validators have 100% coverage

### Phase 2: Core Business Logic (Week 3-4)

**Priority: Command/Query Handlers**

| Task | Type | Est. Tests | Files |
|------|------|------------|-------|
| CreateDocumentCommandHandler tests | Integration | 15 | `CreateDocumentCommandHandlerTests.cs` |
| UpdateDocumentCommandHandler tests | Integration | 15 | `UpdateDocumentCommandHandlerTests.cs` |
| ApproveDocumentCommandHandler tests | Integration | 20 | `ApproveDocumentCommandHandlerTests.cs` |
| GetDocumentQueryHandler tests | Integration | 10 | `GetDocumentQueryHandlerTests.cs` |
| SearchDocumentsQueryHandler tests | Integration | 15 | `SearchDocumentsQueryHandlerTests.cs` |
| Specification tests | Unit | 25 | `DocumentSpecificationsTests.cs` |

**Deliverables:**
- [ ] All command handlers have 80%+ coverage
- [ ] All query handlers have 80%+ coverage
- [ ] Specifications have 90%+ coverage

### Phase 3: API & Pipeline (Week 5-6)

**Priority: HTTP Layer & Cross-Cutting Concerns**

| Task | Type | Est. Tests | Files |
|------|------|------------|-------|
| Re-enable and fix DocumentsControllerTests | Integration | 30 | `DocumentsControllerTests.cs` |
| AuthController tests | Integration | 15 | `AuthControllerTests.cs` |
| BatchProcessingController tests | Integration | 10 | `BatchProcessingControllerTests.cs` |
| ValidationBehavior tests | Integration | 15 | `ValidationBehaviorTests.cs` |
| AuthorizationBehavior tests | Integration | 15 | `AuthorizationBehaviorTests.cs` |

**Deliverables:**
- [ ] All controllers have 80%+ coverage
- [ ] Pipeline behaviors have 90%+ coverage
- [ ] Error responses tested for all endpoints

### Phase 4: Services & E2E (Week 7-8)

**Priority: Application Services & Workflows**

| Task | Type | Est. Tests | Files |
|------|------|------------|-------|
| AutoDraftService tests | Unit/Integration | 20 | `AutoDraftServiceTests.cs` |
| Repository integration tests | Integration | 30 | `DocumentRepositoryTests.cs` |
| Document lifecycle E2E | E2E | 10 | `DocumentLifecycleTests.cs` |
| Approval workflow E2E | E2E | 10 | `ApprovalWorkflowTests.cs` |

**Deliverables:**
- [ ] Critical services have 70%+ coverage
- [ ] E2E tests cover main workflows
- [ ] Overall coverage reaches 50%+

### Phase 5: Hardening (Week 9-10)

**Priority: Edge Cases & Performance**

| Task | Type | Est. Tests |
|------|------|------------|
| Edge case coverage for all layers | Various | 50 |
| Concurrent operation tests | Integration | 20 |
| Error handling paths | Various | 30 |
| Performance baseline tests | E2E | 10 |

**Final Target:**
- [ ] Overall coverage: 80%+
- [ ] Security/Governance coverage: 90%+
- [ ] PII handling coverage: 100%
- [ ] Zero flaky tests

---

## 9. Code Examples & Templates

### 9.1 Test Data Builder Pattern

```csharp
// tests/Unit/TestDataBuilders/DocumentBuilder.cs
public class DocumentBuilder
{
    private DocumentId _id = DocumentId.New();
    private string _title = "Test Document";
    private string _category = "Technical";
    private SecurityClassification _security = SecurityClassification.Internal();
    private UserId _createdBy = UserId.New();
    private ApprovalStatus _approvalStatus = ApprovalStatus.NotRequired();
    private DocumentStatus _status = DocumentStatus.Draft;
    private List<string> _tags = new();

    public static DocumentBuilder Create() => new();

    public DocumentBuilder WithId(DocumentId id) { _id = id; return this; }
    public DocumentBuilder WithTitle(string title) { _title = title; return this; }
    public DocumentBuilder WithCategory(string category) { _category = category; return this; }
    public DocumentBuilder WithSecurity(SecurityClassification security) { _security = security; return this; }
    public DocumentBuilder WithCreatedBy(UserId userId) { _createdBy = userId; return this; }
    public DocumentBuilder WithApprovalStatus(ApprovalStatus status) { _approvalStatus = status; return this; }
    public DocumentBuilder WithTags(params string[] tags) { _tags = tags.ToList(); return this; }

    public DocumentBuilder AsPublished()
    {
        _status = DocumentStatus.Published;
        _approvalStatus = ApprovalStatus.Approved(UserId.New());
        return this;
    }

    public DocumentBuilder AsArchived()
    {
        _status = DocumentStatus.Archived;
        return this;
    }

    public Document Build()
    {
        var doc = new Document(_id, _title, _category, _security, _createdBy);

        // Use reflection for test state setup if needed
        if (_status != DocumentStatus.Draft)
        {
            typeof(Document).GetProperty("Status")!.SetValue(doc, _status);
        }
        if (!_approvalStatus.Equals(ApprovalStatus.NotRequired()))
        {
            doc.UpdateApprovalStatus(_approvalStatus);
        }
        foreach (var tag in _tags)
        {
            doc.AddTag(tag);
        }

        doc.ClearDomainEvents(); // Clear setup events
        return doc;
    }
}

// Usage:
var document = DocumentBuilder.Create()
    .WithTitle("My Document")
    .WithSecurity(SecurityClassification.Confidential(new[] { "Engineering" }))
    .AsPublished()
    .Build();
```

### 9.2 Mock Factory for External Services

```csharp
// tests/Common/MockFactory.cs
public static class MockFactory
{
    public static Mock<IOpenAIEnhancementService> CreateOpenAIMock(
        EnhancedDocumentation? response = null)
    {
        var mock = new Mock<IOpenAIEnhancementService>();
        mock.Setup(x => x.EnhanceDocumentationAsync(
                It.IsAny<DocumentationEnhancementRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response ?? new EnhancedDocumentation
            {
                Summary = "Enhanced summary",
                Tags = new[] { "auto-generated" },
                Suggestions = new List<string>()
            });
        return mock;
    }

    public static Mock<IMessageBus> CreateMessageBusMock()
    {
        var mock = new Mock<IMessageBus>();
        var publishedMessages = new List<object>();

        mock.Setup(x => x.PublishEventAsync(It.IsAny<IEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IEvent, CancellationToken>((e, _) => publishedMessages.Add(e))
            .Returns(Task.CompletedTask);

        // Expose for verification
        mock.SetupGet(x => x.PublishedMessages).Returns(publishedMessages);

        return mock;
    }

    public static Mock<ITeamsNotificationService> CreateTeamsNotificationMock()
    {
        var mock = new Mock<ITeamsNotificationService>();
        mock.Setup(x => x.SendDraftReadyNotificationAsync(
                It.IsAny<DraftReadyNotification>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }
}
```

### 9.3 Integration Test with Full Pipeline

```csharp
public class FullPipelineIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateDocument_ThroughFullPipeline_ShouldValidateAuthorizeAndPersist()
    {
        // Arrange
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var command = new CreateDocumentCommand(
            Title: "Pipeline Test",
            Category: "Technical",
            ContentType: "markdown",
            Tags: new List<string> { "integration" },
            CreatedBy: UserId.New(),
            SecurityClassification: SecurityClassification.Internal());

        // Act
        var result = await mediator.Send(command);

        // Assert - Command succeeded
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        // Assert - Database state correct
        var savedDoc = await _dbContext.Documents
            .Include(d => d.CreatedBy)
            .FirstOrDefaultAsync(d => d.Id == DocumentId.From(result.Id));

        savedDoc.Should().NotBeNull();
        savedDoc!.Title.Should().Be("Pipeline Test");
        savedDoc.Status.Should().Be(DocumentStatus.Draft);

        // Assert - Audit log created (if applicable)
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.EntityId == result.Id.ToString())
            .ToListAsync();

        auditLogs.Should().ContainSingle(a => a.Action == "Created");
    }
}
```

---

## References

### Research Sources

- [Microsoft Learn: Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Martin Fowler: Eradicating Non-Determinism in Tests](https://martinfowler.com/articles/nonDeterminism.html)
- [NimblePros: Integration Testing with Database](https://blog.nimblepros.com/blogs/integration-testing-with-database/)
- [Vertical Slice Test Fixtures for MediatR](https://lostechies.com/jimmybogard/2016/10/24/vertical-slice-test-fixtures-for-mediatr-and-asp-net-core/)
- [TestRail: Flaky Tests Prevention](https://www.testrail.com/blog/flaky-tests/)

### Project-Specific References

- Existing test patterns: `tests/Unit/Entities/DocumentTests.cs`
- Domain events: `src/Core/Domain/Events/DocumentEvents.cs`
- CQRS handlers: `src/Core/Application/Commands/Documents/`
- Governance: `src/Core/Governance/`
- Pipeline behaviors: `src/Core/Application/Behaviors/`

---

**Document Maintained By:** Engineering Team
**Last Updated:** 2026-01-31
**Next Review:** After Phase 2 completion
