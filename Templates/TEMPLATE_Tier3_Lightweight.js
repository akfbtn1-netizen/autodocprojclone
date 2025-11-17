const docx = require("docx");
const fs = require("fs");

const { Document, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, AlignmentType, BorderStyle } = docx;

/**
 * Tier 3 (Lightweight) Stored Procedure Documentation Template
 *
 * Usage: node TEMPLATE_Tier3_Lightweight.js <input.json> <output.docx>
 * This template is designed for QA/validation procedures, utilities, and simple operations
 *
 * Time to complete: 10-15 minutes
 * Page count: 2 pages
 * Use when: Procedure has 0-2 complexity points (simple validation, utilities, one-off fixes)
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_Tier3_Lightweight.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_Tier3_Lightweight.js data.json output.docx");
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
        children: [new TextRun({ text: "QA Procedure Documentation", bold: true, size: 32 })],
        alignment: AlignmentType.CENTER,
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: ensureString(procedureData.procedureName, "Untitled Procedure"), size: 28 })],
        alignment: AlignmentType.CENTER,
        spacing: { after: 400 }
      }),

      // Separator
      new Paragraph({ text: "━".repeat(80), spacing: { after: 200 } }),

      // Quick Reference Box
      new Table({
        width: { size: 100, type: WidthType.PERCENTAGE },
        borders: {
          top: { style: BorderStyle.SINGLE, size: 2, color: "000000" },
          bottom: { style: BorderStyle.SINGLE, size: 2, color: "000000" },
          left: { style: BorderStyle.SINGLE, size: 2, color: "000000" },
          right: { style: BorderStyle.SINGLE, size: 2, color: "000000" }
        },
        rows: [
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Schema", bold: true })], width: { size: 25, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.schema))], width: { size: 25, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Type", bold: true })], width: { size: 25, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.type, "Data Quality Check"))], width: { size: 25, type: WidthType.PERCENTAGE } })
            ]
          }),
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Author", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.author))] }),
              new TableCell({ children: [new Paragraph({ text: "Created", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.created))] })
            ]
          }),
          new TableRow({
            children: [
              new TableCell({ children: [new Paragraph({ text: "Validates", bold: true })] }),
              new TableCell({ children: [new Paragraph(ensureString(procedureData.validates, "N/A"))], columnSpan: 3 })
            ]
          })
        ]
      }),

      new Paragraph({ text: "━".repeat(80), spacing: { before: 200, after: 400 } }),

      // Purpose
      new Paragraph({
        children: [new TextRun({ text: "PURPOSE", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),
      new Paragraph({ text: ensureString(procedureData.purpose, "No description provided."), spacing: { after: 400 } }),

      // Parameters
      new Paragraph({
        children: [new TextRun({ text: "PARAMETERS", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

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
              new TableCell({ children: [new Paragraph({ text: "Parameter", bold: true })], width: { size: 25, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Type", bold: true })], width: { size: 15, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Description", bold: true })], width: { size: 60, type: WidthType.PERCENTAGE } })
            ]
          }),
          ...ensureArray(procedureData.parameters).map(param =>
            new TableRow({
              children: [
                new TableCell({ children: [new Paragraph(ensureString(param.name))] }),
                new TableCell({ children: [new Paragraph(ensureString(param.type))] }),
                new TableCell({ children: [new Paragraph(ensureString(param.description))] })
              ]
            })
          )
        ]
      }),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Validation Rules
      new Paragraph({
        children: [new TextRun({ text: "VALIDATION RULES", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(procedureData.validationRules).flatMap(rule => [
        new Paragraph({ children: [new TextRun({ text: ensureString(rule.title), bold: true })], spacing: { after: 100 } }),
        ...ensureArray(rule.checks).map(check =>
          new Paragraph({ text: ensureString(check), bullet: { level: 0 }, spacing: { after: 100 } })
        ),
        new Paragraph({ text: "", spacing: { after: 100 } })
      ]),

      new Paragraph({ text: "", spacing: { after: 200 } }),

      // Exception Handling
      new Paragraph({
        children: [new TextRun({ text: "EXCEPTION HANDLING", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Exceptions logged to: ", bold: true }),
          new TextRun(ensureString(procedureData.exceptionHandling && procedureData.exceptionHandling.exceptionsLoggedTo, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Execution logged to: ", bold: true }),
          new TextRun(ensureString(procedureData.exceptionHandling && procedureData.exceptionHandling.executionLoggedTo, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Processing: ", bold: true }),
          new TextRun(ensureString(procedureData.exceptionHandling && procedureData.exceptionHandling.processing, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Cleanup: ", bold: true }),
          new TextRun(ensureString(procedureData.exceptionHandling && procedureData.exceptionHandling.cleanup, "N/A"))
        ],
        spacing: { after: 400 }
      }),

      // Usage Examples
      new Paragraph({
        children: [new TextRun({ text: "USAGE EXAMPLES", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(procedureData.usageExamples).flatMap(example => [
        new Paragraph({ children: [new TextRun({ text: ensureString(example.title), bold: true })], spacing: { after: 100 } }),
        new Paragraph({ text: ensureString(example.code), font: { name: "Courier New" }, spacing: { after: 200 } })
      ]),

      new Paragraph({ text: "", spacing: { after: 200 } }),

      // Dependencies
      new Paragraph({
        children: [new TextRun({ text: "DEPENDENCIES", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({ children: [new TextRun({ text: "Source Tables:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.sourceTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Control Tables:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.controlTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Output Tables:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.outputTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ text: "", spacing: { after: 200 } }),

      // Execution Logic
      new Paragraph({
        children: [new TextRun({ text: "EXECUTION LOGIC (HIGH-LEVEL)", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({ text: "For each period in the range:", spacing: { after: 100 } }),
      ...ensureArray(procedureData.executionLogic).map(step =>
        new Paragraph({ text: ensureString(step), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      // Footer
      new Paragraph({ text: "━".repeat(80), spacing: { before: 400, after: 200 } }),
      new Paragraph({ text: "END OF DOCUMENTATION", alignment: AlignmentType.CENTER, spacing: { after: 200 } }),
      new Paragraph({
        children: [new TextRun({
          text: `Documentation Type: Tier 3 (Lightweight) | Generated: ${new Date().toLocaleDateString()}`,
          italics: true,
          size: 18
        })],
        alignment: AlignmentType.CENTER
      })
    ]
  }]
});

// ===== WRITE OUTPUT FILE =====
docx.Packer.toBuffer(doc).then((buffer) => {
  fs.writeFileSync(outputDocxPath, buffer);
  console.log(`✅ Document created successfully: ${outputDocxPath}`);
  console.log(`📄 Template: Tier 3 (Lightweight)`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
