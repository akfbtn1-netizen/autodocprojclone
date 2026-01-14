// <copyright file="EnhancementTemplate.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Template for generating enhancement documents.
/// </summary>
public static class EnhancementTemplate
{
    /// <summary>
    /// Generates an enhancement Word document.
    /// </summary>
    /// <param name="stream">The output stream for the document.</param>
    /// <param name="data">The enhancement data.</param>
    public static void Generate(Stream stream, EnhancementData data)
    {
        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var body = mainPart.Document.Body!;

        TemplateHelper.SetMargins(doc);
        TemplateHelper.AddHeader(body, data.Title, "Enhancement Request", 
            data.DocumentId, data.Jira, data.Status, data.DateRequested, data.ReportedBy, data.AssignedTo);

        TemplateHelper.AddDivider(body);

        TemplateHelper.AddHeading(body, "Current State");
        TemplateHelper.AddContent(body, data.CurrentState);

        TemplateHelper.AddHeading(body, "Proposed Enhancement");
        TemplateHelper.AddContent(body, data.ProposedEnhancement);

        TemplateHelper.AddHeading(body, "Business Value");
        TemplateHelper.AddContent(body, data.BusinessValue);

        TemplateHelper.AddHeading(body, "User Stories");
        foreach (var story in data.UserStories)
        {
            TemplateHelper.AddSubheader(body, $"Story {story.Id}: {story.Title}");
            TemplateHelper.AddContent(body, $"As a {story.AsA}, I want {story.IWant} so that {story.SoThat}");
            
            if (story.AcceptanceCriteria.Any())
            {
                TemplateHelper.AddSubheader(body, "Acceptance Criteria:");
                foreach (var criteria in story.AcceptanceCriteria)
                {
                    TemplateHelper.AddBullet(body, criteria);
                }
            }
        }

        TemplateHelper.AddHeading(body, "Technical Approach");
        TemplateHelper.AddContent(body, data.TechnicalApproach);

        if (!string.IsNullOrEmpty(data.CodeExamples))
        {
            TemplateHelper.AddSubheader(body, "Code Examples");
            TemplateHelper.AddCodeBlock(body, data.CodeExamples);
        }

        TemplateHelper.AddHeading(body, "Implementation Plan");
        if (data.ImplementationSteps.Any())
        {
            foreach (var step in data.ImplementationSteps)
            {
                TemplateHelper.AddSubheader(body, $"Phase {step.Phase}: {step.Title}");
                TemplateHelper.AddContent(body, step.Description);
                TemplateHelper.AddContent(body, $"Estimated Effort: {step.EstimatedEffort}");
                TemplateHelper.AddContent(body, $"Dependencies: {string.Join(", ", step.Dependencies)}");
            }
        }

        TemplateHelper.AddHeading(body, "Testing Strategy");
        TemplateHelper.AddSubheader(body, "Unit Testing");
        TemplateHelper.AddContent(body, data.TestingStrategy.UnitTesting);
        
        TemplateHelper.AddSubheader(body, "Integration Testing");
        TemplateHelper.AddContent(body, data.TestingStrategy.IntegrationTesting);
        
        TemplateHelper.AddSubheader(body, "User Acceptance Testing");
        TemplateHelper.AddContent(body, data.TestingStrategy.UserAcceptanceTesting);

        TemplateHelper.AddHeading(body, "Performance Impact");
        TemplateHelper.AddContent(body, data.PerformanceImpact);

        TemplateHelper.AddHeading(body, "Security Considerations");
        TemplateHelper.AddContent(body, data.SecurityConsiderations);

        TemplateHelper.AddHeading(body, "Rollback Plan");
        TemplateHelper.AddContent(body, data.RollbackPlan);

        TemplateHelper.AddHeading(body, "Success Metrics");
        if (data.SuccessMetrics.Any())
        {
            foreach (var metric in data.SuccessMetrics)
            {
                TemplateHelper.AddBullet(body, metric);
            }
        }

        TemplateHelper.AddHeading(body, "Risks and Mitigation");
        if (data.Risks.Any())
        {
            foreach (var risk in data.Risks)
            {
                TemplateHelper.AddSubheader(body, $"Risk: {risk.Description}");
                TemplateHelper.AddContent(body, $"Impact: {risk.Impact}");
                TemplateHelper.AddContent(body, $"Probability: {risk.Probability}");
                TemplateHelper.AddContent(body, $"Mitigation: {risk.Mitigation}");
            }
        }
    }

    /// <summary>
    /// Data structure for enhancement template.
    /// </summary>
    public class EnhancementData
    {
        public string Title { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string Jira { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DateRequested { get; set; } = string.Empty;
        public string ReportedBy { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string CurrentState { get; set; } = string.Empty;
        public string ProposedEnhancement { get; set; } = string.Empty;
        public string BusinessValue { get; set; } = string.Empty;
        public List<UserStory> UserStories { get; set; } = new();
        public string TechnicalApproach { get; set; } = string.Empty;
        public string CodeExamples { get; set; } = string.Empty;
        public List<ImplementationStep> ImplementationSteps { get; set; } = new();
        public TestingStrategy TestingStrategy { get; set; } = new();
        public string PerformanceImpact { get; set; } = string.Empty;
        public string SecurityConsiderations { get; set; } = string.Empty;
        public string RollbackPlan { get; set; } = string.Empty;
        public List<string> SuccessMetrics { get; set; } = new();
        public List<RiskItem> Risks { get; set; } = new();
    }

    public class UserStory
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string AsA { get; set; } = string.Empty;
        public string IWant { get; set; } = string.Empty;
        public string SoThat { get; set; } = string.Empty;
        public List<string> AcceptanceCriteria { get; set; } = new();
    }

    public class ImplementationStep
    {
        public int Phase { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EstimatedEffort { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new();
    }

    public class TestingStrategy
    {
        public string UnitTesting { get; set; } = string.Empty;
        public string IntegrationTesting { get; set; } = string.Empty;
        public string UserAcceptanceTesting { get; set; } = string.Empty;
    }

    public class RiskItem
    {
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Probability { get; set; } = string.Empty;
        public string Mitigation { get; set; } = string.Empty;
    }
}