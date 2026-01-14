// ═══════════════════════════════════════════════════════════════════════════
// Approval Workflow API Service
// Connects to backend endpoints for 17-table approval workflow
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient } from './apiClient';
import type {
  Approval,
  ApprovalStats,
  ApprovalHistory,
  ApprovalFilters,
  RegenerationRequest,
  DocumentEdit,
  WorkflowEvent,
  ApproveRequest,
  RejectRequest,
  Notification,
} from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Approval API Client
// ─────────────────────────────────────────────────────────────────────────────

export const approvalApi = {
  // ===== GET APPROVALS =====

  /**
   * Get all approvals with optional filters
   */
  async getApprovals(filters?: ApprovalFilters): Promise<Approval[]> {
    const params = new URLSearchParams();

    if (filters?.status) {
      filters.status.forEach((s) => params.append('status', s));
    }
    if (filters?.documentType) {
      filters.documentType.forEach((t) => params.append('documentType', t));
    }
    if (filters?.priority) {
      filters.priority.forEach((p) => params.append('priority', p));
    }
    if (filters?.assignedTo) {
      params.append('assignedTo', filters.assignedTo);
    }
    if (filters?.searchQuery) {
      params.append('search', filters.searchQuery);
    }
    if (filters?.dateRange) {
      params.append('startDate', filters.dateRange.start);
      params.append('endDate', filters.dateRange.end);
    }

    const response = await apiClient.get<Approval[]>('/approvals', { params });
    return response.data;
  },

  /**
   * Get single approval by ID
   */
  async getApproval(id: number): Promise<Approval> {
    const response = await apiClient.get<Approval>(`/approvals/${id}`);
    return response.data;
  },

  /**
   * Get approvals pending for current user
   */
  async getPendingApprovals(): Promise<Approval[]> {
    const response = await apiClient.get<Approval[]>('/approvals/pending');
    return response.data;
  },

  /**
   * Get approval statistics
   */
  async getApprovalStats(): Promise<ApprovalStats> {
    const response = await apiClient.get<ApprovalStats>('/approvals/stats');
    return response.data;
  },

  // ===== APPROVAL ACTIONS =====

  /**
   * Approve a document
   */
  async approveDocument(id: number, request: ApproveRequest): Promise<void> {
    await apiClient.post(`/approvals/${id}/approve`, request);
  },

  /**
   * Reject a document
   */
  async rejectDocument(id: number, request: RejectRequest): Promise<void> {
    await apiClient.post(`/approvals/${id}/reject`, request);
  },

  /**
   * Request re-prompt (regeneration) with feedback
   */
  async requestRegeneration(request: RegenerationRequest): Promise<number> {
    const response = await apiClient.post<{ newApprovalId: number }>(
      `/approvals/${request.approvalId}/regenerate`,
      request
    );
    return response.data.newApprovalId;
  },

  /**
   * Save inline edits
   */
  async saveEdits(
    approvalId: number,
    edits: Omit<DocumentEdit, 'id' | 'editedAt'>[]
  ): Promise<void> {
    await apiClient.post(`/approvals/${approvalId}/edits`, { edits });
  },

  /**
   * Submit quality rating and feedback
   */
  async submitFeedback(
    approvalId: number,
    data: { qualityRating: number; feedbackText?: string }
  ): Promise<void> {
    await apiClient.post(`/approvals/${approvalId}/feedback`, data);
  },

  // ===== HISTORY & EVENTS =====

  /**
   * Get approval history
   */
  async getApprovalHistory(documentId: string): Promise<ApprovalHistory[]> {
    const response = await apiClient.get<ApprovalHistory[]>(
      `/approvals/${documentId}/history`
    );
    return response.data;
  },

  /**
   * Get workflow events (event sourcing)
   */
  async getWorkflowEvents(documentId: string): Promise<WorkflowEvent[]> {
    const response = await apiClient.get<WorkflowEvent[]>(
      `/approvals/${documentId}/events`
    );
    return response.data;
  },

  /**
   * Get document edits
   */
  async getDocumentEdits(approvalId: number): Promise<DocumentEdit[]> {
    const response = await apiClient.get<DocumentEdit[]>(
      `/approvals/${approvalId}/edits`
    );
    return response.data;
  },

  // ===== DOCUMENT CONTENT =====

  /**
   * Get document content for inline editing
   */
  async getDocumentContent(
    approvalId: number
  ): Promise<{ sections: { name: string; content: string }[] }> {
    const response = await apiClient.get<{
      sections: { name: string; content: string }[];
    }>(`/approvals/${approvalId}/content`);
    return response.data;
  },

  /**
   * Get document preview URL (HTML or PDF)
   */
  getDocumentPreviewUrl(filePath: string): string {
    const baseUrl = apiClient.defaults.baseURL || '';
    return `${baseUrl}/documents/preview?path=${encodeURIComponent(filePath)}`;
  },

  /**
   * Get document download URL
   */
  getDocumentDownloadUrl(filePath: string): string {
    const baseUrl = apiClient.defaults.baseURL || '';
    return `${baseUrl}/documents/download?path=${encodeURIComponent(filePath)}`;
  },

  // ===== NOTIFICATIONS =====

  /**
   * Get user notifications
   */
  async getNotifications(): Promise<Notification[]> {
    const response = await apiClient.get<Notification[]>('/notifications');
    return response.data;
  },

  /**
   * Mark notification as read
   */
  async markNotificationRead(notificationId: number): Promise<void> {
    await apiClient.post(`/notifications/${notificationId}/read`);
  },

  /**
   * Get unread notification count
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiClient.get<{ count: number }>(
      '/notifications/unread-count'
    );
    return response.data.count;
  },

  /**
   * Mark all notifications as read
   */
  async markAllNotificationsRead(): Promise<void> {
    await apiClient.post('/notifications/read-all');
  },

  // ===== BULK OPERATIONS =====

  /**
   * Bulk approve multiple documents
   */
  async bulkApprove(
    approvalIds: number[],
    comments?: string
  ): Promise<{ succeeded: number[]; failed: number[] }> {
    const response = await apiClient.post<{
      succeeded: number[];
      failed: number[];
    }>('/approvals/bulk-approve', { approvalIds, comments });
    return response.data;
  },

  /**
   * Bulk reject multiple documents
   */
  async bulkReject(
    approvalIds: number[],
    reason: string
  ): Promise<{ succeeded: number[]; failed: number[] }> {
    const response = await apiClient.post<{
      succeeded: number[];
      failed: number[];
    }>('/approvals/bulk-reject', { approvalIds, reason });
    return response.data;
  },

  // ===== ASSIGNMENT =====

  /**
   * Assign approval to a user
   */
  async assignApproval(approvalId: number, assignedTo: string): Promise<void> {
    await apiClient.post(`/approvals/${approvalId}/assign`, { assignedTo });
  },

  /**
   * Reassign approval to a different user
   */
  async reassignApproval(
    approvalId: number,
    assignedTo: string,
    reason?: string
  ): Promise<void> {
    await apiClient.post(`/approvals/${approvalId}/reassign`, {
      assignedTo,
      reason,
    });
  },

  // ===== ESCALATION =====

  /**
   * Escalate an overdue approval
   */
  async escalateApproval(approvalId: number, message?: string): Promise<void> {
    await apiClient.post(`/approvals/${approvalId}/escalate`, { message });
  },

  /**
   * Get overdue approvals
   */
  async getOverdueApprovals(): Promise<Approval[]> {
    const response = await apiClient.get<Approval[]>('/approvals/overdue');
    return response.data;
  },

  // ===== SEARCH =====

  /**
   * Search approvals
   */
  async searchApprovals(
    query: string,
    page = 1,
    pageSize = 20
  ): Promise<{ items: Approval[]; total: number }> {
    const response = await apiClient.get<{ items: Approval[]; total: number }>(
      '/approvals/search',
      {
        params: { query, page, pageSize },
      }
    );
    return response.data;
  },
};

export default approvalApi;
