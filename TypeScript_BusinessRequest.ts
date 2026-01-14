import { Document, Packer, Paragraph, TextRun, Table, TableCell, TableRow, AlignmentType, BorderStyle, WidthType, HeadingLevel } from 'docx';
import * as fs from 'fs';

interface BusinessRequestData {
  doc_id: string;
  jira: string;
  status: string;
  date: string;
  reported_by: string;
  assigned_to: string;
  schema: string;
  table: string;
  column: string;
  data_type: string;
  values: string;
  purpose: string;
  summary: string;
  rule_def: string;
  code: string;
  code_explain: string;
  control_table: string;
  table_desc: string;
  key_columns: string[];
}

interface StoredProcedureAnalysis {
  name: string;
  schema: string;
  full_name: string;
  exists: boolean;
  jira_references: string[];
  parameters: { name: string; type: string; description: string }[];
  tables_accessed: { name: string; operation: string }[];
  business_logic: string;
}

interface VersionHistoryEntry {
  date: string;
  jira: string;
  change: string;
  author: string;
}

class BusinessRequestGenerator {
  private doc: Document;

  constructor() {
    this.doc = new Document({
      sections: []
    });
  }

  // Helper method to create styled paragraphs
  private createStyledParagraph(text: string, options?: {
    bold?: boolean;
    size?: number;
    color?: string;
    spacing?: { before?: number; after?: number };
    alignment?: typeof AlignmentType[keyof typeof AlignmentType];
    indent?: { left?: number };
  }): Paragraph {
    const runs = [];
    
    if (options?.bold && text.includes('**')) {
      // Parse markdown-style bold
      const parts = text.split('**');
      parts.forEach((part, index) => {
        if (index % 2 === 0) {
          // Regular text
          if (part) {
            runs.push(new TextRun({
              text: part,
              size: options.size || 22,
              color: options.color || '000000'
            }));
          }
        } else {
          // Bold text
          runs.push(new TextRun({
            text: part,
            bold: true,
            size: options.size || 22,
            color: options.color || '000000'
          }));
        }
      });
    } else {
      runs.push(new TextRun({
        text: text,
        bold: options?.bold || false,
        size: options?.size || 22,
        color: options?.color || '000000'
      }));
    }

    return new Paragraph({
      children: runs,
      alignment: options?.alignment || AlignmentType.LEFT,
      spacing: {
        before: options?.spacing?.before || 0,
        after: options?.spacing?.after || 0
      },
      indent: options?.indent ? { left: options.indent.left } : undefined
    });
  }

  // Create section divider
  private createDivider(): Paragraph {
    return new Paragraph({
      children: [new TextRun({ text: '', size: 1 })],
      border: {
        bottom: {
          color: '2C5F8D',
          space: 1,
          style: BorderStyle.SINGLE,
          size: 6
        }
      },
      spacing: { before: 160, after: 160 }
    });
  }

  // Create section header
  private createSectionHeader(number: string, title: string): Paragraph {
    return this.createStyledParagraph(`${number}. ${title}`, {
      bold: true,
      size: 26,
      color: '2C5F8D',
      spacing: { before: 240, after: 160 }
    });
  }

  // Create sub-header
  private createSubHeader(text: string, indent?: number): Paragraph {
    return this.createStyledParagraph(text, {
      bold: true,
      size: 24,
      spacing: { before: 120, after: 80 },
      indent: indent ? { left: indent } : undefined
    });
  }

  // Create code block
  private createCodeBlock(code: string): Paragraph[] {
    const paragraphs: Paragraph[] = [];
    const lines = code.split('\n');
    
    lines.forEach(line => {
      paragraphs.push(new Paragraph({
        children: [new TextRun({
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
  private analyzeStoredProcedures(code: string): StoredProcedureAnalysis[] {
    const procedures: StoredProcedureAnalysis[] = [];
    
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
      const jiraRefs: string[] = [];
      let jiraMatch;
      while ((jiraMatch = jiraRegex.exec(code)) !== null) {
        jiraRefs.push(jiraMatch[1]);
      }
      
      // Extract parameters
      const paramRegex = /@(\\w+)\\s+(\\w+(?:\\([^)]*\\))?)/g;
      const parameters: { name: string; type: string; description: string }[] = [];
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
      const tablesAccessed: { name: string; operation: string }[] = [];
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
  private addVersionHistory(versionHistory: VersionHistoryEntry[]): void {
    this.doc.addSection({
      children: [
        this.createSectionHeader('', 'Document Version History'),
        this.createStyledParagraph('Latest changes appear first (living document approach):', {
          spacing: { after: 160 }
        })
      ]
    });

    // Sort by date descending (newest first)
    const sortedHistory = versionHistory.sort((a, b) => 
      new Date(b.date).getTime() - new Date(a.date).getTime()
    );

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

  public async generate(data: BusinessRequestData, outputPath: string): Promise<void> {
    const children: any[] = [];

    // Summary table
    const summaryTable = new Table({
      width: { size: 100, type: WidthType.PERCENTAGE },
      rows: [
        new TableRow({
          children: [
            new TableCell({ children: [this.createStyledParagraph('Jira', { bold: true })] }),
            new TableCell({ children: [this.createStyledParagraph(data.jira)] }),
          ]
        }),
        new TableRow({
          children: [
            new TableCell({ children: [this.createStyledParagraph('Status', { bold: true })] }),
            new TableCell({ children: [this.createStyledParagraph(data.status)] }),
          ]
        }),
        new TableRow({
          children: [
            new TableCell({ children: [this.createStyledParagraph('Date Requested', { bold: true })] }),
            new TableCell({ children: [this.createStyledParagraph(data.date)] }),
          ]
        }),
        new TableRow({
          children: [
            new TableCell({ children: [this.createStyledParagraph('Reported By', { bold: true })] }),
            new TableCell({ children: [this.createStyledParagraph(data.reported_by)] }),
          ]
        }),
        new TableRow({
          children: [
            new TableCell({ children: [this.createStyledParagraph('Assigned To', { bold: true })] }),
            new TableCell({ children: [this.createStyledParagraph(data.assigned_to)] }),
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
          ...procedures.map(proc => 
            this.createStyledParagraph(`• ${proc.full_name}: ${proc.jira_references.join(', ') || 'None detected'}`, {
              spacing: { after: 80 }
            })
          )
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
    const buffer = await Packer.toBuffer(this.doc);
    fs.writeFileSync(outputPath, buffer);
    console.log(`✅ TypeScript Generated: ${outputPath}`);
  }
}

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
    const versionHistory: VersionHistoryEntry[] = [
      {
        date: data.date || '01/07/2026',
        jira: data.jira,
        change: `Initial implementation: ${data.summary}`,
        author: data.assigned_to || 'System'
      }
    ];
    
    await generator.generate(data, outputFile);
  } catch (error) {
    console.error('Error generating document:', error);
    process.exit(1);
  }
}

if (require.main === module) {
  main();
}

export { BusinessRequestGenerator, BusinessRequestData };