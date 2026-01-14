# Enterprise Coding Standards

## Version: 1.0.0
## Last Updated: 2025-11-05
## Status: ACTIVE - All code must comply

---

## ðŸŽ¯ Core Principles

1. **Clarity over cleverness** - Code is read 10x more than written
2. **Consistency** - Follow patterns, don't invent new ones
3. **Testability** - If you can't test it, refactor it
4. **Security first** - Assume breach, validate everything
5. **Fail fast** - Detect errors early, fail loudly

---

## ðŸ“ Code Metrics (Enforced by AI Quality System)

### Complexity
- **Cyclomatic Complexity:** â‰¤ 6 per method
- **Cognitive Complexity:** â‰¤ 10 per method
- **Nesting Depth:** â‰¤ 3 levels

### Size Limits
- **Method Length:** â‰¤ 20 lines (excluding braces/whitespace)
- **Class Length:** â‰¤ 200 lines
- **File Length:** â‰¤ 300 lines
- **Parameter Count:** â‰¤ 4 parameters per method

### Quality Thresholds
- **Overall Quality Score:** â‰¥ 85/100
- **Test Coverage:** â‰¥ 80% for all code, â‰¥ 90% for security-critical code
- **Documentation:** 100% public APIs must have XML comments

---

## ðŸ—ï¸ Architecture Patterns

### Required Base Classes
```csharp
// All agents MUST inherit from BaseAgent
public class MyAgent : BaseAgent<MyInput, MyOutput>
{
    // Implementation
}
```

### Dependency Injection (Required)
```csharp
// âœ… CORRECT: Constructor injection
public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IConfiguration _config;
    
    public MyService(ILogger<MyService> logger, IConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
}

// âŒ WRONG: Service locator, static dependencies
public class BadService
{
    private ILogger _logger = ServiceLocator.Get<ILogger>();
}
```

### Async/Await (Required for I/O)
```csharp
// âœ… CORRECT
public async Task<Document> GenerateAsync(Request request)
{
    var data = await _repository.GetDataAsync();
    return await ProcessAsync(data);
}

// âŒ WRONG: Sync over async, blocking
public Document Generate(Request request)
{
    var data = _repository.GetDataAsync().Result; // BLOCKS!
    return ProcessAsync(data).GetAwaiter().GetResult();
}
```

---

## ðŸ“ Naming Conventions

### General Rules
- **PascalCase:** Classes, methods, properties, public fields
- **camelCase:** Local variables, private fields, parameters
- **_camelCase:** Private fields (with underscore prefix)
- **UPPER_CASE:** Constants only

### Specific Patterns
```csharp
// Interfaces
public interface IDocumentGenerator { }

// Classes
public class DocumentGeneratorAgent { }

// Methods - verb phrases
public async Task<Document> GenerateDocumentAsync() { }
public bool ValidateInput() { }
public void ProcessData() { }

// Properties - noun phrases
public string AgentName { get; }
public int MaxRetries { get; set; }

// Events
public event EventHandler DocumentGenerated;

// Private fields
private readonly ILogger _logger;
private string _cachedValue;

// Constants
public const int MAX_RETRY_COUNT = 3;
private const string DEFAULT_TEMPLATE = "Standard";

// Boolean variables - question form
public bool IsValid { get; }
public bool CanProcess { get; }
public bool HasErrors { get; }
```

### Agent Naming
- Must end with "Agent": `DocumentGeneratorAgent`, `SchemaDetectorAgent`
- Must be descriptive: `PaymentProcessorAgent` not `ProcessorAgent`

### Service Naming
- Must end with "Service": `NotificationService`, `ValidationService`

---

## ðŸ”’ Security Standards

### No Hardcoded Secrets
```csharp
// âŒ WRONG
private string _apiKey = "sk-1234567890";
var connString = "Server=prod;Password=secret123";

// âœ… CORRECT
private string _apiKey = await GetSecretAsync("ApiKey");
var connString = _configuration["ConnectionStrings:Default"];
```

### Input Validation (Always)
```csharp
public async Task<Result> ProcessAsync(Request request)
{
    // ALWAYS validate
    ArgumentNullException.ThrowIfNull(request);
    
    if (string.IsNullOrWhiteSpace(request.DocumentId))
        throw new ArgumentException("DocumentId required", nameof(request));
    
    // Process...
}
```

### SQL Injection Prevention
```csharp
// âŒ WRONG: String concatenation
var sql = $"SELECT * FROM Users WHERE Id = {userId}";

// âœ… CORRECT: Parameterized queries OR use governance proxy
var query = new AgentQuery {
    SqlQuery = "SELECT * FROM Users WHERE Id = @UserId",
    Parameters = new { UserId = userId }
};
var result = await _governanceProxy.ExecuteSecureQuery(query);
```

---

## ðŸ“š Documentation Standards

### XML Documentation (Required for Public APIs)
```csharp
/// <summary>
/// Generates a document from the specified template and data.
/// </summary>
/// <param name="templateId">The unique identifier of the template to use.</param>
/// <param name="data">The data to populate the template with.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>
/// A <see cref="Document"/> containing the generated content.
/// </returns>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="templateId"/> or <paramref name="data"/> is null.
/// </exception>
/// <exception cref="TemplateNotFoundException">
/// Thrown when the specified template cannot be found.
/// </exception>
public async Task<Document> GenerateAsync(
    string templateId, 
    Dictionary<string, object> data,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### README.md (Required for Each Agent)
```markdown
# AgentName

