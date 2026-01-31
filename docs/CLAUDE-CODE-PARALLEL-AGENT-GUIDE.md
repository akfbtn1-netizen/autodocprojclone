# Claude Code Parallel Agent Execution Guide

**Purpose:** Enable efficient parallel test implementation using Claude Code's multi-agent capabilities.

---

## Overview

Claude Code can run multiple specialized agents in parallel to speed up test implementation. This guide shows how to invoke the right agents with the right prompts for testing tasks.

---

## Agent Types for Testing

| Agent Type | Use Case | Tools Available |
|------------|----------|-----------------|
| `general-purpose` | Write test files, implement features | All tools |
| `Explore` | Find patterns, understand architecture | Read, Grep, Glob |
| `Plan` | Design test strategy | Read, Grep, Glob |

---

## Parallel Implementation Pattern

When implementing multiple test files, ask Claude to run agents in parallel:

### Example Prompt

```
Implement the following test files in parallel:
1. DocumentsControllerTests - integration tests for API endpoints
2. CreateDocumentCommandHandlerTests - MediatR handler tests
3. SearchDocumentsQueryHandlerTests - query handler tests

Use the existing test patterns from SecurityClassificationTests.cs
```

Claude Code will launch multiple agents simultaneously, each writing a test file.

---

## Skill Invocation Patterns

### Pattern 1: E2E Testing Agent

**When to use:** Complete workflow tests that span multiple components.

**Prompt template:**
```
Write E2E tests for the document approval workflow:
- Create document -> Submit for approval -> Approve -> Publish
- Test the complete flow through the API
- Use WebApplicationFactory pattern from existing integration tests
- Follow AAA pattern and FluentAssertions style
```

### Pattern 2: API Testing Agent

**When to use:** HTTP endpoint testing with status codes and payloads.

**Prompt template:**
```
Write integration tests for DocumentsController endpoints:
- Read the controller at src/Api/Controllers/DocumentsController.cs
- Test each HTTP endpoint (POST, GET, PUT)
- Verify status codes: 200, 201, 400, 404, 500
- Test error response formats
- Use WebApplicationFactory from Microsoft.AspNetCore.Mvc.Testing
```

### Pattern 3: Security Testing Agent

**When to use:** Testing security controls and input validation.

**Prompt template:**
```
Write security-focused tests:
- SQL injection prevention (already done in GovernanceSecurityEngineTests)
- Authorization checks on protected endpoints
- Input validation for all user-provided data
- PII detection accuracy (already done in GovernancePIIDetectorTests)
```

### Pattern 4: Unit Test Agent

**When to use:** Testing isolated business logic.

**Prompt template:**
```
Write unit tests for [Component]:
- Read the existing test patterns in tests/Unit/
- Use Moq for dependencies
- Test all public methods
- Include edge cases and error conditions
- Follow the MethodName_Scenario_ExpectedBehavior naming convention
```

---

## Cross-Referencing Skills

When implementing tests, the agent should:

1. **Read existing test patterns first:**
   - `tests/Unit/ValueObjects/SecurityClassificationTests.cs` - Test structure
   - `tests/Unit/Entities/DocumentTests.cs` - Entity testing patterns
   - `tests/Unit/Services/DocumentValidationServiceTests.cs` - Service testing

2. **Read the code under test:**
   - Understand the public API
   - Identify edge cases
   - Check for error handling paths

3. **Apply false positive prevention:**
   - Use deterministic data (no DateTime.Now in assertions)
   - Test both positive and negative cases
   - Include boundary value tests

---

## Implementation Commands

### Sequential Implementation (Current Approach)

```bash
# Run tests after writing
dotnet test tests/Unit/Tests.Unit.csproj --filter "FullyQualifiedName~Governance"
```

### Parallel Agent Execution

To implement remaining tests in parallel, ask:

```
Run these agents in parallel:
1. Agent to write DocumentsControllerTests.cs
2. Agent to write CreateDocumentCommandHandlerTests.cs
3. Agent to write ApproveDocumentCommandHandlerTests.cs

Each agent should:
- Read the corresponding source code
- Follow existing test patterns
- Use FluentAssertions and xUnit
- Include both success and failure test cases
```

---

## Test Implementation Checklist

For each test file:

- [ ] Read source code being tested
- [ ] Read similar existing tests for patterns
- [ ] Write constructor/setup tests
- [ ] Write happy path tests
- [ ] Write error/exception tests
- [ ] Write edge case tests
- [ ] Verify no false positives possible
- [ ] Add to project references if needed

---

## File Locations

| Test Type | Location |
|-----------|----------|
| Unit Tests | `tests/Unit/` |
| - Entities | `tests/Unit/Entities/` |
| - Value Objects | `tests/Unit/ValueObjects/` |
| - Services | `tests/Unit/Services/` |
| - Governance | `tests/Unit/Governance/` |
| Integration Tests | `tests/Integration/` |
| - Controllers | `tests/Integration/Controllers/` |
| - Handlers | `tests/Integration/Handlers/` |

---

## Test Project References

```xml
<!-- tests/Unit/Tests.Unit.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\src\Core\Domain\Core.Domain.csproj" />
  <ProjectReference Include="..\..\src\Core\Application\Core.Application.csproj" />
  <ProjectReference Include="..\..\src\Core\Governance\Core.Governance.csproj" />
</ItemGroup>
```

---

## Summary

1. **Use parallel agents** for independent test files
2. **Cross-reference** existing patterns before writing
3. **Verify no false positives** with true negative tests
4. **Follow naming conventions** consistently
5. **Test both success and failure** paths
