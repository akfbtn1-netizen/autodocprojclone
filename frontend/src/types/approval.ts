// ═══════════════════════════════════════════════════════════════════════════
// Approval Workflow Types
// Matches: 17-table database schema (DocumentApprovals, ApprovalTracking, etc.)
// ═══════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// Core Approval Types
// ─────────────────────────────────────────────────────────────────────────────

export type ApprovalStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Editing'
  | 'RePromptRequested';

export type ApprovalPriority = 'High' | 'Medium' | 'Low';

export type DocumentTypeCode = 'DF' | 'EN' | 'BR' | 'AN' | 'ER' | 'EQ' | 'RS';

export const DocumentTypeLabels: Record<DocumentTypeCode, string> = {
  DF: 'Defect Fix',
  EN: 'Enhancement',
  BR: 'Business Request',
  AN: 'Analysis',
  ER: 'Error Report',
  EQ: 'Equipment',
  RS: 'Research',
};

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.DocumentApprovals (Primary Approval Table)
// ─────────────────────────────────────────────────────────────────────────────

export interface Approval {
  id: number;
  documentId: string;
  masterIndexId: number | null;
  objectName: string;
  schemaName: string;
  databaseName: string;
  documentType: DocumentTypeCode;
  templateUsed: string;
  cabNumber: string;

  // File paths
  generatedFilePath: string;
  destinationPath: string | null;
  fileSizeBytes: number | null;

  // Approval state
  status: ApprovalStatus;
  priority: ApprovalPriority;

  // Assignment
  requestedBy: string;
  requestedAt: string; // ISO datetime
  assignedTo: string | null;
  dueDate: string | null;

  // Resolution
  resolvedBy: string | null;
  resolvedAt: string | null;
  resolutionNotes: string | null;

  // Versioning
  version: number;
  previousVersionId: number | null;

  // Timestamps
  createdAt: string;
  modifiedAt: string | null;

  // Computed fields (from joins)
  changeDescription?: string; // From DocumentChanges
  jiraNumber?: string;
  tableName?: string;
  qualityRating?: number; // From ApprovalTracking
  sharepointLink?: string; // Generated or from MasterIndex
}

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.ApprovalHistory
// ─────────────────────────────────────────────────────────────────────────────

export interface ApprovalHistory {
  id: number;
  approvalId: number;
  documentId: string;
  action: ApprovalHistoryAction;
  actionBy: string;
  actionAt: string;
  previousStatus: string | null;
  newStatus: string;
  notes: string | null;
  sourcePath: string | null;
  destinationPath: string | null;
}

export type ApprovalHistoryAction =
  | 'Submitted'
  | 'Approved'
  | 'Rejected'
  | 'RePrompted'
  | 'Edited';

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.ApprovalTracking (Learning Data)
// ─────────────────────────────────────────────────────────────────────────────

export interface ApprovalTracking {
  id: number;
  docId: string;
  action: ApprovalHistoryAction;
  approverUserId: number | null;
  approverName: string | null;
  comments: string | null;
  actionDate: string;
  createdBy: string | null;
  createdDate: string;
  modifiedBy: string | null;
  modifiedDate: string | null;
  isDeleted: boolean;

  // Content tracking for AI learning
  originalContent: string | null;
  editedContent: string | null;
  contentDiff: string | null;
  changedFields: string | null; // JSON array of field names

  // Feedback for continuous learning
  rejectionReason: string | null;
  rerequestPrompt: string | null;
  approverFeedback: string | null;
  qualityRating: number | null; // 1-5 stars

  // Metadata
  documentType: DocumentTypeCode | null;
  changeType: string | null;
  wasAIEnhanced: boolean;
}

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.DocumentEdits (Section-level edits for AI training)
// ─────────────────────────────────────────────────────────────────────────────

export interface DocumentEdit {
  id: number;
  approvalId: number;
  documentId: string;
  sectionName: string;
  originalText: string;
  editedText: string;
  editReason: string | null;
  editCategory: EditCategory;
  editedBy: string;
  editedAt: string;
  shouldTrainAI: boolean;
  aiFeedbackProcessed: boolean;
}

export type EditCategory =
  | 'Clarification'
  | 'FactualCorrection'
  | 'StyleImprovement'
  | 'Addition'
  | 'Removal';

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.RegenerationRequests (Re-prompt workflow)
// ─────────────────────────────────────────────────────────────────────────────

