# Enterprise Documentation Platform - API Documentation

## Table of Contents
1. [Overview](#overview)
2. [API Endpoints](#api-endpoints)
3. [Authentication & Authorization](#authentication--authorization)
4. [API Versioning](#api-versioning)
5. [Middleware Configuration](#middleware-configuration)
6. [Swagger/OpenAPI Setup](#swaggeropenapi-setup)
7. [Data Models & DTOs](#data-models--dtos)
8. [Error Handling](#error-handling)
9. [Rate Limiting & Governance](#rate-limiting--governance)
10. [Configuration](#configuration)

---

## Overview

The Enterprise Documentation Platform provides a comprehensive REST API for managing documentation, users, templates, and batch processing operations. The API is built on ASP.NET Core 8.0 with CQRS pattern, MediatR, and implements enterprise-grade data governance.

**API Version:** v1  
**Base URL:** `/api/`  
**Response Format:** JSON  
**Authentication:** JWT Bearer Token

---

## API Endpoints

### 1. Authentication Controller (`/api/auth`)

Handle user authentication and token management.

#### POST `/api/auth/login`
**Summary:** Authenticate user and get JWT token  
**Authentication:** None (AllowAnonymous)  
**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "00000000-0000-0000-0000-000000000000",
  "expiresAt": "2024-11-23T20:00:00Z",
  "user": {
    "id": "00000000-0000-0000-0000-000000000001",
    "email": "user@example.com",
    "displayName": "John Doe",
    "roles": ["User", "Manager"]
  }
}
```

**Error Responses:**
- `400 Bad Request` - Missing email or password
- `401 Unauthorized` - Invalid credentials or inactive user
- `500 Internal Server Error` - Server error

---

#### POST `/api/auth/logout`
**Summary:** Log out current user  
**Authentication:** Required (Bearer Token)  
**Request Body:** None  

**Success Response (200 OK):**
```json
{
  "message": "Logged out successfully"
}
```

---

#### GET `/api/auth/me`
**Summary:** Get current authenticated user information  
**Authentication:** Required (Bearer Token)  

**Success Response (200 OK):**
```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "email": "user@example.com",
  "displayName": "John Doe",
  "roles": ["User", "Manager"]
}
```

**Error Responses:**
- `401 Unauthorized` - User not authenticated or invalid token

---

### 2. Documents Controller (`/api/documents`)

Manage document creation, retrieval, updates, and approval workflows.

#### POST `/api/documents`
**Summary:** Create a new document  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "title": "API Documentation",
  "documentType": "Technical Specification",
  "content": "Document content here...",
  "contentType": "text/plain",
  "tags": ["api", "documentation", "technical"],
  "templateId": "template-guid-optional",
  "templateVariables": {
    "version": "1.0",
    "author": "John Doe"
  },
  "securityLevel": "Internal",
  "requiresApproval": false
}
```

**Success Response (201 Created):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "API Documentation",
  "documentType": "Technical Specification",
  "status": "Draft",
  "content": "Document content here...",
  "contentType": "text/plain",
  "size": 1024,
  "tags": ["api", "documentation", "technical"],
  "createdBy": "user-guid",
  "modifiedBy": "user-guid",
  "modifiedAt": "2024-11-23T12:00:00Z",
  "documentVersion": 1,
  "containsPII": false,
  "securityLevel": "Internal",
  "approvalStatus": "NotRequired",
  "relatedDocuments": [],
  "templateId": null,
  "storagePath": "/documents/api-documentation.docx"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### GET `/api/documents/{id}`
**Summary:** Get document by ID  
**Authentication:** Required (Bearer Token)  
**Parameters:**
- `id` (path): Document ID (GUID)

**Success Response (200 OK):** Returns DocumentDto (same as POST response)

**Error Responses:**
- `404 Not Found` - Document not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### GET `/api/documents/search`
**Summary:** Search documents with pagination  
**Authentication:** Optional  
**Query Parameters:**
- `searchTerm` (string, optional): Search term to filter documents
- `pageNumber` (int, default: 1): Page number for pagination
- `pageSize` (int, default: 20): Items per page (max: 100)

**Success Response (200 OK):**
```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "title": "API Documentation",
      "status": "Published",
      ...
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

**Error Responses:**
- `400 Bad Request` - Invalid pagination parameters
- `500 Internal Server Error` - Server error

---

#### PUT `/api/documents/{id}`
**Summary:** Update an existing document  
**Authentication:** Required (Bearer Token)  
**Parameters:**
- `id` (path): Document ID to update (GUID)

**Request Body:**
```json
{
  "documentId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Updated API Documentation",
  "content": "Updated content...",
  "tags": ["api", "updated"],
  "status": "UnderReview",
  "updateReason": "Fixed typos and added examples",
  "incrementVersion": true
}
```

**Success Response (200 OK):** Returns updated DocumentDto

**Error Responses:**
- `400 Bad Request` - Route ID doesn't match command ID or invalid data
- `404 Not Found` - Document not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### POST `/api/documents/{id}/approve`
**Summary:** Approve a document  
**Authentication:** Required (Bearer Token with Manager/Admin role)  
**Parameters:**
- `id` (path): Document ID to approve

**Request Body:** Empty

**Success Response (200 OK):** Returns updated DocumentDto with ApprovalStatus: "Approved"

**Error Responses:**
- `400 Bad Request` - Invalid document state
- `404 Not Found` - Document not found
- `401 Unauthorized` - Not authenticated or insufficient permissions
- `500 Internal Server Error` - Server error

---

### 3. Users Controller (`/api/users`)

Manage user accounts and profiles.

#### POST `/api/users`
**Summary:** Create a new user  
**Authentication:** Optional (Admin recommended)  

**Request Body:**
```json
{
  "email": "newuser@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "User"
}
```

**Success Response (201 Created):**
```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "email": "newuser@example.com",
  "displayName": "John Doe",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "securityClearance": "Public",
  "createdAt": "2024-11-23T12:00:00Z",
  "roles": ["User"]
}
```

**Error Responses:**
- `400 Bad Request` - Invalid user data
- `500 Internal Server Error` - Server error

---

#### GET `/api/users/{id}`
**Summary:** Get user by ID  
**Authentication:** Optional  
**Parameters:**
- `id` (path): User ID (GUID)

**Success Response (200 OK):** Returns user object

**Error Responses:**
- `404 Not Found` - User not found
- `500 Internal Server Error` - Server error

---

#### PUT `/api/users/{id}`
**Summary:** Update user information  
**Authentication:** Required (Bearer Token)  
**Parameters:**
- `id` (path): User ID to update

**Request Body:**
```json
{
  "email": "updated@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "role": "Manager"
}
```

**Success Response (200 OK):** Returns updated user object

**Error Responses:**
- `400 Bad Request` - Invalid update data
- `404 Not Found` - User not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

### 4. Templates Controller (`/api/templates`)

Manage document templates.

#### GET `/api/templates/{id}`
**Summary:** Get template by ID  
**Authentication:** Optional  
**Parameters:**
- `id` (path): Template ID (GUID)

**Success Response (200 OK):**
```json
{
  "id": "template-guid",
  "name": "Technical Specification Template",
  "description": "Template for technical specifications",
  "content": "Template content with placeholders...",
  "category": "Technical"
}
```

**Error Responses:**
- `404 Not Found` - Template not found
- `500 Internal Server Error` - Server error

---

### 5. Batch Processing Controller (`/api/batchprocessing`)

Execute batch documentation generation from multiple sources.

#### POST `/api/batchprocessing/schema`
**Summary:** Start batch processing for database schema  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "database": "IRFS1",
  "schema": "gwpc",
  "userId": "00000000-0000-0000-0000-000000000000",
  "options": {
    "extractMetadata": true,
    "useOpenAIEnhancement": true,
    "confidenceThreshold": 0.85,
    "requireHumanReviewBelowThreshold": true,
    "generateDocId": true,
    "validateDocIdUniqueness": true,
    "moveToCorrectPath": true,
    "renameFilesToDocId": true,
    "backupOriginals": true,
    "populateMasterIndex": true,
    "calculateQualityScores": true,
    "generateEmbeddings": true,
    "enableSemanticSearch": true,
    "queueForApproval": false,
    "requireApprovalForLowConfidence": true,
    "maxParallelTasks": 4,
    "retryAttempts": 3,
    "retryDelay": "00:00:05",
    "sendNotifications": true,
    "notifyOnCompletion": true,
    "notifyOnErrors": true,
    "notificationRecipients": ["admin@example.com"]
  }
}
```

**Success Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Schema processing started for IRFS1.gwpc",
  "statusUrl": "/api/batchprocessing/550e8400-e29b-41d4-a716-446655440000"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### POST `/api/batchprocessing/folder`
**Summary:** Start batch processing for folder of .docx files  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "folderPath": "C:\\Documentation\\IRFS1\\gwpc",
  "userId": "00000000-0000-0000-0000-000000000000",
  "options": { /* BatchProcessingOptions */ }
}
```

**Success Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Folder processing started for C:\\Documentation\\IRFS1\\gwpc",
  "statusUrl": "/api/batchprocessing/550e8400-e29b-41d4-a716-446655440000"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid request parameters
- `404 Not Found` - Folder not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### POST `/api/batchprocessing/excel`
**Summary:** Start batch processing from Excel spreadsheet  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "excelFilePath": "C:\\Users\\User\\Desktop\\Change Spreadsheet.xlsx",
  "userId": "00000000-0000-0000-0000-000000000000",
  "options": { /* BatchProcessingOptions */ }
}
```

**Success Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Excel import started from C:\\Users\\User\\Desktop\\Change Spreadsheet.xlsx",
  "statusUrl": "/api/batchprocessing/550e8400-e29b-41d4-a716-446655440000"
}
```

**Error Responses:**
- `400 Bad Request` - Invalid request parameters
- `404 Not Found` - Excel file not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### GET `/api/batchprocessing/{batchId}`
**Summary:** Get batch job status with detailed progress  
**Authentication:** Optional  
**Parameters:**
- `batchId` (path): Batch job ID

**Success Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "sourceType": "DatabaseSchema",
  "databaseName": "IRFS1",
  "schemaName": "gwpc",
  "sourcePath": null,
  "status": "Running",
  "totalItems": 250,
  "processedCount": 150,
  "successCount": 145,
  "failedCount": 3,
  "requiresReviewCount": 2,
  "progressPercentage": 60.0,
  "averageConfidence": 0.87,
  "startedAt": "2024-11-23T10:00:00Z",
  "completedAt": null,
  "duration": "00:30:00",
  "estimatedTimeRemaining": "00:30:00",
  "errorMessage": null,
  "options": { /* BatchProcessingOptions */ }
}
```

**Error Responses:**
- `404 Not Found` - Batch job not found
- `500 Internal Server Error` - Server error

---

#### GET `/api/batchprocessing`
**Summary:** Get all batch jobs with pagination and filtering  
**Authentication:** Optional  
**Query Parameters:**
- `page` (int, default: 1): Page number
- `pageSize` (int, default: 20): Items per page
- `status` (string, optional): Filter by status (Pending, Queued, Running, Completed, etc.)

**Success Response (200 OK):**
```json
{
  "items": [
    { /* BatchJobDto */ }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

---

#### POST `/api/batchprocessing/{batchId}/cancel`
**Summary:** Cancel a running batch job  
**Authentication:** Required (Bearer Token)  
**Parameters:**
- `batchId` (path): Batch job ID to cancel

**Success Response (200 OK):**
```json
{
  "message": "Batch 550e8400-e29b-41d4-a716-446655440000 cancelled successfully"
}
```

**Error Responses:**
- `404 Not Found` - Batch job not found
- `401 Unauthorized` - Not authenticated
- `500 Internal Server Error` - Server error

---

#### POST `/api/batchprocessing/{batchId}/retry`
**Summary:** Retry failed items in a batch  
**Authentication:** Required (Bearer Token)  
**Parameters:**
- `batchId` (path): Batch job ID

**Success Response (200 OK):**
```json
{
  "message": "Failed items in batch 550e8400-e29b-41d4-a716-446655440000 queued for retry"
}
```

---

#### GET `/api/batchprocessing/review`
**Summary:** Get items requiring human review  
**Authentication:** Optional  
**Query Parameters:**
- `batchId` (GUID, optional): Filter by specific batch

**Success Response (200 OK):**
```json
[
  {
    "itemId": "550e8400-e29b-41d4-a716-446655440001",
    "batchId": "550e8400-e29b-41d4-a716-446655440000",
    "objectName": "sp_GetUserData",
    "objectType": "StoredProcedure",
    "status": "RequiresReview",
    "generatedDocId": null,
    "confidenceScore": 0.72,
    "confidenceLevel": "Medium",
    "requiresHumanReview": true,
    "validationWarnings": ["Low confidence score", "Missing parameters documentation"],
    "documentPath": null,
    "isVectorIndexed": false,
    "processedAt": null,
    "errorMessage": null
  }
]
```

---

#### POST `/api/batchprocessing/review/approve`
**Summary:** Approve items for processing  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "itemIds": [
    "550e8400-e29b-41d4-a716-446655440001",
    "550e8400-e29b-41d4-a716-446655440002"
  ],
  "reviewedBy": "00000000-0000-0000-0000-000000000000"
}
```

**Success Response (200 OK):**
```json
{
  "message": "Approved 2 items successfully"
}
```

---

#### POST `/api/batchprocessing/review/reject`
**Summary:** Reject items with feedback  
**Authentication:** Required (Bearer Token)  

**Request Body:**
```json
{
  "itemIds": [
    "550e8400-e29b-41d4-a716-446655440001"
  ],
  "reason": "Parameters are unclear and need revision",
  "reviewedBy": "00000000-0000-0000-0000-000000000000"
}
```

**Success Response (200 OK):**
```json
{
  "message": "Rejected 1 items successfully"
}
```

---

### 6. Governance Testing Endpoints

These endpoints are for testing and validating the governance layer.

#### POST `/governance/validate`
**Summary:** Validate a query through the Data Governance Proxy  
**Authentication:** Optional  

**Request Body:**
```json
{
  "agentId": "agent-001",
  "agentName": "Documentation Generator",
  "agentPurpose": "Auto-generate documentation from schema",
  "databaseName": "IRFS1",
  "sqlQuery": "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema",
  "parameters": {
    "schema": "gwpc"
  },
  "requestedTables": ["INFORMATION_SCHEMA.TABLES"],
  "requestedColumns": ["TABLE_NAME", "TABLE_SCHEMA"],
  "clearanceLevel": "Standard"
}
```

**Success Response (200 OK):**
```json
{
  "isValid": true,
  "failureReason": null,
  "securityRisks": [],
  "warnings": [],
  "recommendations": [],
  "correlationId": "correlation-id-guid",
  "timestamp": "2024-11-23T12:00:00Z"
}
```

---

#### POST `/governance/authorize`
**Summary:** Test agent authorization through governance  
**Authentication:** Optional  
**Query Parameters:**
- `agentId` (string): Agent identifier
- `requestedTables` (string[]): Tables to access
- `clearanceLevel` (enum): Agent clearance level (Restricted=0, Standard=1, Elevated=2, Administrator=3)

**Success Response (200 OK):**
```json
{
  "isAuthorized": true,
  "denialReason": null,
  "grantedClearanceLevel": "Standard",
  "authorizedTables": ["dbo.Documents", "dbo.Users"],
  "rateLimit": {
    "requestsPerMinute": 60,
    "requestsPerHour": 3600,
    "remainingRequests": 45,
    "resetAt": "2024-11-23T13:00:00Z",
    "isExceeded": false
  },
  "expiresAt": "2024-11-24T12:00:00Z",
  "timestamp": "2024-11-23T12:00:00Z"
}
```

---

## Authentication & Authorization

### JWT Authentication

All protected endpoints require a Bearer token in the Authorization header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

#### JWT Token Claims

The JWT token includes the following claims:

| Claim | Type | Description |
|-------|------|-------------|
| `sub` (NameIdentifier) | GUID | User ID |
| `email` | String | User email address |
| `name` | String | User display name |
| `role` | String (array) | User roles |
| `SecurityClearance` | String | Security clearance level |

#### Token Configuration

- **Issuer:** `Enterprise.Documentation.Api`
- **Audience:** `Enterprise.Documentation.Client`
- **Expiration:** 8 hours (configurable)
- **Algorithm:** HS256 (HMAC-SHA256)
- **Secret Key:** Configured in `appsettings.json` (JwtSettings:SecretKey)

### Authorization Roles

The system supports the following roles:

| Role | Description | Permissions |
|------|-------------|-------------|
| **User** | Standard user | Create/read documents, search |
| **Manager** | Manager role | Approve documents, manage team |
| **Administrator** | System administrator | Full access, user management |

### Authorization Attributes

```csharp
[Authorize]                    // Requires authentication
[Authorize(Roles = "Manager")] // Requires specific role
[AllowAnonymous]               // No authentication required
```

---

## API Versioning

### Current Version: v1

Versioning is configured in Swagger/OpenAPI with support for future versions.

```csharp
c.SwaggerDoc("v1", new OpenApiInfo 
{ 
    Title = "Enterprise Documentation Platform API",
    Version = "v1"
});
```

**Future Versioning Strategy:**
- Minor updates: v1.x (backward compatible)
- Major breaking changes: v2 (new API version)
- Deprecated endpoints will be marked with `[Obsolete]` attribute

---

## Middleware Configuration

### Configured Middleware (in order)

1. **HTTPS Redirection** - `app.UseHttpsRedirection()`
   - Redirects HTTP requests to HTTPS

2. **Authentication** - `app.UseAuthentication()`
   - Validates JWT tokens in request headers

3. **Authorization** - `app.UseAuthorization()`
   - Enforces role-based access control

4. **Governance Middleware** - Custom middleware
   - Adds correlation ID to requests (X-Correlation-ID header)
   - Adds governance headers to responses
   - Tracks all API requests for audit trail

5. **Controller Routing** - `app.MapControllers()`
   - Routes requests to appropriate controllers

### Custom Governance Middleware

```csharp
app.Use(async (context, next) =>
{
    // Add correlation ID to all requests
    if (!context.Request.Headers.ContainsKey("X-Correlation-ID"))
    {
        context.Request.Headers["X-Correlation-ID"] = Guid.NewGuid().ToString();
    }
    
    // Add governance response headers
    context.Response.Headers["X-Governance-Protected"] = "true";
    context.Response.Headers["X-Platform-Version"] = "V2";
    
    await next();
});
```

---

## Swagger/OpenAPI Setup

### Swagger Configuration

Swagger UI is available at `/swagger/ui.html` in development environments.

### OpenAPI Schema

The API exposes a complete OpenAPI 3.0 schema with:

- **Full endpoint documentation** with descriptions
- **Request/response models** with examples
- **Authentication configuration** (JWT Bearer)
- **Error responses** with status codes
- **Security definitions** for API security

### Custom Schema Handling

The configuration uses custom schema ID generator to avoid naming conflicts:

```csharp
c.CustomSchemaIds(type =>
{
    if (type.FullName?.Contains("Core.Domain.ValueObjects") == true)
        return $"Domain{type.Name}";
    if (type.FullName?.Contains("Shared.Contracts.DTOs") == true)
        return $"Dto{type.Name}";
    return type.Name;
});
```

### Security Definition

Bearer token authentication is fully configured in Swagger:

```csharp
c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Description = "JWT Authorization header using the Bearer scheme",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer"
});
```

---

## Data Models & DTOs

### Base DTO

All DTOs inherit from `BaseDto`:

```csharp
public abstract class BaseDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
    public string Version { get; set; } = "1.0";
    public Dictionary<string, object>? Metadata { get; set; }
}
```

### Document DTOs

#### DocumentDto
Complete document representation with all metadata.

**Properties:**
- `id` (string): Unique identifier
- `title` (string): Document title
- `documentType` (string): Type of document
- `status` (DocumentStatus): Draft, UnderReview, Approved, Published, Archived, Deleted
- `content` (string): Document content
- `contentType` (string): MIME type
- `size` (long): Document size in bytes
- `tags` (string[]): Document tags
- `createdBy` (string): Creator user ID
- `modifiedBy` (string): Last modifier user ID
- `modifiedAt` (DateTimeOffset): Last modification time
- `documentVersion` (int): Version number
- `containsPII` (bool): Whether PII is present
- `securityLevel` (SecurityClassification): Public, Internal, Confidential, Restricted
- `approvalStatus` (ApprovalStatus): NotRequired, Pending, Approved, Rejected, Expired
- `relatedDocuments` (string[]): Related document IDs
- `templateId` (string): Template ID used
- `storagePath` (string): Storage location

#### CreateDocumentRequest
Request model for creating documents.

#### UpdateDocumentRequest
Request model for updating documents.

#### DocumentOperationResponse
Operation result with status and messages.

### Batch Processing DTOs

#### BatchJobDto
```csharp
public record BatchJobDto(
    Guid BatchId,
    string SourceType,
    string DatabaseName,
    string SchemaName,
    string? SourcePath,
    string Status,
    int TotalItems,
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    int RequiresReviewCount,
    double ProgressPercentage,
    double AverageConfidence,
    DateTime StartedAt,
    DateTime? CompletedAt,
    TimeSpan? Duration,
    TimeSpan? EstimatedTimeRemaining,
    string? ErrorMessage,
    BatchProcessingOptions Options
);
```

#### BatchJobItemDto
```csharp
public record BatchJobItemDto(
    Guid ItemId,
    Guid BatchId,
    string ObjectName,
    string? ObjectType,
    string Status,
    string? GeneratedDocId,
    double? ConfidenceScore,
    string ConfidenceLevel,
    bool RequiresHumanReview,
    List<string> ValidationWarnings,
    string? DocumentPath,
    bool IsVectorIndexed,
    DateTime? ProcessedAt,
    string? ErrorMessage
);
```

#### PaginatedResult<T>
Generic pagination wrapper.

```csharp
public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
```

### Batch Processing Options

```csharp
public class BatchProcessingOptions
{
    // Metadata Extraction
    public bool ExtractMetadata { get; set; } = true;
    public bool UseOpenAIEnhancement { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.85;
    public bool RequireHumanReviewBelowThreshold { get; set; } = true;

    // DocId Generation
    public bool GenerateDocId { get; set; } = true;
    public bool ValidateDocIdUniqueness { get; set; } = true;

    // File Operations
    public bool MoveToCorrectPath { get; set; } = true;
    public bool RenameFilesToDocId { get; set; } = true;
    public bool BackupOriginals { get; set; } = true;

    // MasterIndex
    public bool PopulateMasterIndex { get; set; } = true;
    public bool CalculateQualityScores { get; set; } = true;

    // Vector Indexing
    public bool GenerateEmbeddings { get; set; } = true;
    public bool EnableSemanticSearch { get; set; } = true;

    // Approval Workflow
    public bool QueueForApproval { get; set; } = false;
    public bool RequireApprovalForLowConfidence { get; set; } = true;

    // Performance
    public int MaxParallelTasks { get; set; } = 4;
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    // Notifications
    public bool SendNotifications { get; set; } = true;
    public bool NotifyOnCompletion { get; set; } = true;
    public bool NotifyOnErrors { get; set; } = true;
    public List<string> NotificationRecipients { get; set; } = new();
}
```

### Governance DTOs

#### GovernanceQueryRequest
```csharp
public record GovernanceQueryRequest
{
    public required string AgentId { get; init; }
    public required string AgentName { get; init; }
    public required string AgentPurpose { get; init; }
    public required string DatabaseName { get; init; }
    public required string SqlQuery { get; init; }
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public IReadOnlyList<string> RequestedTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequestedColumns { get; init; } = Array.Empty<string>();
    public AgentClearanceLevel ClearanceLevel { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public DateTime RequestTimestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromSeconds(30);
    public bool ApplyDataMasking { get; init; } = true;
}
```

#### Agent Clearance Levels

| Level | Description | PII Masking |
|-------|-------------|-------------|
| **Restricted** | Minimal access | 95% masked |
| **Standard** | Standard operations | 60% masked |
| **Elevated** | Sensitive operations | 30% masked |
| **Administrator** | Full access | Audit-only masking |

---

## Error Handling

### HTTP Status Codes

The API uses standard HTTP status codes:

| Code | Meaning | Usage |
|------|---------|-------|
| **200** | OK | Successful GET/PUT requests |
| **201** | Created | Successful POST requests |
| **204** | No Content | Successful operations with no response body |
| **400** | Bad Request | Invalid request parameters or data |
| **401** | Unauthorized | Missing or invalid authentication |
| **403** | Forbidden | Authenticated but insufficient permissions |
| **404** | Not Found | Resource not found |
| **500** | Server Error | Unexpected server error |
| **503** | Service Unavailable | Service temporarily unavailable |

### Error Response Format

All error responses follow a consistent format:

```json
{
  "error": "Error message describing the issue",
  "message": "Additional details (optional)",
  "timestamp": "2024-11-23T12:00:00Z"
}
```

Or for validation errors:

```json
{
  "error": "Validation failed",
  "validationErrors": [
    "Field 'email' is required",
    "Field 'password' must be at least 8 characters"
  ],
  "timestamp": "2024-11-23T12:00:00Z"
}
```

### Exception Handling

- **ArgumentException**: Returns `400 Bad Request`
- **InvalidOperationException**: Returns `400 Bad Request`
- **KeyNotFoundException**: Returns `404 Not Found`
- **DirectoryNotFoundException**: Returns `404 Not Found`
- **FileNotFoundException**: Returns `404 Not Found`
- **General Exceptions**: Returns `500 Internal Server Error`

All errors are logged with correlation ID for tracking.

---

## Rate Limiting & Governance

### Rate Limiting

The API implements rate limiting through the Data Governance layer:

#### Rate Limit Configuration by Clearance Level

| Clearance Level | Requests/Minute | Requests/Hour | Notes |
|---|---|---|---|
| **Restricted** | 10 | 500 | Limited access with heavy restrictions |
| **Standard** | 60 | 3600 | Default rate limit |
| **Elevated** | 300 | 10000 | High-volume operations |
| **Administrator** | Unlimited | Unlimited | Full access for admin tasks |

#### Rate Limit Headers (in response)

```
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 2024-11-23T13:00:00Z
X-Governance-Protected: true
```

### Governance Security

The platform implements comprehensive data governance:

#### 1. Query Validation
- SQL injection prevention
- Unauthorized table access detection
- Dangerous pattern detection
- PII column detection

#### 2. Authorization Checks
- Role-based access control (RBAC)
- Agent clearance level validation
- Table-level access permissions
- Data masking based on clearance

#### 3. PII Detection
- Automatic detection of Personally Identifiable Information
- Data masking for non-authorized users
- Audit trail for PII access
- Compliance reporting

#### 4. Audit Trail
- All database access logged
- Correlation ID tracking
- User action audit trail
- Immutable audit logs

#### Governance Endpoints

Two endpoints are provided for testing governance:

- **POST `/governance/validate`** - Validate query security
- **POST `/governance/authorize`** - Test agent authorization

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-that-is-at-least-32-characters-long-for-production",
    "Issuer": "Enterprise.Documentation.Api",
    "Audience": "Enterprise.Documentation.Client",
    "ExpirationHours": 8
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

### Dependency Injection

The API is configured with the following services:

```csharp
// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(/* configuration */);

// Governance Services
builder.Services.AddScoped<IDataGovernanceProxy, DataGovernanceProxy>();
builder.Services.AddScoped<GovernanceSecurityEngine>();
builder.Services.AddScoped<GovernancePIIDetector>();
builder.Services.AddScoped<GovernanceAuditLogger>();
builder.Services.AddScoped<GovernanceAuthorizationEngine>();

// Application Services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthorizationService, SimpleAuthorizationService>();
builder.Services.AddAutoMapper(/* profiles */);

// CQRS & Validation
builder.Services.AddMediatR(/* configuration */);
builder.Services.AddValidatorsFromAssemblyContaining<GovernanceQueryRequest>();

// Infrastructure
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHangfireServices(builder.Configuration);

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* JWT configuration */);

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
```

### Background Job Processing (Hangfire)

#### Configuration
- **Storage**: SQL Server
- **Queue**: Supports multiple queues (default, critical, batch-processing, vector-indexing)
- **Workers**: CPU count × 2
- **Retry Policy**: 3 attempts with 30s, 60s, 120s delays

#### Recurring Jobs
- Cleanup old batch jobs (90 days) - Daily at 2 AM
- Update vector index statistics - Hourly
- Generate weekly batch report - Weekly on Monday at 9 AM

#### Dashboard
- Available at `/hangfire`
- Authorization filter for security
- Configurable per environment

---

## Summary of Key Features

### API Architecture
- **Pattern**: REST API with CQRS
- **Framework**: ASP.NET Core 8.0
- **Authentication**: JWT Bearer tokens
- **Orchestration**: MediatR for command/query handling
- **Background Jobs**: Hangfire for async processing
- **Validation**: FluentValidation

### Security
- JWT-based authentication
- Role-based authorization (RBAC)
- Data governance layer (mandatory for all DB access)
- PII detection and masking
- SQL injection prevention
- Comprehensive audit trails
- Rate limiting by clearance level

### Data Management
- CQRS pattern for separation of concerns
- Repository pattern for data access
- Entity Value Objects for domain modeling
- Specification pattern for queries
- Unit of Work pattern for transactions

### Monitoring & Observability
- Structured logging with correlation IDs
- OpenTelemetry integration
- Hangfire dashboard for job monitoring
- Request/response logging
- Governance audit trails
- Performance metrics

---

## Quick Reference

### Authenticate User
```bash
curl -X POST https://api.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "password123"
  }'
```

### Create Document (with Token)
```bash
curl -X POST https://api.example.com/api/documents \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "My Document",
    "documentType": "Report",
    "content": "Document content..."
  }'
```

### Search Documents
```bash
curl -X GET "https://api.example.com/api/documents/search?searchTerm=api&pageNumber=1&pageSize=20"
```

### Start Batch Processing
```bash
curl -X POST https://api.example.com/api/batchprocessing/schema \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "database": "IRFS1",
    "schema": "gwpc",
    "userId": "00000000-0000-0000-0000-000000000000"
  }'
```

---

**Document Version**: 1.0  
**Last Updated**: 2024-11-23  
**API Version**: v1