## Purpose
Brief description of what this agent does.

## Dependencies
- BaseAgent
- Azure Service Bus
- SQL Database

## Configuration
```json
{
  "Agent:Name": "DocumentGenerator",
  "Agent:RequiredSecrets": ["DatabaseConnection", "ApiKey"]
}
```

## Usage
```csharp
var agent = new DocumentGeneratorAgent(config, logger);
var result = await agent.ExecuteAsync(input, cancellationToken);
```

## Testing
- Unit test coverage: 85%
- Integration tests: Yes
- Performance benchmarks: Available
```

---

## âœ… Error Handling

### Use Specific Exceptions
```csharp
// âŒ WRONG: Generic exceptions
throw new Exception("Something failed");

// âœ… CORRECT: Specific exceptions
throw new DocumentGenerationException("Template not found", templateId);
throw new ValidationException("Invalid schema format");
```

### Always Log Exceptions
```csharp
try
{
    await ProcessAsync(data);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Processing failed for document {DocumentId}", documentId);
    throw; // Re-throw after logging
}
```

### Structured Error Messages
```csharp
// âŒ WRONG: Vague message
throw new Exception("Error processing");

// âœ… CORRECT: Actionable message
throw new DocumentProcessingException(
    $"Failed to process document '{documentId}'. " +
    $"Template '{templateId}' requires field 'CustomerName' but it was not provided.",
    documentId,
    templateId);
```

---

## ðŸ§ª Testing Standards

### Test Naming
```csharp
[Fact]
public void GenerateAsync_WithValidInput_ReturnsDocument()
{
    // Method_Scenario_ExpectedResult
}

[Fact]
public void GenerateAsync_WithNullTemplate_ThrowsArgumentNullException()
{
    // Clear, descriptive test names
}
```

### AAA Pattern (Arrange, Act, Assert)
```csharp
[Fact]
public async Task GenerateAsync_WithValidInput_ReturnsDocument()
{
    // Arrange
    var generator = new DocumentGenerator(_logger, _config);
    var input = new DocumentRequest { TemplateId = "test-123" };
    
    // Act
    var result = await generator.GenerateAsync(input);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("test-123", result.TemplateId);
}
```

### Coverage Requirements
- **Unit Tests:** Test individual methods in isolation
- **Integration Tests:** Test component interactions
- **Minimum Coverage:** 80% overall, 90% for security/governance code
- **Critical Paths:** 100% coverage for payment, PII handling, auth

---

## ðŸš« Anti-Patterns (Never Do These)

### Magic Numbers/Strings
```csharp
// âŒ WRONG
if (status == "approved") { }
await Task.Delay(5000);

// âœ… CORRECT
private const string STATUS_APPROVED = "approved";
private const int RETRY_DELAY_MS = 5000;

if (status == STATUS_APPROVED) { }
await Task.Delay(RETRY_DELAY_MS);
```

### God Classes
```csharp
// âŒ WRONG: 2000 line class doing everything
public class DocumentProcessor
{
    public void Generate() { }
    public void Validate() { }
    public void Transform() { }
    public void Send() { }
    public void Log() { }
    public void Retry() { }
    // 50 more methods...
}

// âœ… CORRECT: Single responsibility
public class DocumentGenerator { }
public class DocumentValidator { }
public class DocumentTransformer { }
public class DocumentSender { }
```

### Swallowing Exceptions
```csharp
// âŒ WRONG: Silent failure
try
{
    await ProcessAsync();
}
catch
{
    // Nothing - error disappears
}

// âœ… CORRECT: Log and handle or rethrow
try
{
    await ProcessAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Processing failed");
    throw; // Or handle appropriately
}
```

---

## âš¡ Performance Guidelines

### Async Best Practices
- Use `ConfigureAwait(false)` in libraries
- Avoid `async void` (except event handlers)
- Don't mix sync and async code

### Resource Management
```csharp
// âœ… Use using statements
using var connection = new SqlConnection(connectionString);
using var stream = File.OpenRead(path);

// âœ… Implement IDisposable/IAsyncDisposable
public class MyResource : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        // Cleanup
    }
}
```

### Collection Efficiency
```csharp
// Preallocate when size known
var list = new List<string>(capacity: 1000);

// Use appropriate collection type
var lookup = new Dictionary<string, Document>(); // O(1) lookup
var queue = new Queue<Task>(); // FIFO operations
```

---

## ðŸ“Š Code Review Checklist

Before submitting code, verify:

- [ ] All public APIs have XML documentation
- [ ] No hardcoded secrets or connection strings
- [ ] All I/O operations are async
- [ ] Exception handling includes logging
- [ ] Input validation on all public methods
- [ ] Test coverage â‰¥ 80%
- [ ] AI Quality System score â‰¥ 85
- [ ] No TODO/HACK comments (file issues instead)
- [ ] Follows naming conventions
- [ ] No copy-paste code (DRY principle)

---

## ðŸ”„ Enforcement

These standards are enforced through:

1. **AI Quality System** - Automated validation on every commit
2. **Unit Tests** - Coverage requirements
3. **Code Review** - Manual peer review
4. **CI/CD Pipeline** - Blocks merge if standards violated

**No exceptions. No bypasses. No "I'll fix it later."**

---

## ðŸ“– References

- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Clean Code by Robert C. Martin](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)

---

**Version History:**
- v1.0.0 (2025-11-05): Initial standards document