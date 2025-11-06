namespace Shared.Contracts.DTOs;

/// <summary>
/// Base Data Transfer Object with common audit properties.
/// All DTOs should inherit from this to ensure consistent structure.
/// </summary>
public abstract record BaseDto
{
    /// <summary>Unique identifier</summary>
    public Guid Id { get; init; }

    /// <summary>When the record was created</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the record was last updated</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Who created the record</summary>
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Who last updated the record</summary>
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Version for optimistic concurrency</summary>
    public string? Version { get; init; }
}

/// <summary>
/// Generic API response wrapper for consistent response structure.
/// Provides standard success/error handling across all API endpoints.
/// </summary>
/// <typeparam name="T">Response data type</typeparam>
public record ApiResponse<T>
{
    /// <summary>Whether the operation was successful</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Response data (null if operation failed)</summary>
    public T? Data { get; init; }

    /// <summary>Error message if operation failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Detailed error information</summary>
    public Dictionary<string, object>? ErrorDetails { get; init; }

    /// <summary>Request correlation identifier</summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Response timestamp</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Response metadata</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>Creates a successful response</summary>
    public static ApiResponse<T> Success(T data, string? correlationId = null) =>
        new()
        {
            IsSuccess = true,
            Data = data,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
        };

    /// <summary>Creates an error response</summary>
    public static ApiResponse<T> Error(string errorMessage, Dictionary<string, object>? errorDetails = null, string? correlationId = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
        };
}

/// <summary>
/// Non-generic API response for operations that don't return data.
/// </summary>
public record ApiResponse
{
    /// <summary>Whether the operation was successful</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Error message if operation failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Detailed error information</summary>
    public Dictionary<string, object>? ErrorDetails { get; init; }

    /// <summary>Request correlation identifier</summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Response timestamp</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Response metadata</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>Creates a successful response without data</summary>
    public static ApiResponse Success(string? correlationId = null) =>
        new()
        {
            IsSuccess = true,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
        };

    /// <summary>Creates an error response without data</summary>
    public static ApiResponse Error(string errorMessage, Dictionary<string, object>? errorDetails = null, string? correlationId = null) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
        };
}

/// <summary>
/// Paginated response wrapper for list operations.
/// Provides consistent pagination metadata across all list endpoints.
/// </summary>
/// <typeparam name="T">Item type in the collection</typeparam>
public record PagedResponse<T>
{
    /// <summary>Items in the current page</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Current page number (1-based)</summary>
    public int PageNumber { get; init; }

    /// <summary>Number of items per page</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages</summary>
    public int TotalItems { get; init; }

    /// <summary>Total number of pages</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>Whether there is a previous page</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Whether there is a next page</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Number of items in the current page</summary>
    public int ItemCount => Items.Count;

    /// <summary>Starting item number (1-based)</summary>
    public int StartItem => (PageNumber - 1) * PageSize + 1;

    /// <summary>Ending item number (1-based)</summary>
    public int EndItem => Math.Min(StartItem + ItemCount - 1, TotalItems);

    /// <summary>Creates a paginated response</summary>
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalItems) =>
        new()
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems
        };
}

/// <summary>
/// Pagination request parameters for list operations.
/// Provides consistent pagination input across all list endpoints.
/// </summary>
public record PaginationRequest
{
    /// <summary>Page number (1-based, default: 1)</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Items per page (default: 20, max: 100)</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Search query string</summary>
    public string? SearchQuery { get; init; }

    /// <summary>Sort field name</summary>
    public string? SortBy { get; init; }

    /// <summary>Sort direction (asc/desc)</summary>
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    /// <summary>Additional filter parameters</summary>
    public Dictionary<string, object> Filters { get; init; } = new();

    /// <summary>Validates the pagination request</summary>
    public bool IsValid => PageNumber > 0 && PageSize > 0 && PageSize <= 100;

    /// <summary>Calculates the number of items to skip</summary>
    public int Skip => (PageNumber - 1) * PageSize;
}

/// <summary>
/// Sort direction enumeration.
/// </summary>
public enum SortDirection
{
    /// <summary>Ascending order (A-Z, 1-9)</summary>
    Ascending = 0,
    /// <summary>Descending order (Z-A, 9-1)</summary>
    Descending = 1
}

/// <summary>
/// Health check result for service monitoring.
/// Provides consistent health status reporting across all services.
/// </summary>
public record HealthCheckResult
{
    /// <summary>Service name being checked</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>Health status</summary>
    public HealthStatus Status { get; init; }

    /// <summary>Human-readable description</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>How long the check took</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>When the check was performed</summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Additional health data</summary>
    public Dictionary<string, object> Data { get; init; } = new();

    /// <summary>Creates a healthy result</summary>
    public static HealthCheckResult Healthy(string serviceName, string? description = null) =>
        new()
        {
            ServiceName = serviceName,
            Status = HealthStatus.Healthy,
            Description = description ?? "Service is healthy"
        };

    /// <summary>Creates an unhealthy result</summary>
    public static HealthCheckResult Unhealthy(string serviceName, string description, Dictionary<string, object>? data = null) =>
        new()
        {
            ServiceName = serviceName,
            Status = HealthStatus.Unhealthy,
            Description = description,
            Data = data ?? new()
        };

    /// <summary>Creates a degraded result</summary>
    public static HealthCheckResult Degraded(string serviceName, string description, Dictionary<string, object>? data = null) =>
        new()
        {
            ServiceName = serviceName,
            Status = HealthStatus.Degraded,
            Description = description,
            Data = data ?? new()
        };
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    /// <summary>Service is fully operational</summary>
    Healthy = 0,
    /// <summary>Service is operational but with reduced functionality</summary>
    Degraded = 1,
    /// <summary>Service is not operational</summary>
    Unhealthy = 2
}