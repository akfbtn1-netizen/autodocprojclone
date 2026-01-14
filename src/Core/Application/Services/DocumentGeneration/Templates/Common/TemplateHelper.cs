// <copyright file="TemplateHelper.cs" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;

/// <summary>
/// Provides common helper methods for creating OpenXML Word documents.
/// </summary>
public static class TemplateHelper
{
    /// <summary>
    /// Sets standard margins for the document.
    /// </summary>
    /// <param name="doc">The WordprocessingDocument to configure.</param>
    public static void SetMargins(WordprocessingDocument doc)
    {
        var sections = doc.MainDocumentPart!.Document.Body!.Elements<SectionProperties>();
        if (!sections.Any())
        {
            var sectionProps = new SectionProperties();
            doc.MainDocumentPart.Document.Body.Append(sectionProps);
            sections = doc.MainDocumentPart.Document.Body.Elements<SectionProperties>();
        }

        foreach (var section in sections)
        {
            var pageMargin = section.GetFirstChild<PageMargin>();
            if (pageMargin == null)
            {
                pageMargin = new PageMargin();
                section.Append(pageMargin);
            }
            pageMargin.Top = 1080;
            pageMargin.Right = 1080;
            pageMargin.Bottom = 1080;
            pageMargin.Left = 1080;
        }
    }

    /// <summary>
    /// Adds a document header with metadata.
    /// </summary>
    public static void AddHeader(Body body, string title, string subtitle, string docId,
        string jira, string status, string dateRequested, string reportedBy, string assignedTo)
    {
        var headerTable = body.AppendChild(new Table());
        var headerProps = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }
            )
        );
        headerTable.AppendChild(headerProps);

        var row = headerTable.AppendChild(new TableRow());
        
        var leftCell = row.AppendChild(new TableCell());
        leftCell.Append(new TableCellProperties(new TableCellWidth { Width = "3500", Type = TableWidthUnitValues.Dxa }));
        
        var titlePara = leftCell.AppendChild(new Paragraph());
        var titleRun = titlePara.AppendChild(new Run(new Text(title)));
        titleRun.RunProperties = new RunProperties(
            new FontSize { Val = "40" },
            new Bold(),
            new Color { Val = "2C5F8D" }
        );

        var subtitlePara = leftCell.AppendChild(new Paragraph());
        var subtitleRun = subtitlePara.AppendChild(new Run(new Text(subtitle)));
        subtitleRun.RunProperties = new RunProperties(
            new FontSize { Val = "22" },
            new Color { Val = "495057" }
        );

        var docIdPara = leftCell.AppendChild(new Paragraph());
        var docIdRun = docIdPara.AppendChild(new Run(new Text($"Document ID: {docId}")));
        docIdRun.RunProperties = new RunProperties(new FontSize { Val = "20" }, new Color { Val = "495057" });

        var rightCell = row.AppendChild(new TableCell());
        rightCell.Append(new TableCellProperties(
            new TableCellWidth { Width = "2000", Type = TableWidthUnitValues.Dxa },
            new Shading { Fill = "F8F9FA" },
            new TableCellBorders(
                new TopBorder { Val = BorderValues.Single, Size = 8, Color = "DEE2E6" },
                new BottomBorder { Val = BorderValues.Single, Size = 8, Color = "DEE2E6" },
                new LeftBorder { Val = BorderValues.Single, Size = 8, Color = "DEE2E6" },
                new RightBorder { Val = BorderValues.Single, Size = 8, Color = "DEE2E6" }
            )
        ));

        AddMetadataItem(rightCell, "Jira:", jira, "495057");
        AddMetadataItem(rightCell, "Status:", status, "28A745");
        AddMetadataItem(rightCell, "Date Requested:", dateRequested, "495057");
        AddMetadataItem(rightCell, "Reported By:", reportedBy, "495057");
        AddMetadataItem(rightCell, "Assigned To:", assignedTo, "495057");
    }

    private static void AddMetadataItem(TableCell cell, string label, string value, string colorHex)
    {
        var para = cell.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { After = "40" });
        
        var labelRun = para.AppendChild(new Run(new Text(label + " ")));
        labelRun.RunProperties = new RunProperties(
            new FontSize { Val = "18" },
            new Bold(),
            new Color { Val = "495057" }
        );

        var valueRun = para.AppendChild(new Run(new Text(value)));
        valueRun.RunProperties = new RunProperties(
            new FontSize { Val = "18" },
            new Bold(),
            new Color { Val = colorHex }
        );
    }

    /// <summary>
    /// Adds a divider line to separate sections.
    /// </summary>
    public static void AddDivider(Body body)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { Before = "160", After = "160" },
            new ParagraphBorders(
                new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "2C5F8D" }
            )
        );
    }

    /// <summary>
    /// Adds a main heading.
    /// </summary>
    public static void AddHeading(Body body, string text)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { Before = "240", After = "160" }
        );
        
        var run = para.AppendChild(new Run(new Text(text)));
        run.RunProperties = new RunProperties(
            new FontSize { Val = "28" },
            new Bold(),
            new Color { Val = "2C5F8D" }
        );
    }

    /// <summary>
    /// Adds a subheader.
    /// </summary>
    public static void AddSubheader(Body body, string text)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { Before = "120", After = "80" }
        );
        
        var run = para.AppendChild(new Run(new Text(text)));
        run.RunProperties = new RunProperties(
            new FontSize { Val = "22" },
            new Bold(),
            new Color { Val = "212529" }
        );
    }

    /// <summary>
    /// Adds regular content text.
    /// </summary>
    public static void AddContent(Body body, string text)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new SpacingBetweenLines { After = "200" }
        );
        
        var run = para.AppendChild(new Run(new Text(text)));
        run.RunProperties = new RunProperties(new FontSize { Val = "22" });
    }

    /// <summary>
    /// Adds a formatted code block.
    /// </summary>
    public static void AddCodeBlock(Body body, string code)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new Indentation { Left = "600" },
            new SpacingBetweenLines { After = "200" },
            new Shading { Fill = "F5F5F5" },
            new ParagraphBorders(
                new LeftBorder { Val = BorderValues.Single, Size = 16, Color = "2C5F8D" }
            )
        );
        
        var run = para.AppendChild(new Run(new Text(code) { Space = SpaceProcessingModeValues.Preserve }));
        run.RunProperties = new RunProperties(
            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize { Val = "18" },
            new Color { Val = "212529" }
        );
    }

    /// <summary>
    /// Adds a bullet point item.
    /// </summary>
    public static void AddBullet(Body body, string text)
    {
        var para = body.AppendChild(new Paragraph());
        para.ParagraphProperties = new ParagraphProperties(
            new Indentation { Left = "720" },
            new SpacingBetweenLines { After = "80" },
            new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = 1 }
            )
        );
        
        var run = para.AppendChild(new Run(new Text(text)));
        run.RunProperties = new RunProperties(new FontSize { Val = "20" });
    }
}