// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex API Types
// Matches backend DTOs from src/Shared/Contracts/DTOs/MasterIndexDtos.cs
// ═══════════════════════════════════════════════════════════════════════════

/**
 * Summary DTO for MasterIndex records displayed in lists and grids.
 * Maps to backend MasterIndexSummaryDto
 */
export interface MasterIndexSummary {
  indexId: number;
  databaseName: string | null;
  schemaName: string | null;
  tableName: string | null;
  columnName: string | null;
  objectType: string | null;
  approvalStatus: string | null;
  workflowStatus: string | null;
  lastModifiedDate: string | null;
  category: string | null;
  tier: string | null;
  objectPath: string | null;
}

/**
 * Detailed DTO for single MasterIndex record view.
 * Maps to backend MasterIndexDetailDto
 */
export interface MasterIndexDetail {
  // Identity
  indexId: number;
  physicalName: string | null;
  logicalName: string | null;
  schemaName: string | null;
  databaseName: string | null;
  serverName: string | null;
  objectType: string | null;
  columnName: string | null;

  // Classification
  category: string | null;
  subCategory: string | null;
  businessDomain: string | null;
  tier: string | null;
  tags: string[];

  // Documentation
  description: string | null;
  technicalSummary: string | null;
  businessPurpose: string | null;
  usageNotes: string | null;

  // Document Generation
  generatedDocPath: string | null;
  generatedDate: string | null;
  approvalStatus: string | null;
  approvedBy: string | null;
  approvedDate: string | null;
  documentVersion: string | null;

  // Data Type (for columns)
  dataType: string | null;
  maxLength: number | null;
  isNullable: boolean | null;
  defaultValue: string | null;

  // Compliance
  piiIndicator: boolean | null;
  dataClassification: string | null;
  securityLevel: string | null;

  // Audit
  createdDate: string | null;
  createdBy: string | null;
  modifiedDate: string | null;
  modifiedBy: string | null;
  isActive: boolean;
}

/**
 * Statistics DTO for dashboard and reporting.
 * Maps to backend MasterIndexStatisticsDto
 */
export interface MasterIndexStatistics {
  totalDocuments: number;
  draftCount: number;
  pendingCount: number;
  approvedCount: number;
  rejectedCount: number;
  byCategory: Record<string, number>;
  byObjectType: Record<string, number>;
  byDatabase: Record<string, number>;
  byTier: Record<string, number>;
  computedAt: string;
}

/**
 * Generic paginated response wrapper.
 * Maps to backend PaginatedResponse<T>
 */
export interface PaginatedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/**
 * Filter state for MasterIndex queries
 */
export interface MasterIndexFilters {
  status?: string;
  database?: string;
  tier?: string;
  query?: string;
  pageNumber: number;
  pageSize: number;
}

/**
 * Display name mapping for business-friendly column labels.
 * Can be updated without code changes as schema evolves.
 */
export const MasterIndexDisplayNames: Record<string, string> = {
  indexId: 'ID',
  databaseName: 'Database',
  schemaName: 'Schema',
  tableName: 'Table',
  columnName: 'Column',
  objectType: 'Object Type',
  approvalStatus: 'Approval Status',
  workflowStatus: 'Workflow Status',
  lastModifiedDate: 'Last Modified',
  category: 'Category',
  tier: 'Tier',
  objectPath: 'Object Path',
  physicalName: 'Physical Name',
  logicalName: 'Logical Name',
  serverName: 'Server',
  subCategory: 'Sub-Category',
  businessDomain: 'Business Domain',
  tags: 'Tags',
  description: 'Description',
  technicalSummary: 'Technical Summary',
  businessPurpose: 'Business Purpose',
  usageNotes: 'Usage Notes',
  generatedDocPath: 'Document Path',
  generatedDate: 'Generated Date',
  approvedBy: 'Approved By',
  approvedDate: 'Approved Date',
  documentVersion: 'Version',
  dataType: 'Data Type',
  maxLength: 'Max Length',
  isNullable: 'Nullable',
  defaultValue: 'Default Value',
  piiIndicator: 'Contains PII',
  dataClassification: 'Data Classification',
  securityLevel: 'Security Level',
  createdDate: 'Created Date',
  createdBy: 'Created By',
  modifiedDate: 'Modified Date',
  modifiedBy: 'Modified By',
  isActive: 'Active',
};

/**
 * Status badge color mapping
 */
export const ApprovalStatusColors: Record<string, { bg: string; text: string; border: string }> = {
  Draft: { bg: 'bg-stone-100', text: 'text-stone-700', border: 'border-stone-300' },
  Pending: { bg: 'bg-amber-100', text: 'text-amber-700', border: 'border-amber-300' },
  Approved: { bg: 'bg-teal-100', text: 'text-teal-700', border: 'border-teal-300' },
  Rejected: { bg: 'bg-red-100', text: 'text-red-700', border: 'border-red-300' },
};

/**
 * Tier description mapping
 */
export const TierDescriptions: Record<string, string> = {
  '1': 'Complex - Requires detailed documentation',
  '2': 'Standard - Standard documentation template',
  '3': 'Simple - Basic documentation only',
};
