using Core.Application.Interfaces;
using Core.Application.Services;
using Enterprise.Documentation.Core.Application.Services;
using Core.Infrastructure.Data;
using Core.Infrastructure.Services;
using Enterprise.Documentation.Core.Infrastructure.Documents;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Api.Hubs;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core Services
        services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.IDocumentGenerationPipeline, DocumentGenerationPipeline>();
        services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.IApprovalService, ApprovalService>();
        services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.ISchemaMetadataService, SchemaMetadataService>();
        services.AddScoped<Core.Application.Interfaces.ITierClassifierService, TierClassifierService>();
        services.AddScoped<Core.Application.Interfaces.ITemplateSelector, TemplateSelector>();
        services.AddScoped<Core.Application.Interfaces.INodeJsTemplateExecutor, NodeJsTemplateExecutor>();

        // Document Services
        services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.IDocxCustomPropertiesService, DocxCustomPropertiesService>();

        // Database Context
        services.AddDbContext<DocumentationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(120);
            });
        });

        // Redis Cache (optional)
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "DocumentationPlatform";
            });
            services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddScoped<Enterprise.Documentation.Core.Application.Interfaces.ICacheService, MemoryCacheService>();
        }

        // SignalR
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.HandshakeTimeout = TimeSpan.FromSeconds(30);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        // Health Checks
        services.AddHealthChecks()
            .AddCheck<Core.Infrastructure.Services.HealthCheckService>("platform-health");

        return services;
    }

    public static IServiceCollection AddDocumentationSwagger(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Enterprise Documentation Platform API",
                Version = "v2.0.0",
                Description = "End-to-end document generation and approval workflow API",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "Enterprise Documentation Team",
                    Email = "docs-team@enterprise.com"
                }
            });

            // Include XML comments
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlFile in xmlFiles)
            {
                options.IncludeXmlComments(xmlFile);
            }

            // Custom operation filters
            options.OperationFilter<ApprovalOperationFilter>();
            options.DocumentFilter<HealthCheckDocumentFilter>();
        });

        return services;
    }

    public static WebApplication ConfigureDocumentationPipeline(this WebApplication app)
    {
        // Development middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Documentation Platform API V1");
                options.RoutePrefix = "api-docs";
                options.EnableDeepLinking();
                options.EnableFilter();
                options.DisplayOperationId();
            });
            
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        // Security middleware
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        // CORS (if needed)
        app.UseCors(policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://yourdomain.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // For SignalR
        });

        // Static files for document downloads
        app.UseStaticFiles();

        // API routes
        app.MapControllers();

        // SignalR hub
        app.MapHub<ApprovalHub>("/approvalHub");

        // Health checks
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(x => new
                    {
                        name = x.Key,
                        status = x.Value.Status.ToString(),
                        exception = x.Value.Exception?.Message,
                        duration = x.Value.Duration.ToString()
                    })
                });
                await context.Response.WriteAsync(response);
            }
        });

        return app;
    }
}