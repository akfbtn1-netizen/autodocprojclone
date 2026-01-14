// =============================================
// API TYPES - Maps to actual database columns
// File: frontend/src/types/api.ts
// =============================================

// =============================================
// APPROVAL TYPES (from DaQa.DocumentApprovals + DaQa.ApprovalWorkflow)
// =============================================

export interface Approval {
  // From DocumentApprovals table
  id: number;
  documentId: string;
  masterIndexId: number | null;
  objectName: string;
  schemaName: string;
  databaseName: string;
  documentType: string;
  templateUsed: string | null;
  cabNumber: string | null;
  generatedFilePath: string | null;
  destinationPath: string | null;
  fileSizeBytes: number | null;
  status: ApprovalStatus;
  priority: Priority;
  requestedBy: string;
  requestedAt: string;
  assignedTo: string | null;
  dueDate: string | null;
  resolvedBy: string | null;
  resolvedAt: string | null;
  resolutionNotes: string | null;
  version: number;
  previousVersionId: number | null;
  createdAt: string;
  modifiedAt: string | null;
  
  // From ApprovalWorkflow table (joined)
  approverEmail?: string;
  approvalStatus?: string;
  approvedDate?: string;
  comments?: string;
  rejectionReason?: string;
  approvedBy?: string;
  docIdString?: string;
}

export type ApprovalStatus = 
  | 'Pending' 
  | 'InReview' 
  | 'Approved' 
  | 'Rejected' 
  | 'ChangesRequested';

export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';

// =============================================
// DOCUMENT TYPES (from DaQa.MasterIndex - 119 columns)
// =============================================

export interface Document {
  indexId: number;
  sourceSystem: string | null;
  sourceDocumentId: string | null;
  sourceFilePath: string | null;
  documentTitle: string | null;
  documentType: string | null;
  description: string | null;
  
  // Business context
  businessDomain: string | null;
  businessProcess: string | null;
  businessOwner: string | null;
  technicalOwner: string | null;
  
  // Object reference
  systemName: string | null;
  databaseName: string | null;
  schemaName: string | null;
  tableName: string | null;
  columnName: string | null;
  dataType: string | null;
  
  // Classification
  dataClassification: string | null;
  sensitivity: string | null;
  complianceFlags: string | null;
  
  // Quality
  qualityScore: number | null;
  completenessScore: number | null;
  dataQualityScore: number | null;
  
  // Generated doc
  generatedDocPath: string | null;
  generatedDocUrl: string | null;
  fileSize: number | null;
  fileHash: string | null;
  
  // Version
  versionNumber: number | null;
  isLatestVersion: boolean;
  previousVersionId: number | null;
  
  // Status
  status: string | null;
  workflowStatus: string | null;
  approvalStatus: string | null;
  approvedBy: string | null;
  approvedDate: string | null;
  
  // Identifiers
  cabNumber: string | null;
  docId: string | null;
  
  // AI
  aiGeneratedTags: string | null;
  semanticCategory: string | null;
  technicalComplexity: string | null;
  
  // PII
  containsPii: boolean;
  piiIndicator: boolean;
  piiTypes: string | null;
  sensitivityLevel: string | null;
  
  // Audit
  createdDate: string;
  createdBy: string | null;
  modifiedDate: string | null;
  modifiedBy: string | null;
  isDeleted: boolean;
}

// =============================================
// PIPELINE TYPES (from DaQa.DocumentationQueue)
// =============================================

export interface PipelineItem {
  queueId: number;
  changeId: number | null;
  objectName: string;
  priority: Priority;
  status: string;
  assignedTo: string | null;
  createdDate: string;
  startedDate: string | null;
  completedDate: string | null;
  documentUrl: string | null;
  errorMessage: string | null;
  docIdString: string | null;
}

export interface PipelineStatus {
  queued: number;
  processing: number;
  completed: number;
  failed: number;
}

export interface PipelineStage {
  name: string;
  status: 'idle' | 'active' | 'completed' | 'error';
  count: number;
}

// =============================================
// WORKFLOW EVENTS (from DaQa.WorkflowEvents)
// =============================================

export interface WorkflowEvent {
  eventId: number;
  workflowId: string | null;
  eventType: string;
  status: string;
  message: string;
  durationMs: number | null;
  timestamp: string;
  metadata: string | null;
}

// =============================================
// SCHEMA CHANGES (from DaQa.SchemaChangeLog - display only)
// =============================================

export interface SchemaChange {
  changeId: number;
  databaseName: string;
  schemaName: string;
  objectName: string;
  objectType: string;
  changeType: 'CREATE' | 'ALTER' | 'DROP';
  changeDetails: string | null;
  detectedDate: string;
  detectedBy: string;
  processedDate: string | null;
  processedStatus: string | null;
  errorMessage: string | null;
}

// =============================================
// REQUEST TYPES
// =============================================

export interface ApproveRequest {
  comments?: string;
  approvedBy: string;
}

export interface RejectRequest {
  rejectionReason: string;
  rejectedBy: string;
}

// Maps to DaQa.DocumentEdits table
export interface EditRequest {
  sectionName: string;
  originalText: string;
  editedText: string;
  editReason: string;
  editCategory: 'Factual' | 'Clarity' | 'Formatting' | 'Technical' | 'Other';
  shouldTrainAi: boolean;
}

// Maps to DaQa.RegenerationRequests table
export interface RepromptRequest {
  feedbackText: string;
  feedbackSection?: string;
  additionalContext?: string;
}

// =============================================
// STATISTICS TYPES
// =============================================

export interface DashboardStats {
  documents: {
    total: number;
    draft: number;
    pendingApproval: number;
    approved: number;
    rejected: number;
    published: number;
  };
  approvals: {
    pending: number;
    inReview: number;
    approvedToday: number;
    approvedThisWeek: number;
    averageApprovalTimeHours: number;
  };
  pipeline: {
    queued: number;
    processing: number;
    completed: number;
    failed: number;
  };
}

export interface ApprovalStats {
  pending: number;
  approved: number;
  rejected: number;
  changesRequested: number;
  averageTimeToApproval: number;
  approvalsByDay: Array<{ date: string; count: number }>;
  approvalsByType: Array<{ type: string; count: number }>;
}

// =============================================
// SIGNALR EVENT TYPES
// =============================================

export interface DocumentGeneratedEvent {
  documentId: string;
  docIdString: string;
  objectName: string;
  documentType: string;
  generatedAt: string;
  generatedBy: string;
}

export interface ApprovalRequestedEvent {
  approvalId: number;
  documentId: string;
  requestedBy: string;
  requestedAt: string;
  priority: Priority;
}

export interface ApprovalDecisionEvent {
  approvalId: number;
  documentId: string;
  decision: 'Approved' | 'Rejected' | 'ChangesRequested';
  decidedBy: string;
  decidedAt: string;
  comments?: string;
}

export interface MasterIndexUpdatedEvent {
  indexId: number;
  documentId: string;
  updateType: 'Created' | 'Updated' | 'Deleted';
  updatedAt: string;
}

// =============================================
// API RESPONSE WRAPPERS
// =============================================

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ApiError {
  message: string;
  code?: string;
  details?: Record<string, string[]>;
}
