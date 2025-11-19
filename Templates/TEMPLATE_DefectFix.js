const docx = require("docx");
const fs = require("node:fs");
const { Document, Paragraph, TextRun, AlignmentType } = docx;

/**
 * Defect Fix Documentation Template
 *
 * Usage: node TEMPLATE_DefectFix.js <input.json> <output.docx>
 * This template is for documenting defect fixes and bug corrections
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_DefectFix.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_DefectFix.js data.json output.docx");
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
let defectData;
try {
  const jsonContent = fs.readFileSync(inputJsonPath, 'utf8');
  defectData = JSON.parse(jsonContent);
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
        children: [new TextRun({ text: "Defect Fix Documentation", bold: true, size: 32 })],
        alignment: AlignmentType.CENTER,
        spacing: { after: 400 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { after: 200 } }),

      new Paragraph({
        children: [
          new TextRun({ text: "Jira Ticket: ", bold: true }),
          new TextRun(ensureString(defectData.ticket, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "CAB Number: ", bold: true }),
          new TextRun(ensureString(defectData.cabNumber, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Date Entered: ", bold: true }),
          new TextRun(ensureString(defectData.dateEntered || defectData.created, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Author: ", bold: true }),
          new TextRun(ensureString(defectData.author, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Status: ", bold: true }),
          new TextRun(ensureString(defectData.status, "Completed"))
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
          new TextRun({ text: "Schema: ", bold: true }),
          new TextRun(ensureString(defectData.schema, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Tables Affected: ", bold: true }),
          new TextRun(ensureString(defectData.tablesAffected, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Associated Stored Procedure: ", bold: true }),
          new TextRun(ensureString(defectData.storedProcedure || defectData.procedureName, "N/A"))
        ],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Table Purpose", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(defectData.tablePurpose || defectData.purpose, "No description provided."),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Defect Description", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(defectData.defectDescription, "No description provided."),
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Issue Discovered During Implementation:", bold: true })],
        spacing: { after: 100 }
      }),

      new Paragraph({
        text: ensureString(defectData.issueDiscovered, "N/A"),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Impact & Root Cause", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Impact:", bold: true })],
        spacing: { after: 100 }
      }),

      ...ensureArray(defectData.impact).map(item =>
        new Paragraph({ text: ensureString(item), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({
        children: [new TextRun({ text: "Root Cause:", bold: true })],
        spacing: { after: 100, before: 200 }
      }),

      new Paragraph({
        text: ensureString(defectData.rootCause, "N/A"),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Procedures Modified", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(defectData.proceduresModified).map(proc =>
        new Paragraph({
          children: [
            new TextRun({ text: ensureString(proc.name || proc) + ": ", bold: true }),
            new TextRun(ensureString(proc.function || ""))
          ],
          spacing: { after: 100 }
        })
      ),

      new Paragraph({
        children: [new TextRun({ text: "SQL Fix Implementation", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      new Paragraph({
        text: ensureString(defectData.sqlExample || defectData.implementationApproach, "No SQL example provided."),
        font: { name: "Courier New" },
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Testing and Validation", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(defectData.testingValidation).map(item =>
        new Paragraph({ text: ensureString(item), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({
        children: [new TextRun({ text: "Benefits", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...ensureArray(defectData.benefits).flatMap(benefit => [
        new Paragraph({
          children: [new TextRun({ text: ensureString(benefit.title || benefit) + ": ", bold: true })],
          spacing: { after: 100 }
        }),
        new Paragraph({
          text: ensureString(benefit.description || ""),
          spacing: { after: 200 }
        })
      ]),

      new Paragraph({
        children: [new TextRun({ text: "Follow-up Actions", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...ensureArray(defectData.followUpActions).map(action =>
        new Paragraph({ text: ensureString(action), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({
        children: [new TextRun({ text: "Deployment Information", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...(defectData.deploymentInfo ? [
        new Paragraph({
          children: [
            new TextRun({ text: "Database: ", bold: true }),
            new TextRun(ensureString(defectData.deploymentInfo.database, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Schema: ", bold: true }),
            new TextRun(ensureString(defectData.deploymentInfo.schema, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Method: ", bold: true }),
            new TextRun(ensureString(defectData.deploymentInfo.method, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Rollback Plan: ", bold: true }),
            new TextRun(ensureString(defectData.deploymentInfo.rollbackPlan, "N/A"))
          ],
          spacing: { after: 200 }
        })
      ] : []),

      new Paragraph({ text: "_".repeat(90), spacing: { before: 600, after: 200 } }),
      new Paragraph({ text: "End of Documentation", alignment: AlignmentType.CENTER }),
      new Paragraph({
        text: `Documentation Type: Defect Fix | Generated: ${new Date().toLocaleDateString()}`,
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
  console.log(`📄 Template: Defect Fix`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
