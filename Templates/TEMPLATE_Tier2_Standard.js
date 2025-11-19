const docx = require("docx");
const fs = require("node:fs");

const { Document, Paragraph, TextRun, Table, TableRow, TableCell, WidthType, AlignmentType, BorderStyle } = docx;

/**
 * Tier 2 (Standard) Stored Procedure Documentation Template
 *
 * Usage: node TEMPLATE_Tier2_Standard.js <input.json> <output.docx>
 * This template is designed for standard CRUD operations, single-source loads, and typical business logic
 *
 * Time to complete: 30-45 minutes
 * Page count: 4-5 pages
 * Use when: Procedure has 3-5 complexity points (standard operations, moderate complexity)
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_Tier2_Standard.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_Tier2_Standard.js data.json output.docx");
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
        children: [new TextRun({ text: "Stored Procedure Documentation", bold: true, size: 32 })],
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
              new TableCell({ children: [new Paragraph(ensureString(procedureData.type, "SystemDoc"))], width: { size: 25, type: WidthType.PERCENTAGE } })
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
              new TableCell({ children: [new Paragraph({ text: "Status", bold: true })] }),
              new TableCell({ children: [new Paragraph("Active")] })
            ]
          })
        ]
      }),

      new Paragraph({ text: "━".repeat(80), spacing: { before: 200, after: 400 } }),

      // Section 1: Purpose
      new Paragraph({
        children: [new TextRun({ text: "PURPOSE", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),
      new Paragraph({ text: ensureString(procedureData.purpose, "No description provided."), spacing: { after: 400 } }),

      // Section 2: Parameters
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
              new TableCell({ children: [new Paragraph({ text: "Type", bold: true })], width: { size: 20, type: WidthType.PERCENTAGE } }),
              new TableCell({ children: [new Paragraph({ text: "Description", bold: true })], width: { size: 55, type: WidthType.PERCENTAGE } })
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

      // Section 3: Return Value & Output
      new Paragraph({
        children: [new TextRun({ text: "RETURN VALUE & OUTPUT", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),
      new Paragraph({
        children: [
          new TextRun({ text: "Return Value: ", bold: true }),
          new TextRun(ensureString(procedureData.returnValue, "None"))
        ],
        spacing: { after: 100 }
      }),
      new Paragraph({
        children: [
          new TextRun({ text: "Output: ", bold: true }),
          new TextRun(ensureString(procedureData.output, "N/A"))
        ],
        spacing: { after: 400 }
      }),

      // Section 4: Execution Logic
      new Paragraph({
        children: [new TextRun({ text: "EXECUTION LOGIC", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),
      ...ensureArray(procedureData.executionLogic).map(step =>
        new Paragraph({ text: ensureString(step), bullet: { level: 0 }, spacing: { after: 100 } })
      ),
      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 5: Dependencies
      new Paragraph({
        children: [new TextRun({ text: "DEPENDENCIES", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({ children: [new TextRun({ text: "Source Tables:", bold: true })], spacing: { after: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.sourceTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Target Tables:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.targetTables).map(table =>
        new Paragraph({ text: ensureString(table), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({ children: [new TextRun({ text: "Related Procedures:", bold: true })], spacing: { after: 100, before: 100 } }),
      ...ensureArray(procedureData.dependencies && procedureData.dependencies.procedures).map(proc =>
        new Paragraph({ text: ensureString(proc), bullet: { level: 0 }, spacing: { after: 100 } })
      ),
      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 6: Usage Examples
      new Paragraph({
        children: [new TextRun({ text: "USAGE EXAMPLES", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(procedureData.usageExamples).flatMap(example => [
        new Paragraph({ children: [new TextRun({ text: ensureString(example.title), bold: true })], spacing: { after: 100 } }),
        new Paragraph({ text: ensureString(example.code), font: { name: "Courier New" }, spacing: { after: 200 } })
      ]),

      new Paragraph({ text: "", spacing: { after: 400 } }),

      // Section 7: Change History
      new Paragraph({
        children: [new TextRun({ text: "CHANGE HISTORY", bold: true, size: 24 })],
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
        text: `Documentation Type: Tier 2 (Standard) | Generated: ${new Date().toLocaleDateString()}`,
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
  console.log(`📄 Template: Tier 2 (Standard)`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
