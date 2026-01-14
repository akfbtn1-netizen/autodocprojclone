// ═══════════════════════════════════════════════════════════════════════════
// Approval Service
// Endpoints: /api/approvals/*
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient } from './api';
import type {
  ApprovalRequest,
  ApprovalStatus,
  ApprovalPriority,
  Result,
  PaginationMeta,
} from '@/types';

export interface ApprovalListResponse {
  approvals: ApprovalRequest[];
  meta: PaginationMeta;
}

export interface ApprovalAction {
  comment?: string;
  reason?: string;
}

export interface ApprovalStats {
  pending: number;
  approvedToday: number;
  rejectedToday: number;
  avgResponseTimeHours: number;
  byPriority: Record<ApprovalPriority, number>;
  overdue: number;
}

export const approvalService = {
  // ─────────────────────────────────────────────────────────────────────────
  // Approval Queries
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get pending approvals for current user
   */
  getPendingApprovals: async (
    page = 1,
    pageSize = 20
  ): Promise<Result<ApprovalListResponse>> => {
    return apiClient.get<ApprovalListResponse>(
      `/approvals/pending?page=${page}&pageSize=${pageSize}`
    );
  },

  /**
   * Get all approvals with optional filters
   */
  getApprovals: async (options?: {
    status?: ApprovalStatus;
    priority?: ApprovalPriority;
    documentType?: string;
    page?: number;
    pageSize?: number;
  }): Promise<Result<ApprovalListResponse>> => {
    const params = new URLSearchParams();
    if (options?.status) params.set('status', options.status);
    if (options?.priority) params.set('priority', options.priority);
    if (options?.documentType) params.set('documentType', options.documentType);
    if (options?.page) params.set('page', options.page.toString());
    if (options?.pageSize) params.set('pageSize', options.pageSize.toString());

    return apiClient.get<ApprovalListResponse>(`/approvals?${params.toString()}`);
  },

  /**
   * Get single approval request by ID
   */
  getApproval: async (id: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.get<ApprovalRequest>(`/approvals/${id}`);
  },

  /**
   * Get approval by document ID
   */
  getApprovalByDocumentId: async (documentId: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.get<ApprovalRequest>(`/approvals/by-document/${documentId}`);
  },

  /**
   * Get my approval history
   */
  getMyApprovalHistory: async (
    page = 1,
    pageSize = 20
  ): Promise<Result<ApprovalListResponse>> => {
    return apiClient.get<ApprovalListResponse>(
      `/approvals/my-history?page=${page}&pageSize=${pageSize}`
    );
  },

  /**
   * Get approval statistics
   */
  getStats: async (): Promise<Result<ApprovalStats>> => {
    return apiClient.get<ApprovalStats>('/approvals/stats');
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Approval Actions
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Approve a document
   */
  approve: async (id: string, action?: ApprovalAction): Promise<Result<ApprovalRequest>> => {
    return apiClient.put<ApprovalRequest>(`/approvals/${id}/approve`, action);
  },

  /**
   * Reject a document
   */
  reject: async (id: string, action: ApprovalAction): Promise<Result<ApprovalRequest>> => {
    if (!action.reason) {
      return {
        success: false,
        error: { code: 'VALIDATION_ERROR', message: 'Rejection reason is required' },
      };
    }
    return apiClient.put<ApprovalRequest>(`/approvals/${id}/reject`, action);
  },

  /**
   * Request changes (soft reject)
   */
  requestChanges: async (id: string, action: ApprovalAction): Promise<Result<ApprovalRequest>> => {
    return apiClient.put<ApprovalRequest>(`/approvals/${id}/request-changes`, action);
  },

  /**
   * Escalate approval to next tier
   */
  escalate: async (id: string, action?: ApprovalAction): Promise<Result<ApprovalRequest>> => {
    return apiClient.put<ApprovalRequest>(`/approvals/${id}/escalate`, action);
  },

  /**
   * Add comment to approval
   */
  addComment: async (id: string, comment: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.post<ApprovalRequest>(`/approvals/${id}/comments`, { comment });
  },

  /**
   * Reassign approval to another user
   */
  reassign: async (id: string, assigneeId: string, reason?: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.put<ApprovalRequest>(`/approvals/${id}/reassign`, {
      assigneeId,
      reason,
    });
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Approval Request Creation
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Request approval for a document
   */
  requestApproval: async (
    documentId: string,
    options?: {
      priority?: ApprovalPriority;
      comments?: string;
      dueDate?: string;
      assignees?: string[];
    }
  ): Promise<Result<ApprovalRequest>> => {
    return apiClient.post<ApprovalRequest>('/approvals/request', {
      documentId,
      ...options,
    });
  },

  /**
   * Cancel an approval request
   */
  cancelApproval: async (id: string, reason?: string): Promise<Result<void>> => {
    return apiClient.put<void>(`/approvals/${id}/cancel`, { reason });
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Bulk Operations
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Bulk approve multiple documents
   */
  bulkApprove: async (
    ids: string[],
    comment?: string
  ): Promise<Result<{ approved: number; failed: string[] }>> => {
    return apiClient.post('/approvals/bulk-approve', { ids, comment });
  },

  /**
   * Bulk reject multiple documents
   */
  bulkReject: async (
    ids: string[],
    reason: string
  ): Promise<Result<{ rejected: number; failed: string[] }>> => {
    return apiClient.post('/approvals/bulk-reject', { ids, reason });
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Priority & Due Date
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get overdue approvals
   */
  getOverdue: async (): Promise<Result<ApprovalRequest[]>> => {
    return apiClient.get<ApprovalRequest[]>('/approvals/overdue');
  },

  /**
   * Get approvals by priority
   */
  getByPriority: async (priority: ApprovalPriority): Promise<Result<ApprovalRequest[]>> => {
    return apiClient.get<ApprovalRequest[]>(`/approvals/by-priority/${priority}`);
  },

  /**
   * Update approval priority
   */
  updatePriority: async (
    id: string,
    priority: ApprovalPriority
  ): Promise<Result<ApprovalRequest>> => {
    return apiClient.patch<ApprovalRequest>(`/approvals/${id}/priority`, { priority });
  },

  /**
   * Update due date
   */
  updateDueDate: async (id: string, dueDate: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.patch<ApprovalRequest>(`/approvals/${id}/due-date`, { dueDate });
  },
};

export default approvalService;
