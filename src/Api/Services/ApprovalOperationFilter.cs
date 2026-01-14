using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Services;

/// <summary>
/// Swagger operation filter for approval endpoints
/// </summary>
public class ApprovalOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType?.Name == "ApprovalController")
        {
            operation.Tags = new List<OpenApiTag> { new() { Name = "Approvals" } };
            
            // Add custom headers for approval operations
            operation.Parameters ??= new List<OpenApiParameter>();
            
            // Add correlation ID header
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Correlation-ID",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "string" },
                Description = "Correlation ID for tracking requests"
            });
            
            // Add user context header for audit
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-User-Context",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "string" },
                Description = "User context information for audit logging"
            });
        }
    }
}