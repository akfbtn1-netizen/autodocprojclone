using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates.Common;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates
{
    /// <summary>
    /// Stored Procedure template for generating comprehensive stored procedure documentation
    /// with adaptive sections based on complexity scores and QA validation procedures.
    /// Supports automatic documentation detection and version history tracking.
    /// </summary>
    public class StoredProcedureTemplate
    {
        private readonly TemplateHelper _templateHelper;

        public StoredProcedureTemplate()
        {
            _templateHelper = new TemplateHelper();
        }

        /// <summary>
        /// Generates a comprehensive stored procedure documentation document
        /// </summary>
        public MemoryStream GenerateDocument(StoredProcedureData data)
        {
            var stream = new MemoryStream();
            using var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
            
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Set document margins
            _templateHelper.SetMargins(mainPart);

            // Create header with metadata table
            CreateHeader(body, data);
            
            // Add divider
            _templateHelper.AddDivider(body);

            // Recent Changes Section
            AddRecentChangesSection(body, data);
            _templateHelper.AddDivider(body);

            // Section numbering (dynamic based on content)
            int sectionNumber = 1;

            // 1. Purpose
            AddPurposeSection(body, data, sectionNumber++);
            _templateHelper.AddDivider(body);

            // 2. What's New (conditional)
            if (!string.IsNullOrEmpty(data.WhatsNew))
            {
                AddWhatsNewSection(body, data, sectionNumber++);
                _templateHelper.AddDivider(body);
            }

            // Parameters
            AddParametersSection(body, data, sectionNumber++);
            _templateHelper.AddDivider(body);

            // Logic Flow
            AddLogicFlowSection(body, data, sectionNumber++);
            _templateHelper.AddDivider(body);

            // Dependencies (only if complexity > 30)
            if (data.ComplexityScore > 30 && data.Dependencies != null)
            {
                AddDependenciesSection(body, data, sectionNumber++);
                _templateHelper.AddDivider(body);
            }

            // Usage Examples
            AddUsageExamplesSection(body, data, sectionNumber++);
            _templateHelper.AddDivider(body);

            // Performance Notes (only if complexity > 50)
            if (data.ComplexityScore > 50 && !string.IsNullOrEmpty(data.PerformanceNotes))
            {
                AddPerformanceNotesSection(body, data, sectionNumber++);
                _templateHelper.AddDivider(body);
            }

            // Error Handling (only if complexity > 40)
            if (data.ComplexityScore > 40 && !string.IsNullOrEmpty(data.ErrorHandling))
            {
                AddErrorHandlingSection(body, data, sectionNumber++);
                _templateHelper.AddDivider(body);
            }

            // Full Version History
            AddVersionHistorySection(body, data, sectionNumber);

            document.Save();
            return stream;
        }

        private void CreateHeader(Body body, StoredProcedureData data)
        {
            // Create header table with procedure info and metadata
            var headerTable = body.AppendChild(new Table());
            
            // Table properties for borderless design
            var tableProps = headerTable.AppendChild(new TableProperties());
            tableProps.AppendChild(new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.None) }
            ));

            var headerRow = headerTable.AppendChild(new TableRow());
            
            // Left cell - Title and procedure info
            var leftCell = headerRow.AppendChild(new TableCell());
            var leftCellProps = leftCell.AppendChild(new TableCellProperties());
            leftCellProps.AppendChild(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "6480" }); // 4.5 inches
            
            // Title
            var docTitle = data.IsQA ? "QA STORED PROCEDURE" : "STORED PROCEDURE";
            var titlePara = leftCell.AppendChild(new Paragraph());
            var titleRun = titlePara.AppendChild(new Run());
            titleRun.AppendChild(new RunProperties(
                new FontSize { Val = "40" }, // 20pt
                new Bold(),
                new Color { Val = "2C5F8D" }
            ));
            titleRun.AppendChild(new Text(docTitle));

            // Subtitle
            var subtitlePara = leftCell.AppendChild(new Paragraph());
            subtitlePara.AppendChild(new ParagraphProperties(new SpacingBetweenLines { Before = "40" }));
            var subtitleRun = subtitlePara.AppendChild(new Run());
            subtitleRun.AppendChild(new RunProperties(
                new FontSize { Val = "22" }, // 11pt
                new Color { Val = "495057" }
            ));
            subtitleRun.AppendChild(new Text("Technical Documentation"));

            // Procedure name
            var namePara = leftCell.AppendChild(new Paragraph());
            namePara.AppendChild(new ParagraphProperties(new SpacingBetweenLines { Before = "120" }));
            var nameRun = namePara.AppendChild(new Run());
            nameRun.AppendChild(new RunProperties(
                new FontSize { Val = "20" }, // 10pt
                new Bold(),
                new Color { Val = "495057" }
            ));
            nameRun.AppendChild(new Text($"{data.Schema}.{data.SpName}"));

            // Right cell - Metadata
            var rightCell = headerRow.AppendChild(new TableCell());
            var rightCellProps = rightCell.AppendChild(new TableCellProperties());
            rightCellProps.AppendChild(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "3600" }); // 2.5 inches
            rightCellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "F8F9FA" });
            rightCellProps.AppendChild(new TableCellBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8, Color = "DEE2E6" },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8, Color = "DEE2E6" },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8, Color = "DEE2E6" },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 8, Color = "DEE2E6" }
            ));

            // Metadata entries
            var qaLabel = data.IsQA ? "QA Procedure" : "Production";
            var qaColor = data.IsQA ? "28A745" : "495057";
            
            var metadataEntries = new[]
            {
                ("Version:", $"v{data.Version}", "495057"),
                ("Type:", qaLabel, qaColor),
                ("Created:", data.CreatedDate, "495057"),
                ("Created By:", data.CreatedBy, "495057"),
                ("Complexity:", $"{data.ComplexityScore}/100", "495057")
            };

            foreach (var (label, value, color) in metadataEntries)
            {
                var metaPara = rightCell.AppendChild(new Paragraph());
                metaPara.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "40" }));
                
                var labelRun = metaPara.AppendChild(new Run());
                labelRun.AppendChild(new RunProperties(
                    new FontSize { Val = "18" }, // 9pt
                    new Bold(),
                    new Color { Val = "495057" }
                ));
                labelRun.AppendChild(new Text($"{label} "));

                var valueRun = metaPara.AppendChild(new Run());
                valueRun.AppendChild(new RunProperties(
                    new FontSize { Val = "18" }, // 9pt
                    new Bold(),
                    new Color { Val = color }
                ));
                valueRun.AppendChild(new Text(value));
            }
        }

        private void AddRecentChangesSection(Body body, StoredProcedureData data)
        {
            _templateHelper.AddHeading(body, "5 MOST RECENT CHANGES", 1);

            if (data.RecentChanges?.Any() == true)
            {
                foreach (var change in data.RecentChanges.Take(5))
                {
                    var para = body.AppendChild(new Paragraph());
                    para.AppendChild(new ParagraphProperties(
                        new SpacingBetweenLines { After = "80" },
                        new Indentation { Left = "360" }, // 0.25 inches
                        new NumberingProperties(
                            new NumberingLevelReference { Val = 0 },
                            new NumberingId { Val = 1 }
                        )
                    ));
                    
                    var run = para.AppendChild(new Run());
                    run.AppendChild(new RunProperties(
                        new FontSize { Val = "20" }, // 10pt
                        new Color { Val = "495057" }
                    ));
                    run.AppendChild(new Text($"{change.Date} - {change.Summary} ({change.RefDoc})"));
                }
            }
            else
            {
                _templateHelper.AddContent(body, "No changes recorded yet.", isItalic: true);
            }
        }

        private void AddPurposeSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. PURPOSE", 1);

            var purposeText = data.IsQA 
                ? $"This is a QA validation procedure designed to verify data quality and integrity. {data.Purpose}"
                : data.Purpose;

            _templateHelper.AddContent(body, purposeText);
        }

        private void AddWhatsNewSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. WHAT'S NEW IN VERSION {data.Version}", 1);
            _templateHelper.AddSubheading(body, "Changes in This Version:");
            _templateHelper.AddContent(body, data.WhatsNew);
        }

        private void AddParametersSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. PARAMETERS", 1);

            if (data.Parameters?.Any() == true)
            {
                foreach (var param in data.Parameters)
                {
                    _templateHelper.AddSubheading(body, $"{param.Name} ({param.Type}):");
                    _templateHelper.AddContent(body, param.Description, indent: 0.25);
                    
                    if (!string.IsNullOrEmpty(param.DefaultValue))
                    {
                        _templateHelper.AddContent(body, $"Default: {param.DefaultValue}", 
                            indent: 0.25, fontSize: 9, isItalic: true);
                    }
                }
            }
            else
            {
                _templateHelper.AddContent(body, "No parameters", fontSize: 10);
            }
        }

        private void AddLogicFlowSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. LOGIC FLOW", 1);

            if (data.LogicFlow?.Any() == true)
            {
                for (int i = 0; i < data.LogicFlow.Count; i++)
                {
                    var step = data.LogicFlow[i];
                    _templateHelper.AddSubheading(body, $"Step {i + 1}: {step.Title}");
                    _templateHelper.AddContent(body, step.Description, indent: 0.25, fontSize: 10);
                }
            }
            else if (!string.IsNullOrEmpty(data.LogicFlowText))
            {
                _templateHelper.AddContent(body, data.LogicFlowText);
            }
        }

        private void AddDependenciesSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. DEPENDENCIES", 1);

            if (data.Dependencies.Tables?.Any() == true)
            {
                _templateHelper.AddSubheading(body, "Tables Accessed:");
                foreach (var table in data.Dependencies.Tables)
                {
                    _templateHelper.AddBullet(body, table);
                }
            }

            if (data.Dependencies.Procedures?.Any() == true)
            {
                _templateHelper.AddSubheading(body, "Stored Procedures Called:");
                foreach (var proc in data.Dependencies.Procedures)
                {
                    _templateHelper.AddBullet(body, proc);
                }
            }
        }

        private void AddUsageExamplesSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. USAGE EXAMPLES", 1);

            if (data.UsageExamples?.Any() == true)
            {
                for (int i = 0; i < data.UsageExamples.Count; i++)
                {
                    var example = data.UsageExamples[i];
                    _templateHelper.AddSubheading(body, $"Example {i + 1}: {example.Title}");
                    _templateHelper.AddCodeBlock(body, example.Code);
                    
                    if (!string.IsNullOrEmpty(example.Explanation))
                    {
                        _templateHelper.AddContent(body, example.Explanation, indent: 0.25, fontSize: 10);
                    }
                }
            }
        }

        private void AddPerformanceNotesSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. PERFORMANCE NOTES", 1);
            _templateHelper.AddContent(body, data.PerformanceNotes);
        }

        private void AddErrorHandlingSection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. ERROR HANDLING", 1);
            _templateHelper.AddContent(body, data.ErrorHandling);
        }

        private void AddVersionHistorySection(Body body, StoredProcedureData data, int sectionNumber)
        {
            _templateHelper.AddHeading(body, $"{sectionNumber}. FULL VERSION HISTORY", 1);

            if (data.FullVersionHistory?.Any() == true)
            {
                // Create version history table
                var table = body.AppendChild(new Table());
                
                // Table properties
                var tableProps = table.AppendChild(new TableProperties());
                tableProps.AppendChild(new TableStyle { Val = "LightGridAccent1" });
                tableProps.AppendChild(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" });

                // Header row
                var headerRow = table.AppendChild(new TableRow());
                var headers = new[] { "Version", "Date", "Changed By", "Changes" };
                
                foreach (var headerText in headers)
                {
                    var headerCell = headerRow.AppendChild(new TableCell());
                    var headerPara = headerCell.AppendChild(new Paragraph());
                    var headerRun = headerPara.AppendChild(new Run());
                    headerRun.AppendChild(new RunProperties(
                        new Bold(),
                        new FontSize { Val = "20" } // 10pt
                    ));
                    headerRun.AppendChild(new Text(headerText));
                }

                // Data rows
                foreach (var entry in data.FullVersionHistory)
                {
                    var dataRow = table.AppendChild(new TableRow());
                    
                    var versionCell = dataRow.AppendChild(new TableCell());
                    var versionPara = versionCell.AppendChild(new Paragraph());
                    var versionRun = versionPara.AppendChild(new Run());
                    versionRun.AppendChild(new RunProperties(new FontSize { Val = "18" })); // 9pt
                    versionRun.AppendChild(new Text($"v{entry.Version}"));

                    var dateCell = dataRow.AppendChild(new TableCell());
                    var datePara = dateCell.AppendChild(new Paragraph());
                    var dateRun = datePara.AppendChild(new Run());
                    dateRun.AppendChild(new RunProperties(new FontSize { Val = "18" })); // 9pt
                    dateRun.AppendChild(new Text(entry.Date));

                    var changedByCell = dataRow.AppendChild(new TableCell());
                    var changedByPara = changedByCell.AppendChild(new Paragraph());
                    var changedByRun = changedByPara.AppendChild(new Run());
                    changedByRun.AppendChild(new RunProperties(new FontSize { Val = "18" })); // 9pt
                    changedByRun.AppendChild(new Text(entry.ChangedBy));

                    var changesCell = dataRow.AppendChild(new TableCell());
                    var changesPara = changesCell.AppendChild(new Paragraph());
                    var changesRun = changesPara.AppendChild(new Run());
                    changesRun.AppendChild(new RunProperties(new FontSize { Val = "18" })); // 9pt
                    var changesText = !string.IsNullOrEmpty(entry.RefDoc) 
                        ? $"{entry.Changes} (Ref: {entry.RefDoc})" 
                        : entry.Changes;
                    changesRun.AppendChild(new Text(changesText));
                }
            }
        }

        /// <summary>
        /// Creates sample data for testing the stored procedure template
        /// </summary>
        public static StoredProcedureData CreateSampleData()
        {
            return new StoredProcedureData
            {
                SpName = "usp_Customer_Update",
                Version = "1.2",
                CreatedDate = "2024-10-01",
                CreatedBy = "A.Kirby",
                Schema = "dbo",
                ObjectType = "Stored Procedure",
                Purpose = "Updates customer information including contact details and preferences. Validates input data and maintains audit trail.",
                RecentChanges = new List<ChangeEntry>
                {
                    new() { Date = "2024-12-03", Summary = "Added error handling for NULL inputs", RefDoc = "DF-0089" },
                    new() { Date = "2024-11-15", Summary = "Optimized JOIN performance", RefDoc = "EN-0067" },
                    new() { Date = "2024-10-20", Summary = "Added email validation", RefDoc = "BR-0045" },
                    new() { Date = "2024-10-10", Summary = "Fixed timezone handling", RefDoc = "DF-0032" },
                    new() { Date = "2024-10-01", Summary = "Initial documentation", RefDoc = "" }
                },
                WhatsNew = "Added comprehensive error handling for NULL and invalid input parameters. Procedure now validates email format before update and returns detailed error codes.",
                Parameters = new List<ParameterInfo>
                {
                    new() { Name = "@CustomerID", Type = "INT", Description = "Unique customer identifier" },
                    new() { Name = "@Email", Type = "VARCHAR(255)", Description = "Customer email address" },
                    new() { Name = "@Phone", Type = "VARCHAR(20)", Description = "Customer phone number", DefaultValue = "NULL" }
                },
                LogicFlow = new List<LogicStep>
                {
                    new() { Title = "Input Validation", Description = "Validates all input parameters. Checks CustomerID exists, email format is valid, phone number format is correct." },
                    new() { Title = "Begin Transaction", Description = "Starts transaction to ensure data consistency across multiple table updates." },
                    new() { Title = "Update Customer Table", Description = "Updates primary customer record with new contact information." },
                    new() { Title = "Update Audit Log", Description = "Records change in audit table with timestamp and user information." },
                    new() { Title = "Commit Transaction", Description = "Commits all changes if successful, rolls back on any error." }
                },
                Dependencies = new DependencyInfo
                {
                    Tables = new List<string> { "dbo.Customers", "dbo.CustomerAudit", "dbo.EmailValidation" },
                    Procedures = new List<string> { "dbo.usp_ValidateEmail", "dbo.usp_LogAuditEntry" }
                },
                UsageExamples = new List<UsageExample>
                {
                    new()
                    {
                        Title = "Update customer email",
                        Code = "EXEC dbo.usp_Customer_Update \n    @CustomerID = 12345,\n    @Email = 'john.doe@example.com',\n    @Phone = NULL",
                        Explanation = "Updates email address for customer ID 12345 without changing phone number."
                    }
                },
                PerformanceNotes = "Procedure uses covering index on CustomerID for optimal lookup performance. Average execution time: 15ms. Recommended to batch updates if processing >1000 records.",
                ErrorHandling = "Returns error code -1 for invalid CustomerID, -2 for invalid email format, -3 for database constraint violations. All errors are logged to ErrorLog table.",
                FullVersionHistory = new List<VersionHistoryEntry>
                {
                    new() { Version = "1.2", Date = "2024-12-03", ChangedBy = "A.Kirby", Changes = "Added error handling for NULL inputs", RefDoc = "DF-0089" },
                    new() { Version = "1.1", Date = "2024-11-15", ChangedBy = "J.Smith", Changes = "Optimized JOIN performance", RefDoc = "EN-0067" },
                    new() { Version = "1.0", Date = "2024-10-01", ChangedBy = "System", Changes = "Initial documentation", RefDoc = "" }
                },
                ComplexityScore = 45,
                IsQA = false
            };
        }
    }

    /// <summary>
    /// Data model for stored procedure documentation
    /// </summary>
    public class StoredProcedureData
    {
        public string SpName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<ChangeEntry>? RecentChanges { get; set; }
        public string? WhatsNew { get; set; }
        public List<ParameterInfo>? Parameters { get; set; }
        public List<LogicStep>? LogicFlow { get; set; }
        public string? LogicFlowText { get; set; }
        public DependencyInfo? Dependencies { get; set; }
        public List<UsageExample>? UsageExamples { get; set; }
        public string? PerformanceNotes { get; set; }
        public string? ErrorHandling { get; set; }
        public List<VersionHistoryEntry>? FullVersionHistory { get; set; }
        public int ComplexityScore { get; set; }
        public bool IsQA { get; set; }
    }

    public class ChangeEntry
    {
        public string Date { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string RefDoc { get; set; } = string.Empty;
    }

    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? DefaultValue { get; set; }
    }

    public class LogicStep
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class DependencyInfo
    {
        public List<string>? Tables { get; set; }
        public List<string>? Procedures { get; set; }
    }

    public class UsageExample
    {
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Explanation { get; set; }
    }

    public class VersionHistoryEntry
    {
        public string Version { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public string Changes { get; set; } = string.Empty;
        public string? RefDoc { get; set; }
    }
}