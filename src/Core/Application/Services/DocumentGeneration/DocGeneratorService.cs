// <copyright file="DocGeneratorService.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using System.Text;
using Enterprise.Documentation.Core.Application.Interfaces.Services;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;
using Enterprise.Documentation.Core.Domain.Entities;

using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Enhanced service for generating documents using OpenXML templates.
/// </summary>
public class DocGeneratorService : IDocGeneratorService
{
    private readonly ILogger<DocGeneratorService> _logger;

    public DocGeneratorService(ILogger<DocGeneratorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a document based on the template type and data.
    /// </summary>
    /// <param name="templateType">The type of template to use.</param>
    /// <param name="data">The data to populate the template with.</param>
    /// <returns>A memory stream containing the generated document.</returns>
    public async Task<MemoryStream> GenerateDocumentAsync(string templateType, object data)
    {
        _logger.LogInformation("Generating document for template type: {TemplateType}", templateType);

        var stream = new MemoryStream();

        try
        {
            switch (templateType.ToUpperInvariant())
            {
                case "BUSINESSREQUEST":
                case "BR":
                    if (data is BusinessRequestTemplate.BusinessRequestData brData)
                    {
                        BusinessRequestTemplate.Generate(stream, brData);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid data type for Business Request template. Expected BusinessRequestData, got {data?.GetType().Name}");
                    }
                    break;

                case "ENHANCEMENT":
                case "EN":
                    if (data is EnhancementTemplate.EnhancementData enData)
                    {
                        EnhancementTemplate.Generate(stream, enData);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid data type for Enhancement template. Expected EnhancementData, got {data?.GetType().Name}");
                    }
                    break;

                case "DEFECT":
                case "DF":
                    if (data is DefectTemplate.DefectData dfData)
                    {
                        DefectTemplate.Generate(stream, dfData);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid data type for Defect template. Expected DefectData, got {data?.GetType().Name}");
                    }
                    break;

                case "STOREDPROCEDURE":
                case "SP":
                    if (data is StoredProcedureTemplate.StoredProcedureData spData)
                    {
                        var spTemplate = new StoredProcedureTemplate();
                        var generatedStream = spTemplate.GenerateDocument(spData);
                        generatedStream.CopyTo(stream);
                        generatedStream.Dispose();
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid data type for Stored Procedure template. Expected StoredProcedureData, got {data?.GetType().Name}");
                    }
                    break;

                default:
                    throw new ArgumentException($"Unsupported template type: {templateType}");
            }

            stream.Position = 0;
            _logger.LogInformation("Document generated successfully for template type: {TemplateType}", templateType);
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating document for template type: {TemplateType}", templateType);
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates sample data for testing templates.
    /// </summary>
    /// <param name="templateType">The template type to create sample data for.</param>
    /// <returns>Sample data object.</returns>
    public object CreateSampleData(string templateType)
    {
        return templateType.ToUpperInvariant() switch
        {
            "BUSINESSREQUEST" or "BR" => new BusinessRequestTemplate.BusinessRequestData
            {
                Title = "Enterprise Customer Portal Enhancement",
                DocumentId = "BR-2024-001",
                Jira = "PROJ-1234",
                Status = "In Review",
                DateRequested = DateTime.Now.ToString("yyyy-MM-dd"),
                ReportedBy = "John Smith",
                AssignedTo = "Development Team",
                ExecutiveSummary = "This business request outlines the need for enhancing our customer portal to improve user experience and operational efficiency.",
                BusinessJustification = "Current customer portal has usability issues resulting in increased support tickets and customer dissatisfaction.",
                InScope = new List<string>
                {
                    "User interface redesign",
                    "Performance optimization",
                    "Mobile responsiveness",
                    "Single sign-on integration"
                },
                OutOfScope = new List<string>
                {
                    "Third-party integrations",
                    "Data migration from legacy systems"
                },
                SuccessCriteria = new List<string>
                {
                    "Reduce support tickets by 30%",
                    "Improve page load times by 50%",
                    "Achieve 95% mobile compatibility"
                },
                FunctionalRequirements = new List<BusinessRequestTemplate.FunctionalRequirement>
                {
                    new() { Id = "001", Title = "User Authentication", Description = "Users must be able to log in using SSO", AcceptanceCriteria = "Login completes within 3 seconds" },
                    new() { Id = "002", Title = "Dashboard View", Description = "Users see personalized dashboard", AcceptanceCriteria = "Dashboard loads in under 2 seconds" }
                },
                Budget = new BusinessRequestTemplate.BudgetInfo
                {
                    EstimatedCost = "$50,000",
                    CostBreakdown = "Development: $30,000, Testing: $10,000, Infrastructure: $10,000",
                    RoiProjection = "Expected ROI of 200% within 12 months"
                }
            },
            
            "ENHANCEMENT" or "EN" => new EnhancementTemplate.EnhancementData
            {
                Title = "Real-time Notification System",
                DocumentId = "EN-2024-002",
                Jira = "PROJ-5678",
                Status = "In Development", 
                DateRequested = DateTime.Now.ToString("yyyy-MM-dd"),
                ReportedBy = "Sarah Johnson",
                AssignedTo = "Backend Team",
                CurrentState = "Current system uses email notifications with significant delays",
                ProposedEnhancement = "Implement real-time push notifications using WebSocket technology",
                BusinessValue = "Improve user engagement and response times by 80%",
                UserStories = new List<EnhancementTemplate.UserStory>
                {
                    new() 
                    { 
                        Id = "US001", 
                        Title = "Instant Notifications",
                        AsA = "user", 
                        IWant = "to receive instant notifications", 
                        SoThat = "I can respond quickly to important events",
                        AcceptanceCriteria = new List<string> { "Notifications appear within 1 second", "User can customize notification preferences" }
                    }
                },
                TechnicalApproach = "Implement SignalR for real-time communication with fallback to Server-Sent Events",
                TestingStrategy = new EnhancementTemplate.TestingStrategy
                {
                    UnitTesting = "Test notification delivery mechanisms",
                    IntegrationTesting = "Test end-to-end notification flow",
                    UserAcceptanceTesting = "User testing for notification preferences"
                }
            },
            
            "DEFECT" or "DF" => new DefectTemplate.DefectData
            {
                Title = "Login Page Timeout Error",
                DocumentId = "DF-2024-003",
                Jira = "BUG-9012",
                Status = "Open",
                DateRequested = DateTime.Now.ToString("yyyy-MM-dd"),
                ReportedBy = "QA Team",
                AssignedTo = "Frontend Team",
                ProblemDescription = "Users experience timeout errors when attempting to log in during peak hours",
                StepsToReproduce = new List<string>
                {
                    "Navigate to login page",
                    "Enter valid credentials",
                    "Click login button",
                    "Wait for response (occurs during 9-11 AM)"
                },
                ExpectedResult = "User should be successfully logged in within 3 seconds",
                ActualResult = "Page displays timeout error after 30 seconds",
                Environment = new DefectTemplate.EnvironmentInfo
                {
                    OperatingSystem = "Windows 10, macOS 12.0",
                    BrowserVersion = "Chrome 118.0, Firefox 119.0",
                    ApplicationVersion = "v2.1.4"
                },
                Severity = "High",
                Priority = "P1",
                Impact = "Blocks user access during peak business hours"
            },
            
            "STOREDPROCEDURE" or "SP" => StoredProcedureTemplate.CreateSampleData("SampleProcedure", "CREATE PROCEDURE SampleProcedure AS BEGIN SELECT 1 END"),
            
            _ => throw new ArgumentException($"Unsupported template type: {templateType}")
        };
    }

    /// <summary>
    /// Gets available template types.
    /// </summary>
    /// <returns>List of supported template types.</returns>
    public List<string> GetAvailableTemplateTypes()
    {
        return new List<string>
        {
            "BusinessRequest",
            "Enhancement", 
            "Defect",
            "StoredProcedure"
        };
    }

    /// <summary>
    /// Validates template data before generation.
    /// </summary>
    /// <param name="templateType">The template type.</param>
    /// <param name="data">The data to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool ValidateTemplateData(string templateType, object data)
    {
        try
        {
            return templateType.ToUpperInvariant() switch
            {
                "BUSINESSREQUEST" or "BR" => data is BusinessRequestTemplate.BusinessRequestData brData && !string.IsNullOrEmpty(brData.Title),
                "ENHANCEMENT" or "EN" => data is EnhancementTemplate.EnhancementData enData && !string.IsNullOrEmpty(enData.Title),
                "DEFECT" or "DF" => data is DefectTemplate.DefectData dfData && !string.IsNullOrEmpty(dfData.Title),
                "STOREDPROCEDURE" or "SP" => data is StoredProcedureTemplate.StoredProcedureData spData && !string.IsNullOrEmpty(spData.SpName),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates documentation for a given source.
    /// </summary>
    public async Task<string> GenerateDocumentationAsync(string source, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating documentation for source: {Source}", source);
        
        // Stub implementation - return basic documentation
        await Task.Delay(1, cancellationToken);
        return $"# Documentation\n\nGenerated documentation for: {source}\n\nTimestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
    }
}