export interface RegenerationRequest {
  id?: number;
  approvalId: number;
  documentId: string;
  originalVersion: number;
  feedbackText: string;
  feedbackSection: string | null;
  additionalContext: string | null;
  requestedBy: string;
  requestedAt?: string;
  status?: RegenerationStatus;
  newVersion?: number | null;
  newApprovalId?: number | null;
  completedAt?: string | null;
  errorMessage?: string | null;
}

export type RegenerationStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.WorkflowEvents (Event Sourcing)
// ─────────────────────────────────────────────────────────────────────────────

export interface WorkflowEvent {
  eventId: number;
  workflowId: string; // DocumentId
  eventType: WorkflowEventType;
  status: 'Success' | 'Failed' | 'Pending';
  message: string | null;
  durationMs: number | null;
  timestamp: string;
  metadata: string | null; // JSON blob
}

export type WorkflowEventType =
  | 'ApprovalRequested'
  | 'ApprovalViewed'
  | 'Approved'
  | 'Rejected'
  | 'Edited'
  | 'RePromptRequested'
  | 'NotificationSent'
  | 'EscalationTriggered';

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.Approvers
// ─────────────────────────────────────────────────────────────────────────────

export interface Approver {
  id: number;
  email: string;
  displayName: string;
  isActive: boolean;
  notificationPreference: NotificationPreference | null;
  teamsWebhookUrl: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export type NotificationPreference = 'Teams' | 'Email' | 'Both' | 'None';

// ─────────────────────────────────────────────────────────────────────────────
// DaQa.Notifications
// ─────────────────────────────────────────────────────────────────────────────

export interface Notification {
  notificationId: number;
  notificationType: string;
  title: string;
  message: string;
  tableName: string | null;
  columnName: string | null;
  changeType: string | null;
  priority: string | null;
  jiraNumber: string | null;
  documentPath: string | null;
  isRead: boolean;
  isSent: boolean;
  sentDate: string | null;
  readDate: string | null;
  createdDate: string;
  createdBy: string | null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Statistics & Filters
// ─────────────────────────────────────────────────────────────────────────────

export interface ApprovalStats {
  total: number;
  pending: number;
  approved: number;
  rejected: number;
  editing: number;
  rePromptRequested: number;
  avgTimeToApproval: number; // in hours
  oldestPending: string | null; // ISO datetime
}

export interface ApprovalFilters {
  status?: ApprovalStatus[];
  documentType?: DocumentTypeCode[];
  priority?: ApprovalPriority[];
  assignedTo?: string;
  dateRange?: {
    start: string;
    end: string;
  };
  searchQuery?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// Action DTOs
// ─────────────────────────────────────────────────────────────────────────────

export interface ApprovalAction {
  action: 'Approve' | 'Reject' | 'RePrompt' | 'Edit';
  comments?: string;
  rejectionReason?: string;
  qualityRating?: number; // 1-5 stars
  feedbackText?: string;
  additionalContext?: string;
}

export interface ApproveRequest {
  comments?: string;
  qualityRating?: number;
  feedbackText?: string;
}

export interface RejectRequest {
  rejectionReason: string;
  comments?: string;
  qualityRating?: number;
  feedbackText?: string;
}

export interface EditSubmission {
  approvalId: number;
  edits: Omit<DocumentEdit, 'id' | 'editedAt'>[];
}

// ─────────────────────────────────────────────────────────────────────────────
// Status Colors & Display Helpers
// ─────────────────────────────────────────────────────────────────────────────

export const statusColors: Record<ApprovalStatus, string> = {
  PendingApproval: '#FDB022', // Amber
  Approved: '#13A10E', // Green
  Rejected: '#D13438', // Red
  Editing: '#8764B8', // Purple
  RePromptRequested: '#0078D4', // Blue
};

export const priorityColors: Record<ApprovalPriority, string> = {
  High: '#D13438', // Red
  Medium: '#FDB022', // Amber
  Low: '#13A10E', // Green
};

export const getStatusDisplayName = (status: ApprovalStatus): string => {
  const displayNames: Record<ApprovalStatus, string> = {
    PendingApproval: 'Pending Approval',
    Approved: 'Approved',
    Rejected: 'Rejected',
    Editing: 'In Editing',
    RePromptRequested: 'Re-prompt Requested',
  };
  return displayNames[status] || status;
};

export const getStatusVariant = (
  status: ApprovalStatus
): 'warning' | 'success' | 'danger' | 'brand' | 'default' => {
  const variants: Record<ApprovalStatus, 'warning' | 'success' | 'danger' | 'brand' | 'default'> = {
    PendingApproval: 'warning',
    Approved: 'success',
    Rejected: 'danger',
    Editing: 'brand',
    RePromptRequested: 'brand',
  };
  return variants[status] || 'default';
};
