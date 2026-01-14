# Testing Task

## Expert Mode: E2E Testing Engineer
Reference: e2e-testing-patterns skill

## Task Description
[DESCRIBE YOUR TESTING TASK HERE]

## Apply These Patterns

### Test Types
- **E2E Tests**: Full user workflows (Playwright/Cypress)
- **Integration Tests**: API endpoints, database operations
- **Unit Tests**: Handlers, validators, domain logic

### E2E Testing Best Practices (Playwright/Cypress)
- Use Page Object Model for maintainability
- Stable selectors: data-testid > role > label > CSS
- Avoid fixed timeouts - wait for conditions
- Independent tests (no shared state)
- Proper setup and teardown

### Reliable Selectors
```typescript
// ✅ Good
await page.getByTestId('submit-button').click();
await page.getByRole('button', { name: 'Submit' }).click();
await page.getByLabel('Email').fill('test@example.com');

// ❌ Bad
await page.locator('.btn.btn-primary').click(); // Brittle CSS
await page.locator('div > form > button:nth-child(2)').click(); // DOM structure
```

### Network Mocking
- Mock external APIs in tests
- Test error scenarios (500, 404, timeout)
- Verify loading states
- Test retry logic

### Test Data Management
- Use fixtures for consistent data
- Clean up after each test
- Generate unique data (timestamps)
- Isolate test databases

### .NET Testing (xUnit + Testcontainers)
- Use Testcontainers for real database
- WebApplicationFactory for integration tests
- Moq for mocking dependencies
- FluentAssertions for readable assertions

## Test Organization

### E2E Tests Structure
```
tests/
├── e2e/
│   ├── pages/              # Page objects
│   │   └── LoginPage.ts
│   ├── fixtures/           # Test data
│   │   └── users.ts
│   └── specs/              # Test specs
│       └── login.spec.ts
```

### .NET Tests Structure
```
tests/
├── UnitTests/
│   ├── Application/        # Handler tests
│   ├── Core/              # Domain tests
│   └── WebApi/            # Controller tests
└── IntegrationTests/
    ├── Api/               # API endpoint tests
    └── Infrastructure/    # Repository tests
```

## Success Criteria
- [ ] Tests are independent and isolated
- [ ] Use stable selectors (data-testid)
- [ ] No fixed timeouts (proper waits)
- [ ] Page Object Model for E2E tests
- [ ] Proper setup and cleanup
- [ ] Tests pass consistently (not flaky)
- [ ] Good coverage of critical paths
- [ ] Error scenarios tested

## Common Patterns

### Playwright Page Object
```typescript
export class LoginPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.getByLabel('Email');
    this.passwordInput = page.getByLabel('Password');
    this.submitButton = page.getByRole('button', { name: 'Login' });
  }

  async login(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }
}
```

### .NET Integration Test
```csharp
public class DocumentControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DocumentControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDocuments_ReturnsSuccessStatusCode()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/documents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

## Next Steps After Writing Tests
1. Run tests locally
2. Verify they pass consistently (run 5+ times)
3. Add to CI/CD pipeline
4. Review coverage reports
5. Document test scenarios
