// Document Types
export type DocumentStatus = 
  | 'draft'
  | 'pending'
  | 'review'
  | 'approved'
  | 'completed'
  | 'rejected';

export interface User {
  id: string;
  name: string;
  email: string;
  role?: 'admin' | 'approver' | 'viewer';
  avatar?: string;
}

export interface Document {
  id: string;
  docId: string;
  title: string;
  description?: string;
  type: string;
  status: DocumentStatus;
  author: User;
  approvers?: User[];
  jiraTicket?: string;
  createdAt: string;
  updatedAt: string;
  approvalHistory?: ApprovalHistoryEntry[];
  metadata?: DocumentMetadata;
}

export interface DocumentMetadata {
  storedProcedure?: string;
  schema?: string;
  database?: string;
  complexity?: 'low' | 'medium' | 'high';
  aiEnhanced?: boolean;
  confidenceScore?: number;
  wordCount?: number;
  pageCount?: number;
}

export interface ApprovalHistoryEntry {
  id: string;
  action: 'submitted' | 'approved' | 'rejected' | 'commented';
  actor: string;
  timestamp: string;
  comment?: string;
}

// Workflow Types
export interface WorkflowStage {
  id: string;
  name: string;
  status: 'waiting' | 'active' | 'completed' | 'skipped';
  position: { x: number; y: number };
  documentCount: number;
  avgProcessingTime?: string;
}

export interface WorkflowConnection {
  id: string;
  source: string;
  target: string;
  animated?: boolean;
}

export interface WorkflowStats {
  totalDocuments: number;
  pendingApprovals: number;
  approvedToday: number;
  avgProcessingTime: string;
  completionRate: number;
}

// Agent Types
export interface Agent {
  id: string;
  name: string;
  type: 'documentation' | 'schema' | 'lineage' | 'quality' | 'compliance';
  status: 'idle' | 'processing' | 'error';
  description: string;
  lastActivity?: string;
  processedCount: number;
  icon: string;
}

// Approval Types
export interface ApprovalRequest {
  id: string;
  documentId: string;
  documentTitle: string;
  requester: User;
  requestedAt: string;
  dueDate: string;
  priority: 'low' | 'medium' | 'high' | 'urgent';
  status: 'pending' | 'approved' | 'rejected';
  comments?: string;
  reviewedAt?: string;
  reviewer?: User;
  rejectionReason?: string;
}

// API Response Types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
  meta?: {
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
  };
}

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, string[]>;
}

export type Result<T> = 
  | { success: true; data: T }
  | { success: false; error: ApiError };

// Notification Types
export interface Notification {
  id: string;
  type: 'approval' | 'status' | 'system' | 'agent';
  title: string;
  message: string;
  timestamp: string;
  read: boolean;
  actionUrl?: string;
}

// Dashboard KPI Types
export interface KpiData {
  id: string;
  label: string;
  value: number | string;
  change?: number;
  changeLabel?: string;
  changeType?: 'increase' | 'decrease' | 'neutral';
  icon: 'documents' | 'pending' | 'approved' | 'time' | 'rate' | 'ai';
  variant?: 'default' | 'brand' | 'success' | 'warning' | 'danger';
}
