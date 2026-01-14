using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Services;

/// <summary>
/// Swagger document filter for health check endpoints
/// </summary>
public class HealthCheckDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Add health check endpoints to Swagger documentation
        var healthCheckPath = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Health" } },
                    Summary = "Health Check",
                    Description = "Returns the health status of the application and its dependencies",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "Healthy",
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["application/json"] = new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchema
                                    {
                                        Type = "object",
                                        Properties = new Dictionary<string, OpenApiSchema>
                                        {
                                            ["status"] = new OpenApiSchema { Type = "string" },
                                            ["totalDuration"] = new OpenApiSchema { Type = "string" },
                                            ["entries"] = new OpenApiSchema { Type = "object" }
                                        }
                                    }
                                }
                            }
                        },
                        ["503"] = new OpenApiResponse { Description = "Unhealthy" }
                    }
                }
            }
        };
        
        swaggerDoc.Paths.Add("/health", healthCheckPath);
        
        // Add health check ready endpoint
        var readyPath = new OpenApiPathItem
        {
            Operations = new Dictionary<OperationType, OpenApiOperation>
            {
                [OperationType.Get] = new OpenApiOperation
                {
                    Tags = new List<OpenApiTag> { new() { Name = "Health" } },
                    Summary = "Readiness Check",
                    Description = "Returns whether the application is ready to serve requests",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "Ready" },
                        ["503"] = new OpenApiResponse { Description = "Not Ready" }
                    }
                }
            }
        };
        
        swaggerDoc.Paths.Add("/health/ready", readyPath);
    }
}