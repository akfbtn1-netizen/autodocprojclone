// <copyright file="BusinessRequestTemplate.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Template for generating business request documents.
/// </summary>
public static class BusinessRequestTemplate
{
    /// <summary>
    /// Generates a business request Word document.
    /// </summary>
    /// <param name="stream">The output stream for the document.</param>
    /// <param name="data">The business request data.</param>
    public static void Generate(Stream stream, BusinessRequestData data)
    {
        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var body = mainPart.Document.Body!;

        TemplateHelper.SetMargins(doc);
        TemplateHelper.AddHeader(body, data.Title, "Business Request", 
            data.DocumentId, data.Jira, data.Status, data.DateRequested, data.ReportedBy, data.AssignedTo);

        TemplateHelper.AddDivider(body);

        TemplateHelper.AddHeading(body, "Executive Summary");
        TemplateHelper.AddContent(body, data.ExecutiveSummary);

        TemplateHelper.AddHeading(body, "Business Justification");
        TemplateHelper.AddContent(body, data.BusinessJustification);

        TemplateHelper.AddHeading(body, "Scope");
        TemplateHelper.AddSubheader(body, "In Scope");
        if (data.InScope.Any())
        {
            foreach (var item in data.InScope)
            {
                TemplateHelper.AddBullet(body, item);
            }
        }

        TemplateHelper.AddSubheader(body, "Out of Scope");
        if (data.OutOfScope.Any())
        {
            foreach (var item in data.OutOfScope)
            {
                TemplateHelper.AddBullet(body, item);
            }
        }

        TemplateHelper.AddHeading(body, "Success Criteria");
        if (data.SuccessCriteria.Any())
        {
            foreach (var criteria in data.SuccessCriteria)
            {
                TemplateHelper.AddBullet(body, criteria);
            }
        }

        TemplateHelper.AddHeading(body, "Functional Requirements");
        foreach (var req in data.FunctionalRequirements)
        {
            TemplateHelper.AddSubheader(body, $"FR-{req.Id}: {req.Title}");
            TemplateHelper.AddContent(body, req.Description);
            
            if (!string.IsNullOrEmpty(req.AcceptanceCriteria))
            {
                TemplateHelper.AddContent(body, $"Acceptance Criteria: {req.AcceptanceCriteria}");
            }
        }

        TemplateHelper.AddHeading(body, "Non-Functional Requirements");
        if (data.NonFunctionalRequirements.Any())
        {
            foreach (var req in data.NonFunctionalRequirements)
            {
                TemplateHelper.AddSubheader(body, req.Category);
                TemplateHelper.AddContent(body, req.Description);
            }
        }

        TemplateHelper.AddHeading(body, "Assumptions");
        if (data.Assumptions.Any())
        {
            foreach (var assumption in data.Assumptions)
            {
                TemplateHelper.AddBullet(body, assumption);
            }
        }

        TemplateHelper.AddHeading(body, "Dependencies");
        if (data.Dependencies.Any())
        {
            foreach (var dependency in data.Dependencies)
            {
                TemplateHelper.AddBullet(body, dependency);
            }
        }

        TemplateHelper.AddHeading(body, "Risks");
        if (data.Risks.Any())
        {
            foreach (var risk in data.Risks)
            {
                TemplateHelper.AddSubheader(body, $"Risk: {risk.Description}");
                TemplateHelper.AddContent(body, $"Likelihood: {risk.Likelihood}");
                TemplateHelper.AddContent(body, $"Impact: {risk.Impact}");
                TemplateHelper.AddContent(body, $"Mitigation: {risk.Mitigation}");
            }
        }

        TemplateHelper.AddHeading(body, "Timeline");
        if (data.Timeline.Any())
        {
            foreach (var milestone in data.Timeline)
            {
                TemplateHelper.AddSubheader(body, milestone.Phase);
                TemplateHelper.AddContent(body, $"Start Date: {milestone.StartDate}");
                TemplateHelper.AddContent(body, $"End Date: {milestone.EndDate}");
                TemplateHelper.AddContent(body, $"Deliverables: {string.Join(", ", milestone.Deliverables)}");
            }
        }

        TemplateHelper.AddHeading(body, "Budget");
        TemplateHelper.AddContent(body, $"Estimated Cost: {data.Budget.EstimatedCost}");
        TemplateHelper.AddContent(body, $"Cost Breakdown: {data.Budget.CostBreakdown}");
        TemplateHelper.AddContent(body, $"ROI Projection: {data.Budget.RoiProjection}");
    }

    /// <summary>
    /// Data structure for business request template.
    /// </summary>
    public class BusinessRequestData
    {
        public string Title { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string Jira { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DateRequested { get; set; } = string.Empty;
        public string ReportedBy { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string BusinessJustification { get; set; } = string.Empty;
        public List<string> InScope { get; set; } = new();
        public List<string> OutOfScope { get; set; } = new();
        public List<string> SuccessCriteria { get; set; } = new();
        public List<FunctionalRequirement> FunctionalRequirements { get; set; } = new();
        public List<NonFunctionalRequirement> NonFunctionalRequirements { get; set; } = new();
        public List<string> Assumptions { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public List<RiskItem> Risks { get; set; } = new();
        public List<TimelinePhase> Timeline { get; set; } = new();
        public BudgetInfo Budget { get; set; } = new();
    }

    public class FunctionalRequirement
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;
    }

    public class NonFunctionalRequirement
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class RiskItem
    {
        public string Description { get; set; } = string.Empty;
        public string Likelihood { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Mitigation { get; set; } = string.Empty;
    }

    public class TimelinePhase
    {
        public string Phase { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public List<string> Deliverables { get; set; } = new();
    }

    public class BudgetInfo
    {
        public string EstimatedCost { get; set; } = string.Empty;
        public string CostBreakdown { get; set; } = string.Empty;
        public string RoiProjection { get; set; } = string.Empty;
    }
}