const docx = require("docx");
const fs = require("node:fs");

const { Document, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, HeadingLevel, AlignmentType, BorderStyle } = docx;

/**
 * Tier 1 (Comprehensive) Stored Procedure Documentation Template
 *
 * Usage: node TEMPLATE_Tier1_Comprehensive.js <input.json> <output.docx>
 * This template is designed for complex ETL loads, multi-source integrations, and business-critical procedures
 *
 * Time to complete: 2-3 hours
 * Page count: 8-10 pages
 * Use when: Procedure has 6+ complexity points (multi-source, complex logic, critical business impact)
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_Tier1_Comprehensive.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_Tier1_Comprehensive.js data.json output.docx");
  process.exit(1);
}

const inputJsonPath = args[0];
const outputDocxPath = args[1];

// Validate input file exists
if (!fs.existsSync(inputJsonPath)) {
  console.error(`ERROR: Input file not found: ${inputJsonPath}`);
  process.exit(1);
}

// ===== READ AND PARSE JSON DATA =====
let procedureData;
try {
  const jsonContent = fs.readFileSync(inputJsonPath, 'utf8');
  procedureData = JSON.parse(jsonContent);
  console.log(`✓ Loaded data from: ${inputJsonPath}`);
} catch (error) {
  console.error(`ERROR: Failed to read or parse JSON file: ${error.message}`);
  process.exit(1);
}

// ===== HELPER FUNCTIONS =====
function ensureArray(value) {
  if (!value) return [];
  return Array.isArray(value) ? value : [value];
}

function ensureString(value, defaultValue = "") {
  return value !== null && value !== undefined ? String(value) : defaultValue;
}

// ===== DOCUMENT GENERATION =====
const doc = new Document({
  sections: [{
    properties: {},
    children: [
      // Title
      new Paragraph({
        text: "Stored Procedure Documentation",
        heading: HeadingLevel.HEADING_1,
        alignment: AlignmentType.CENTER,
        spacing: { after: 400 }
      }),

      new Paragraph({
        text: ensureString(procedureData.procedureName, "Untitled Procedure"),
        heading: HeadingLevel.HEADING_2,
        alignment: AlignmentType.CENTER,
        spacing: { after: 600 }
      }),

      // Header Summary Box
      new Paragraph({
        text: "━".repeat(80),
        spacing: { after: 100 }
      }),

      new Table({
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          bottom: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          left: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          right: { style: BorderStyle.SINGLE, size: 1, color: "000000" }
        },
        rows: [
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Schema", bold: true })], width: { size: 20, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.schema))], width: { size: 30, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Version", bold: true })], width: { size: 20, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.version, "1.0"))], width: { size: 30, type: WidthType.PERCENTAGE } })
            ]
          }),
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Author", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.author))] }),
              new TableCell({ children: [new Paragraph({ text: "Ticket", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.ticket))] })
            ]
          }),
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Created", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.created))] }),
              new TableCell({ children: [new Paragraph({ text: "Last Modified", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.lastModified, procedureData.created))] })
            ]
          }),
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Frequency", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.frequency, "N/A"))] }),
              new TableCell({ children: [new Paragraph({ text: "Avg Duration", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.avgDuration, "N/A"))] })
            ]
          })
        ]
      }),

      new Paragraph({ text: "━".repeat(80), spacing: { before: 100, after: 600 } }),

      // Section 1: Overview & Purpose
      new Paragraph({ text: "1. OVERVIEW & PURPOSE", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Paragraph({
        children: [
          new TextRun({ text: "Business Function: ", bold: true }),
          new TextRun(ensureString(procedureData.businessFunction || procedureData.purpose, "No description provided"))
        ],
        spacing: { after: 200 }
      }),

      new Paragraph({ children: [new TextRun({ text: "Primary Operations:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.primaryOperations).map((op, idx) =>
        new Paragraph({ text: `${idx + 1}. ${ensureString(op)}`, spacing: { after: 100 } })
      ),

      new Paragraph({ text: ensureString(procedureData.consolidationSummary, ""), spacing: { after: 400 } }),

      // Section 2: Parameters
      new Paragraph({ text: "2. PARAMETERS", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Table({
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          bottom: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          left: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          right: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideHorizontal: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideVertical: { style: BorderStyle.SINGLE, size: 1, color: "000000" }
        },
        rows: [
          new TableRow({
            tableHeader: true,
            children: [
              new TableCell({ children: [new Paragraph({ text: "Parameter", bold: true })], width: { size: 20, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Data Type", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Required", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Description", bold: true })], width: { size: 50, type: WidthType.PERCENTAGE } })
            ]
          }),
          ...ensureArray(procedureData.parameters).map(param =>
            new TableRow({
              children: [
                new TableCell({ children: [new Paragraph(ensureString(param.name))] }),
                new TableCell({ children: [new Paragraph(ensureString(param.type))] }),
                new TableCell({ children: [new Paragraph(ensureString(param.required, "No"))] }),
                new TableCell({ children: [new Paragraph(ensureString(param.description))] })
              ]
            })
          )
        ]
      }),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 3: Return Values & Output
      new Paragraph({ text: "3. RETURN VALUES & OUTPUT", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),
      new Paragraph({ children: [new TextRun({ text: "Return Value: ", bold: true }), new TextRun(ensureString(procedureData.returnValue, "None"))], spacing: { after: 100 } }),
      new Paragraph({ children: [new TextRun({ text: "Result Sets: ", bold: true }), new TextRun(ensureString(procedureData.resultSets, "None"))], spacing: { after: 100 } }),
      new Paragraph({ children: [new TextRun({ text: "Side Effects:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.sideEffects).map(effect => new Paragraph({ text: ensureString(effect), bullet: { level: 0 }, spacing: { after: 100 } })),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 4: Execution Flow
      new Paragraph({ text: "4. EXECUTION FLOW", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),
      ...ensureArray(procedureData.executionSteps || procedureData.executionLogic).map(step => {
        const stepText = typeof step === 'object' ? `${step.step}: ${step.description}` : ensureString(step);
        return new Paragraph({ text: stepText, spacing: { after: 150 } });
      }),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 5: Data Quality Rules
      new Paragraph({ text: "5. DATA QUALITY RULES", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),
      ...ensureArray(procedureData.qualityRules).flatMap(qr => [
        new Paragraph({ children: [new TextRun({ text: ensureString(qr.category), bold: true })], spacing: { after: 100 } }),
        ...ensureArray(qr.rules).map(rule => new Paragraph({ text: ensureString(rule), bullet: { level: 0 }, spacing: { after: 100 } }))
      ]),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 6: Dependencies
      new Paragraph({ text: "6. DEPENDENCIES", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Paragraph({ children: [new TextRun({ text: "Source Tables:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.sourceTables || (procedureData.dependencies && procedureData.dependencies.sourceTables)).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Target Tables:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.targetTables || (procedureData.dependencies && procedureData.dependencies.targetTables)).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Control Tables:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.controlTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "External Procedures:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.externalProcedures || (procedureData.dependencies && procedureData.dependencies.procedures)).map(proc =>
        new Paragraph({ text: ensureString(proc), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 7: Performance Metrics
      new Paragraph({ text: "7. PERFORMANCE METRICS", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Table({
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          bottom: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          left: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          right: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideHorizontal: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideVertical: { style: BorderStyle.SINGLE, size: 1, color: "000000" }
        },
        rows: [
          new TableRow({
            tableHeader: true,
            children: [
              new TableCell({ children: [new Paragraph({ text: "Metric", bold: true })], width: { size: 40, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Value", bold: true })], width: { size: 60, type: WidthType.PERCENTAGE } })
            ]
          }),
          ...ensureArray(procedureData.performanceMetrics).map(metric =>
            new TableRow({
              children: [
                new TableCell({ children: [new Paragraph(ensureString(metric.metric))] }),
                new TableCell({ children: [new Paragraph(ensureString(metric.value))] })
              ]
            })
          )
        ]
      }),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 8: Error Handling
      new Paragraph({ text: "8. ERROR HANDLING", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Paragraph({ children: [new TextRun({ text: "Current Implementation:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.errorHandling && procedureData.errorHandling.currentImplementation).map(item =>
        new Paragraph({ text: ensureString(item), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Recommendations:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.errorHandling && procedureData.errorHandling.recommendations).map(item =>
        new Paragraph({ text: ensureString(item), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 9: Usage Examples
      new Paragraph({ text: "9. USAGE EXAMPLES", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      ...ensureArray(procedureData.usageExamples).flatMap(example => [
        new Paragraph({ children: [new TextRun({ text: ensureString(example.title), bold: true })], spacing: { after: 100 } }),
        new Paragraph({ text: ensureString(example.code), font: { name: "Courier New" }, spacing: { after: 200 } })
      ]),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 10: Change History
      new Paragraph({ text: "10. CHANGE HISTORY", heading: HeadingLevel.HEADING_2, spacing: { before: 400, after: 200 } }),

      new Table({
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          bottom: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          left: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          right: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideHorizontal: { style: BorderStyle.SINGLE, size: 1, color: "000000" },
          insideVertical: { style: BorderStyle.SINGLE, size: 1, color: "000000" }
        },
        rows: [
          new TableRow({
            tableHeader: true,
            children: [
              new TableCell({ children: [new Paragraph({ text: "Date", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Author", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Ticket", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Description", bold: true })], width: { size: 55, type: WidthType.PERCENTAGE } })
            ]
          }),
          ...ensureArray(procedureData.changeHistory).map(change =>
            new TableRow({
              children: [
                new TableCell({ children: [new Paragraph(ensureString(change.date))] }),
                new TableCell({ children: [new Paragraph(ensureString(change.author))] }),
                new TableCell({ children: [new Paragraph(ensureString(change.ticket))] }),
                new TableCell({ children: [new Paragraph(ensureString(change.description))] })
              ]
            })
          )
        ]
      }),

      // Footer
      new Paragraph({ text: "━".repeat(80), spacing: { before: 600, after: 200 } }),
      new Paragraph({ text: "END OF DOCUMENTATION", alignment: AlignmentType.CENTER, spacing: { after: 200 } }),
      new Paragraph({
        text: `Documentation Type: Tier 1 (Comprehensive) | Generated: ${new Date().toLocaleDateString()}`,
        italics: true,
        alignment: AlignmentType.CENTER
      })
    ]
  }]
});

// ===== WRITE OUTPUT FILE =====
docx.Packer.toBuffer(doc).then((buffer) => {
  fs.writeFileSync(outputDocxPath, buffer);
  console.log(`✅ Document created successfully: ${outputDocxPath}`);
  console.log(`📄 Template: Tier 1 (Comprehensive)`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
