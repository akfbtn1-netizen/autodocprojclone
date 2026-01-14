const docx = require("docx");
const fs = require("fs");
const { Document, Paragraph, TextRun, AlignmentType } = docx;

/**
 * Enhancement Documentation Template
 *
 * Usage: node TEMPLATE_Enhancement.js <input.json> <output.docx>
 * This template is for documenting enhancements and improvements to existing functionality
 */

// ===== COMMAND-LINE ARGUMENT PARSING =====
const args = process.argv.slice(2);

if (args.length < 2) {
  console.error("ERROR: Missing required arguments");
  console.error("Usage: node TEMPLATE_Enhancement.js <input.json> <output.docx>");
  console.error("Example: node TEMPLATE_Enhancement.js data.json output.docx");
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
let enhancementData;
try {
  const jsonContent = fs.readFileSync(inputJsonPath, 'utf8');
  enhancementData = JSON.parse(jsonContent);
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
        children: [new TextRun({ text: "Enhancement Documentation", bold: true, size: 32 })],
        alignment: AlignmentType.CENTER,
        spacing: { after: 400 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { after: 200 } }),

      new Paragraph({
        children: [
          new TextRun({ text: "Jira Ticket: ", bold: true }),
          new TextRun(ensureString(enhancementData.ticket, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "CAB Number: ", bold: true }),
          new TextRun(ensureString(enhancementData.cabNumber, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Date Entered: ", bold: true }),
          new TextRun(ensureString(enhancementData.dateEntered || enhancementData.created, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Author: ", bold: true }),
          new TextRun(ensureString(enhancementData.author, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Status: ", bold: true }),
          new TextRun(ensureString(enhancementData.status, "Completed"))
        ],
        spacing: { after: 200 }
      }),

      new Paragraph({ text: "_".repeat(90), spacing: { after: 400 } }),

      new Paragraph({
        children: [new TextRun({ text: "Object Information", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Schema: ", bold: true }),
          new TextRun(ensureString(enhancementData.schema, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Table/Object: ", bold: true }),
          new TextRun(ensureString(enhancementData.tableName || enhancementData.objectName, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Column: ", bold: true }),
          new TextRun(ensureString(enhancementData.columnName, "N/A"))
        ],
        spacing: { after: 100 }
      }),

      new Paragraph({
        children: [
          new TextRun({ text: "Associated Stored Procedures: ", bold: true }),
          new TextRun(ensureString(enhancementData.storedProcedure || enhancementData.procedureName, "N/A"))
        ],
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Enhancement Description", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        text: ensureString(enhancementData.enhancementDescription || enhancementData.requestDescription, "No description provided."),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Reason for Enhancement", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Current State:", bold: true })],
        spacing: { after: 100 }
      }),

      new Paragraph({
        text: ensureString(enhancementData.currentState, "N/A"),
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Improvement Needed:", bold: true })],
        spacing: { after: 100 }
      }),

      new Paragraph({
        text: ensureString(enhancementData.improvementNeeded, "N/A"),
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Business Value:", bold: true })],
        spacing: { after: 100 }
      }),

      new Paragraph({
        text: ensureString(enhancementData.businessValue, "N/A"),
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Implementation Details", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Changes Made:", bold: true })],
        spacing: { after: 100 }
      }),

      ...ensureArray(enhancementData.changesMade).map(change =>
        new Paragraph({ text: ensureString(change), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({
        children: [new TextRun({ text: "Modified Stored Procedures:", bold: true })],
        spacing: { after: 100, before: 200 }
      }),

      ...ensureArray(enhancementData.proceduresModified).map(proc =>
        new Paragraph({
          children: [
            new TextRun({ text: ensureString(proc.name || proc) + ": ", bold: true }),
            new TextRun(ensureString(proc.function || ""))
          ],
          spacing: { after: 100 }
        })
      }),

      new Paragraph({
        children: [new TextRun({ text: "SQL Implementation", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      new Paragraph({
        text: ensureString(enhancementData.sqlExample || enhancementData.implementationApproach, "No SQL example provided."),
        font: { name: "Courier New" },
        spacing: { after: 400 }
      }),

      new Paragraph({
        children: [new TextRun({ text: "Testing and Validation", bold: true, size: 24 })],
        spacing: { after: 200 }
      }),

      ...ensureArray(enhancementData.testingValidation).map(item =>
        new Paragraph({ text: ensureString(item), bullet: { level: 0 }, spacing: { after: 100 } })
      ),

      new Paragraph({
        children: [new TextRun({ text: "Performance Impact", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...(enhancementData.performanceImpact ? [
        new Paragraph({
          children: [
            new TextRun({ text: "Before: ", bold: true }),
            new TextRun(ensureString(enhancementData.performanceImpact.before, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "After: ", bold: true }),
            new TextRun(ensureString(enhancementData.performanceImpact.after, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Improvement: ", bold: true }),
            new TextRun(ensureString(enhancementData.performanceImpact.improvement, "N/A"))
          ],
          spacing: { after: 200 }
        })
      ] : []),

      new Paragraph({
        children: [new TextRun({ text: "Benefits", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...ensureArray(enhancementData.benefits).flatMap(benefit => [
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
        children: [new TextRun({ text: "Deployment Information", bold: true, size: 24 })],
        spacing: { after: 200, before: 200 }
      }),

      ...(enhancementData.deploymentInfo ? [
        new Paragraph({
          children: [
            new TextRun({ text: "Database: ", bold: true }),
            new TextRun(ensureString(enhancementData.deploymentInfo.database, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Schema: ", bold: true }),
            new TextRun(ensureString(enhancementData.deploymentInfo.schema, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Deployment Date: ", bold: true }),
            new TextRun(ensureString(enhancementData.deploymentInfo.date, "N/A"))
          ],
          spacing: { after: 100 }
        }),
        new Paragraph({
          children: [
            new TextRun({ text: "Rollback Plan: ", bold: true }),
            new TextRun(ensureString(enhancementData.deploymentInfo.rollbackPlan, "N/A"))
          ],
          spacing: { after: 200 }
        })
      ] : []),

      new Paragraph({ text: "_".repeat(90), spacing: { before: 600, after: 200 } }),
      new Paragraph({ text: "End of Documentation", alignment: AlignmentType.CENTER }),
      new Paragraph({
        text: `Documentation Type: Enhancement | Generated: ${new Date().toLocaleDateString()}`,
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
  console.log(`📄 Template: Enhancement`);
  console.log(`📊 Size: ${buffer.length} bytes`);
}).catch((error) => {
  console.error(`ERROR: Failed to generate document: ${error.message}`);
  process.exit(1);
});
