"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.BusinessRequestGenerator = void 0;
const docx_1 = require("docx");
const fs = __importStar(require("fs"));
class BusinessRequestGenerator {
    constructor() {
        this.doc = new docx_1.Document({
            sections: [{
                    properties: {},
                    children: []
                }]
        });
    }
    // Helper method to create styled paragraphs
    createStyledParagraph(text, options) {
        const runs = [];
        if (options?.bold && text.includes('**')) {
            // Parse markdown-style bold
            const parts = text.split('**');
            parts.forEach((part, index) => {
                if (index % 2 === 0) {
                    // Regular text
                    if (part) {
                        runs.push(new docx_1.TextRun({
                            text: part,
                            size: options.size || 22,
                            color: options.color || '000000'
                        }));
                    }
                }
                else {
                    // Bold text
                    runs.push(new docx_1.TextRun({
                        text: part,
                        bold: true,
                        size: options.size || 22,
                        color: options.color || '000000'
                    }));
                }
            });
        }
        else {
            runs.push(new docx_1.TextRun({
                text: text,
                bold: options?.bold || false,
                size: options?.size || 22,
                color: options?.color || '000000'
            }));
        }
        return new docx_1.Paragraph({
            children: runs,
            alignment: options?.alignment || docx_1.AlignmentType.LEFT,
            spacing: {
                before: options?.spacing?.before || 0,
                after: options?.spacing?.after || 0
            },
            indent: options?.indent ? { left: options.indent.left } : undefined
        });
    }
    // Create section divider
    createDivider() {
        return new docx_1.Paragraph({
            children: [new docx_1.TextRun({ text: '', size: 1 })],
            border: {
                bottom: {
                    color: '2C5F8D',
                    space: 1,
                    style: docx_1.BorderStyle.SINGLE,
                    size: 6
                }
            },
            spacing: { before: 160, after: 160 }
        });
    }
    // Create section header
    createSectionHeader(number, title) {
        return this.createStyledParagraph(`${number}. ${title}`, {
            bold: true,
            size: 26,
            color: '2C5F8D',
            spacing: { before: 240, after: 160 }
        });
    }
    // Create sub-header
    createSubHeader(text, indent) {
        return this.createStyledParagraph(text, {
            bold: true,
            size: 24,
            spacing: { before: 120, after: 80 },
            indent: indent ? { left: indent } : undefined
        });
    }
    // Create code block
    createCodeBlock(code) {
        const paragraphs = [];
        const lines = code.split('\n');
        lines.forEach(line => {
            paragraphs.push(new docx_1.Paragraph({
                children: [new docx_1.TextRun({
                        text: line,
                        font: 'Consolas',
                        size: 20,
                        color: '333333'
                    })],
                shading: { fill: 'F8F9FA' },
                spacing: { before: 20, after: 20 },
                indent: { left: 360 }
            }));
        });
        return paragraphs;
    }
    // Advanced stored procedure analysis with JIRA detection
    analyzeStoredProcedures(code) {
        const procedures = [];
        // Extract stored procedure names from code
        const spRegex = /(?:CREATE|ALTER)\\s+PROCEDURE\\s+([\\w\\.]+)/gi;
        let match;
        while ((match = spRegex.exec(code)) !== null) {
            const fullName = match[1];
            const parts = fullName.split('.');
            const schema = parts.length > 1 ? parts[0] : 'dbo';
            const name = parts.length > 1 ? parts[1] : parts[0];
            // Extract JIRA references from comments
            const jiraRegex = /--.*?([A-Z]+-\\d+)/g;
            const jiraRefs = [];
            let jiraMatch;
            while ((jiraMatch = jiraRegex.exec(code)) !== null) {
                jiraRefs.push(jiraMatch[1]);
            }
            // Extract parameters
            const paramRegex = /@(\\w+)\\s+(\\w+(?:\\([^)]*\\))?)/g;
            const parameters = [];
            let paramMatch;
            while ((paramMatch = paramRegex.exec(code)) !== null) {
                parameters.push({
                    name: `@${paramMatch[1]}`,
                    type: paramMatch[2],
                    description: 'Parameter extracted from stored procedure'
                });
            }
            // Extract table operations
            const tableRegex = /(UPDATE|INSERT|DELETE|SELECT).*?FROM\\s+([\\w\\.]+)/gi;
            const tablesAccessed = [];
            let tableMatch;
            while ((tableMatch = tableRegex.exec(code)) !== null) {
                tablesAccessed.push({
                    name: tableMatch[2],
                    operation: tableMatch[1].toUpperCase()
                });
            }
            procedures.push({
                name,
                schema,
                full_name: fullName,
                exists: true, // Assume exists since we're documenting it
                jira_references: jiraRefs,
                parameters,
                tables_accessed: tablesAccessed,
                business_logic: 'Extracted from code analysis'
            });
        }
        return procedures;
    }
    // Generate living document with version history at top
    addVersionHistory(versionHistory) {
        this.doc.addSection({
            children: [
                this.createSectionHeader('', 'Document Version History'),
                this.createStyledParagraph('Latest changes appear first (living document approach):', {
                    spacing: { after: 160 }
                })
            ]
        });
        // Sort by date descending (newest first)
        const sortedHistory = versionHistory.sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());
        sortedHistory.forEach((entry, index) => {
            const versionNum = `v1.${sortedHistory.length - index}`;
            this.doc.addSection({
                children: [
                    this.createStyledParagraph(`${versionNum} - ${entry.date}`, {
                        bold: true,
                        spacing: { before: 120, after: 40 }
                    }),
                    this.createStyledParagraph(`JIRA: ${entry.jira}`, {
                        indent: { left: 360 },
                        spacing: { after: 40 }
                    }),
                    this.createStyledParagraph(`Change: ${entry.change}`, {
                        indent: { left: 360 },
                        spacing: { after: 40 }
                    }),
                    this.createStyledParagraph(`Author: ${entry.author}`, {
                        indent: { left: 360 },
                        spacing: { after: 160 }
                    })
                ]
            });
        });
        this.doc.addSection({
            children: [this.createDivider()]
        });
    }
    async generate(data, outputPath) {
        // Document header
        this.doc.addSection({
            children: [
                new docx_1.Paragraph({
                    children: [
                        new docx_1.TextRun({
                            text: 'BUSINESS REQUEST DOCUMENTATION',
                            bold: true,
                            size: 32,
                            color: '2C5F8D'
                        })
                    ],
                    alignment: docx_1.AlignmentType.CENTER,
                    spacing: { after: 240 }
                }),
                new docx_1.Paragraph({
                    children: [
                        new docx_1.TextRun({
                            text: `Document ID: ${data.doc_id}`,
                            bold: true,
                            size: 24
                        })
                    ],
                    alignment: docx_1.AlignmentType.CENTER,
                    spacing: { after: 400 }
                })
            ]
        });
        // Summary table
        const summaryTable = new docx_1.Table({
            width: { size: 100, type: docx_1.WidthType.PERCENTAGE },
            rows: [
                new docx_1.TableRow({
                    children: [
                        new docx_1.TableCell({ children: [this.createStyledParagraph('Jira', { bold: true })] }),
                        new docx_1.TableCell({ children: [this.createStyledParagraph(data.jira)] }),
                    ]
                }),
                new docx_1.TableRow({
                    children: [
                        new docx_1.TableCell({ children: [this.createStyledParagraph('Status', { bold: true })] }),
                        new docx_1.TableCell({ children: [this.createStyledParagraph(data.status)] }),
                    ]
                }),
                new docx_1.TableRow({
                    children: [
                        new docx_1.TableCell({ children: [this.createStyledParagraph('Date Requested', { bold: true })] }),
                        new docx_1.TableCell({ children: [this.createStyledParagraph(data.date)] }),
                    ]
                }),
                new docx_1.TableRow({
                    children: [
                        new docx_1.TableCell({ children: [this.createStyledParagraph('Reported By', { bold: true })] }),
                        new docx_1.TableCell({ children: [this.createStyledParagraph(data.reported_by)] }),
                    ]
                }),
                new docx_1.TableRow({
                    children: [
                        new docx_1.TableCell({ children: [this.createStyledParagraph('Assigned To', { bold: true })] }),
                        new docx_1.TableCell({ children: [this.createStyledParagraph(data.assigned_to)] }),
                    ]
                })
            ]
        });
        this.doc.addSection({
            children: [summaryTable, this.createDivider()]
        });
        // Business summary
        this.doc.addSection({
            children: [
                this.createSectionHeader('1', 'Business Summary'),
                this.createStyledParagraph(data.summary, { spacing: { after: 160 } })
            ]
        });
        // Business purpose
        this.doc.addSection({
            children: [
                this.createSectionHeader('2', 'Business Purpose'),
                this.createStyledParagraph(data.purpose, { spacing: { after: 160 } })
            ]
        });
        // Technical details
        this.doc.addSection({
            children: [
                this.createSectionHeader('3', 'Technical Details'),
                this.createSubHeader('Affected Object:'),
                this.createStyledParagraph(`${data.schema}.${data.table}`, { spacing: { after: 120 } }),
                this.createSubHeader('Columns Modified:'),
                this.createStyledParagraph(data.column, { spacing: { after: 120 } }),
                this.createSubHeader('Data Type:'),
                this.createStyledParagraph(data.data_type, { spacing: { after: 120 } }),
                this.createSubHeader('Valid Values:'),
                this.createStyledParagraph(data.values, { bold: true, spacing: { after: 160 } })
            ]
        });
        // Business rules
        this.doc.addSection({
            children: [
                this.createSectionHeader('4', 'Business Rules'),
                this.createStyledParagraph(data.rule_def, { bold: true, spacing: { after: 160 } })
            ]
        });
        // Code implementation
        this.doc.addSection({
            children: [
                this.createSectionHeader('5', 'Code Implementation'),
                ...this.createCodeBlock(data.code)
            ]
        });
        // Stored procedure analysis
        const procedures = this.analyzeStoredProcedures(data.code);
        if (procedures.length > 0) {
            this.doc.addSection({
                children: [
                    this.createSectionHeader('6', 'Stored Procedure Analysis'),
                    this.createSubHeader('Detected JIRA References in Code:'),
                    ...procedures.map(proc => this.createStyledParagraph(`• ${proc.full_name}: ${proc.jira_references.join(', ') || 'None detected'}`, {
                        spacing: { after: 80 }
                    }))
                ]
            });
        }
        // Code explanation
        this.doc.addSection({
            children: [
                this.createSectionHeader('7', 'Code Explanation'),
                this.createStyledParagraph(data.code_explain, { bold: true, spacing: { after: 160 } })
            ]
        });
        // Generate document
        const buffer = await docx_1.Packer.toBuffer(this.doc);
        fs.writeFileSync(outputPath, buffer);
        console.log(`✅ TypeScript Generated: ${outputPath}`);
    }
}
exports.BusinessRequestGenerator = BusinessRequestGenerator;
// Main execution
async function main() {
    if (process.argv.length < 3) {
        console.log('Usage: node script.js <json_file> [output_file]');
        process.exit(1);
    }
    const jsonFile = process.argv[2];
    const outputFile = process.argv[3] || 'typescript_output.docx';
    try {
        const data = JSON.parse(fs.readFileSync(jsonFile, 'utf8'));
        const generator = new BusinessRequestGenerator();
        // Add version history if available
        const versionHistory = [
            {
                date: data.date || '01/07/2026',
                jira: data.jira,
                change: `Initial implementation: ${data.summary}`,
                author: data.assigned_to || 'System'
            }
        ];
        await generator.generate(data, outputFile);
    }
    catch (error) {
        console.error('Error generating document:', error);
        process.exit(1);
    }
}
if (require.main === module) {
    main();
}
