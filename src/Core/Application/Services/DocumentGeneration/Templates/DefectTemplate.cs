// <copyright file="DefectTemplate.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Template for generating defect documents.
/// </summary>
public static class DefectTemplate
{
    /// <summary>
    /// Generates a defect Word document.
    /// </summary>
    /// <param name="stream">The output stream for the document.</param>
    /// <param name="data">The defect data.</param>
    public static void Generate(Stream stream, DefectData data)
    {
        using var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var body = mainPart.Document.Body!;

        TemplateHelper.SetMargins(doc);
        TemplateHelper.AddHeader(body, data.Title, "Defect Report", 
            data.DocumentId, data.Jira, data.Status, data.DateRequested, data.ReportedBy, data.AssignedTo);

        TemplateHelper.AddDivider(body);

        TemplateHelper.AddHeading(body, "Problem Description");
        TemplateHelper.AddContent(body, data.ProblemDescription);

        TemplateHelper.AddHeading(body, "Steps to Reproduce");
        if (data.StepsToReproduce.Any())
        {
            for (int i = 0; i < data.StepsToReproduce.Count; i++)
            {
                TemplateHelper.AddContent(body, $"{i + 1}. {data.StepsToReproduce[i]}");
            }
        }

        TemplateHelper.AddHeading(body, "Expected Result");
        TemplateHelper.AddContent(body, data.ExpectedResult);

        TemplateHelper.AddHeading(body, "Actual Result");
        TemplateHelper.AddContent(body, data.ActualResult);

        TemplateHelper.AddHeading(body, "Environment");
        TemplateHelper.AddSubheader(body, "Operating System");
        TemplateHelper.AddContent(body, data.Environment.OperatingSystem);
        
        TemplateHelper.AddSubheader(body, "Browser/Application Version");
        TemplateHelper.AddContent(body, data.Environment.BrowserVersion);
        
        TemplateHelper.AddSubheader(body, "Application Version");
        TemplateHelper.AddContent(body, data.Environment.ApplicationVersion);

        if (!string.IsNullOrEmpty(data.Environment.AdditionalInfo))
        {
            TemplateHelper.AddSubheader(body, "Additional Environment Information");
            TemplateHelper.AddContent(body, data.Environment.AdditionalInfo);
        }

        TemplateHelper.AddHeading(body, "Severity and Priority");
        TemplateHelper.AddContent(body, $"Severity: {data.Severity}");
        TemplateHelper.AddContent(body, $"Priority: {data.Priority}");
        TemplateHelper.AddContent(body, $"Impact: {data.Impact}");

        if (data.Screenshots.Any())
        {
            TemplateHelper.AddHeading(body, "Screenshots/Attachments");
            foreach (var screenshot in data.Screenshots)
            {
                TemplateHelper.AddBullet(body, screenshot);
            }
        }

        if (!string.IsNullOrEmpty(data.Workaround))
        {
            TemplateHelper.AddHeading(body, "Workaround");
            TemplateHelper.AddContent(body, data.Workaround);
        }

        if (data.RelatedDefects.Any())
        {
            TemplateHelper.AddHeading(body, "Related Defects");
            foreach (var related in data.RelatedDefects)
            {
                TemplateHelper.AddBullet(body, related);
            }
        }

        if (!string.IsNullOrEmpty(data.RootCause))
        {
            TemplateHelper.AddHeading(body, "Root Cause Analysis");
            TemplateHelper.AddContent(body, data.RootCause);
        }

        if (!string.IsNullOrEmpty(data.Resolution))
        {
            TemplateHelper.AddHeading(body, "Resolution");
            TemplateHelper.AddContent(body, data.Resolution);

            if (!string.IsNullOrEmpty(data.CodeChanges))
            {
                TemplateHelper.AddSubheader(body, "Code Changes");
                TemplateHelper.AddCodeBlock(body, data.CodeChanges);
            }
        }

        if (!string.IsNullOrEmpty(data.TestingNotes))
        {
            TemplateHelper.AddHeading(body, "Testing Notes");
            TemplateHelper.AddContent(body, data.TestingNotes);
        }

        if (data.TestCases.Any())
        {
            TemplateHelper.AddHeading(body, "Test Cases");
            foreach (var testCase in data.TestCases)
            {
                TemplateHelper.AddSubheader(body, $"Test Case: {testCase.Name}");
                TemplateHelper.AddContent(body, testCase.Description);
                TemplateHelper.AddContent(body, $"Expected Result: {testCase.ExpectedResult}");
                TemplateHelper.AddContent(body, $"Status: {testCase.Status}");
            }
        }

        if (!string.IsNullOrEmpty(data.PreventionMeasures))
        {
            TemplateHelper.AddHeading(body, "Prevention Measures");
            TemplateHelper.AddContent(body, data.PreventionMeasures);
        }
    }

    /// <summary>
    /// Data structure for defect template.
    /// </summary>
    public class DefectData
    {
        public string Title { get; set; } = string.Empty;
        public string DocumentId { get; set; } = string.Empty;
        public string Jira { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DateRequested { get; set; } = string.Empty;
        public string ReportedBy { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public string ProblemDescription { get; set; } = string.Empty;
        public List<string> StepsToReproduce { get; set; } = new();
        public string ExpectedResult { get; set; } = string.Empty;
        public string ActualResult { get; set; } = string.Empty;
        public EnvironmentInfo Environment { get; set; } = new();
        public string Severity { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public List<string> Screenshots { get; set; } = new();
        public string Workaround { get; set; } = string.Empty;
        public List<string> RelatedDefects { get; set; } = new();
        public string RootCause { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string CodeChanges { get; set; } = string.Empty;
        public string TestingNotes { get; set; } = string.Empty;
        public List<TestCase> TestCases { get; set; } = new();
        public string PreventionMeasures { get; set; } = string.Empty;
    }

    public class EnvironmentInfo
    {
        public string OperatingSystem { get; set; } = string.Empty;
        public string BrowserVersion { get; set; } = string.Empty;
        public string ApplicationVersion { get; set; } = string.Empty;
        public string AdditionalInfo { get; set; } = string.Empty;
    }

    public class TestCase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}