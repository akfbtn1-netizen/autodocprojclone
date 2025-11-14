# Integration Test Fixes Summary

## Issues Fixed

### 1. Database Context Issue
**Problem**: `CustomWebApplicationFactory.cs` referenced `ApplicationDbContext` which doesn't exist in the project.

**Fix**: Updated to use the correct `DocumentationDbContext` class from `Enterprise.Documentation.Core.Infrastructure.Persistence`.

**Changes in** `tests/Integration/CustomWebApplicationFactory.cs`:
- Changed all references from `ApplicationDbContext` to `DocumentationDbContext`
- Added proper using statements for domain entities and value objects
- Implemented test data seeding with pre-configured test users

### 2. Missing User Registration Endpoint
**Problem**: Integration tests expected a `/api/auth/register` endpoint, but only login, logout, and "me" endpoints existed.

**Fix**: Added a complete user registration endpoint to `AuthController`.

**Changes in** `src/Api/Controllers/AuthController.cs`:
- Added `RegisterRequest` model class with Email, Password, FirstName, LastName, and SecurityClearanceLevel properties
- Implemented `[HttpPost("register")]` endpoint with:
  - Input validation
  - Duplicate user checking (returns 409 Conflict)
  - User creation with proper domain entities
  - JWT token generation
  - Proper HTTP status codes (201 Created, 400 Bad Request, 409 Conflict, 500 Internal Server Error)

### 3. Test Helper Improvements
**Problem**: Integration test helpers had incorrect method signatures and response models.

**Fix**: Updated `IntegrationTestHelpers.cs` with proper types and methods.

**Changes in** `tests/Integration/Helpers/IntegrationTestHelpers.cs`:
- Changed `CreateAuthenticatedClientAsync` to accept `CustomWebApplicationFactory` instead of generic `WebApplicationFactory<Program>`
- Added `GetAuthTokenAsync` method for getting JWT tokens
- Added `CreateTestUserAsync` method for registering new test users
- Updated response models to match actual API responses (added RefreshToken, ExpiresAt, User properties)

### 4. Test Database Seeding
**Problem**: Tests were failing because no users existed in the in-memory database.

**Fix**: Implemented automatic test data seeding in `CustomWebApplicationFactory`.

**Test Users Created**:
1. **testadmin@example.com**
   - Display Name: "Test Admin"
   - Security Clearance: Confidential
   - Roles: Admin, DocumentEditor

2. **testuser@example.com**
   - Display Name: "Test User"
   - Security Clearance: Restricted
   - Roles: DocumentViewer

## How to Test

### Step 1: Build the Project
```powershell
dotnet build
```

This should now compile successfully without errors.

### Step 2: Run All Tests
```powershell
dotnet test
```

### Step 3: Run Specific Integration Tests
```powershell
# Run only integration tests
dotnet test --filter "FullyQualifiedName~Integration"

# Run only Auth controller tests (if they exist in your local repo)
dotnet test --filter "FullyQualifiedName~AuthController"
```

## Expected Results

After these fixes, the following should work:

1. ✅ **Database Connection**: Tests use in-memory database with `DocumentationDbContext`
2. ✅ **User Authentication**: Pre-seeded test users can log in successfully
3. ✅ **User Registration**: New users can be registered via `/api/auth/register`
4. ✅ **JWT Token Generation**: Login returns valid JWT tokens
5. ✅ **Authenticated Requests**: Tests can make authenticated API calls

## Integration Test Usage Example

```csharp
public class MyIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MyIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = IntegrationTestHelpers.CreateAuthenticatedClientAsync(factory).Result;
    }

    [Fact]
    public async Task MyTest_ShouldSucceed()
    {
        // Client is already authenticated as testadmin@example.com
        var response = await _client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
    }
}
```

## API Endpoints Available

### Authentication Endpoints

#### POST /api/auth/register
Register a new user account.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "securityClearanceLevel": "Restricted"
}
```

**Responses**:
- `201 Created`: User registered successfully with JWT token
- `400 Bad Request`: Invalid input
- `409 Conflict`: User already exists
- `500 Internal Server Error`: Server error

#### POST /api/auth/login
Authenticate and receive JWT token.

**Request Body**:
```json
{
  "email": "testadmin@example.com",
  "password": "AnyPassword123"
}
```

**Response** (200 OK):
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "550e8400e29b41d4a716446655440000",
  "expiresAt": "2025-11-15T12:00:00Z",
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "testadmin@example.com",
    "displayName": "Test Admin",
    "roles": ["Admin", "DocumentEditor"]
  }
}
```

#### GET /api/auth/me
Get current authenticated user information.

**Headers Required**:
```
Authorization: Bearer {token}
```

**Response** (200 OK):
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "email": "testadmin@example.com",
  "displayName": "Test Admin",
  "roles": ["Admin", "DocumentEditor"]
}
```

#### POST /api/auth/logout
Logout current user (requires authentication).

## Troubleshooting

### If tests still fail with 404 errors:
- Ensure your test files use `CustomWebApplicationFactory` (not `WebApplicationFactory<Program>`)
- Verify controller routing with `[Route("api/[controller]")]` attribute
- Check that `app.MapControllers()` is called in `Program.cs`

### If tests fail with 401 Unauthorized:
- Verify test users are seeded properly
- Check JWT settings in test configuration
- Ensure `Authorization: Bearer {token}` header is set

### If database errors occur:
- Clear the bin/obj folders: `dotnet clean`
- Rebuild: `dotnet build`
- Check that `DocumentationDbContext` is properly registered in DI container

## Files Modified

1. `tests/Integration/CustomWebApplicationFactory.cs`
   - Fixed DbContext reference
   - Added test data seeding

2. `src/Api/Controllers/AuthController.cs`
   - Added RegisterRequest model
   - Implemented register endpoint

3. `tests/Integration/Helpers/IntegrationTestHelpers.cs`
   - Updated method signatures
   - Added helper methods
   - Fixed response models

## Next Steps

1. Test locally with `dotnet test`
2. Review test results
3. If all tests pass, commit and push changes
4. Create pull request with test results
