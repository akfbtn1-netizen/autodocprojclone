const docx = require("docx");
const fs = require("node:fs");
const { Document, Paragraph, TextRun, AlignmentType } = docx;

/**
 * Business Request Documentation Template
 *
 * Usage: node TEMPLATE_BusinessRequest.js <input.json> <output.docx>
 * This template is for documenting new business requests and feature implementations
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_BusinessRequest.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_BusinessRequest.js data.json output.docx");
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
let requestData;
try {
  const jsonContent = fs.readFileSync(inputJsonPath, 'utf8');
  requestData = JSON.parse(jsonContent);
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
      new Paragraph({
        children: [new TextRun({ text: "Business Request Documentation", bold: true, size: 32 })],
        alignment: AlignmentType.CENTER,
        spacing: { after: 400 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { after: 200 } }),

      new Paragraph({
        children: [
          new TextRun({ text: "Jira Ticket: ", bold: true }),
          new TextRun(ensureString(requestData.ticket, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "CAB Number: ", bold: true }),
          new TextRun(ensureString(requestData.cabNumber, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Date Entered: ", bold: true }),
          new TextRun(ensureString(requestData.dateEntered || requestData.created, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Author: ", bold: true }),
          new TextRun(ensureString(requestData.author, "N/A"))
        ],
        spacing: { after: 200 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { after: 400 } }),

      new Paragraph({
        children: [new TextRun({ text: "Table Information", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "New Table Created: ", bold: true }),
          new TextRun(ensureString(requestData.newTableCreated || requestData.procedureName, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Source Tables: ", bold: true }),
          new TextRun(ensureString(requestData.sourceTables, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Associated Stored Procedure: ", bold: true }),
          new TextRun(ensureString(requestData.storedProcedure || requestData.procedureName, "N/A"))
        ],
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Business Purpose", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(requestData.businessPurpose || requestData.purpose, "No description provided."),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Business Request Description", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(requestData.requestDescription, "No description provided."),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Reason for Change", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...(requestData.reasonForChange ? [
        new Paragraph({
          children: [
            new TextRun({ text: "Business Driver: ", bold: true }),
            new TextRun(ensureString(requestData.reasonForChange.businessDriver, "N/A"))
          ],
          spacing: { after: 200 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Gap Identified: ", bold: true }),
            new TextRun(ensureString(requestData.reasonForChange.gapIdentified, "N/A"))
          ],
          spacing: { after: 200 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Solution: ", bold: true }),
            new TextRun(ensureString(requestData.reasonForChange.solution, "N/A"))
          ],
          spacing: { after: 400 }
        })
      ] : []),

      new Paragraph({
        children: [new TextRun({ text: "Key Data Elements Captured", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(requestData.keyDataElements).flatMap(cat => [
        new Paragraph({
          children: [new TextRun({ text: ensureString(cat.category || cat) + ": ", bold: true })],
          spacing: { after: 100 }
        }),
        ...ensureArray(cat.elements || []).map(elem =>
          new Paragraph({ text: ensureString(elem), bullet: { level: 0 }, spacing: { after: 100 } })
        )
      ]),

      new Paragraph({
        children: [new TextRun({ text: "Implementation Details", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...(requestData.implementationDetails ? [
        new Paragraph({
          children: [
            new TextRun({ text: "Load Strategy: ", bold: true }),
            new TextRun(ensureString(requestData.implementationDetails.loadStrategy, "N/A"))
          ],
          spacing: { after: 200 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Primary Source: ", bold: true }),
            new TextRun(ensureString(requestData.implementationDetails.primarySource, "N/A"))
          ],
          spacing: { after: 200 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Supplemental Source: ", bold: true }),
            new TextRun(ensureString(requestData.implementationDetails.supplementalSource, "N/A"))
          ],
          spacing: { after: 200 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Deduplication: ", bold: true }),
            new TextRun(ensureString(requestData.implementationDetails.deduplication, "N/A"))
          ],
          spacing: { after: 400 }
        })
      ] : []),

      new Paragraph({
        children: [new TextRun({ text: "Additional Notes", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(requestData.additionalNotes, "No additional notes."),
        spacing: { after: 400 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { before: 600, after: 200 } }),
      new Paragraph({ text: "End of Documentation", alignment: AlignmentType.CENTER }),
      new Paragraph({
        text: `Documentation Type: Business Request | Generated: ${new Date().toLocaleDateString()}`,
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
  console.log(`📄 Template: Business Request`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
