import { apiClient } from './apiClient';

// Approval Types
export interface Approval {
  id: string;
  documentId: string;
  documentTitle: string;
  documentType: string;
  requestedBy: string;
  requestedAt: string;
  assignedTo?: string;
  assignedAt?: string;
  reviewedBy?: string;
  reviewedAt?: string;
  status: 'pending' | 'approved' | 'rejected' | 'cancelled';
  priority: 'low' | 'medium' | 'high' | 'urgent';
  comments?: string;
  rejectionReason?: string;
  dueDate?: string;
}

export interface ApprovalStats {
  total: number;
  pending: number;
  approved: number;
  rejected: number;
  overdue: number;
}

export interface ApprovalHistory {
  id: string;
  approvalId: string;
  action: 'created' | 'assigned' | 'approved' | 'rejected' | 'commented' | 'cancelled';
  performedBy: string;
  performedAt: string;
  comment?: string;
  oldStatus?: string;
  newStatus?: string;
}

export interface ApprovalRequest {
  documentId: string;
  assignedTo?: string;
  priority?: 'low' | 'medium' | 'high' | 'urgent';
  dueDate?: string;
  comments?: string;
}

export interface ApprovalDecision {
  approvalId: string;
  action: 'approve' | 'reject';
  comments?: string;
  rejectionReason?: string;
}

// Approval Service
class ApprovalService {
  async getApprovals(status?: string): Promise<Approval[]> {
    try {
      const url = status ? `/approvals?status=${status}` : '/approvals';
      const response = await apiClient.get<Approval[]>(url);
      return response.data;
    } catch (error) {
      console.error('Failed to fetch approvals:', error);
      return [];
    }
  }

  async getPendingApprovals(page = 1, pageSize = 20): Promise<{ items: Approval[]; total: number }> {
    try {
      const response = await apiClient.get<{ items: Approval[]; total: number }>(
        `/approvals/pending?page=${page}&pageSize=${pageSize}`
      );
      return response.data;
    } catch (error) {
      console.error('Failed to fetch pending approvals:', error);
      return { items: [], total: 0 };
    }
  }

  async getApproval(id: string): Promise<Approval | null> {
    try {
      const response = await apiClient.get<Approval>(`/approvals/${id}`);
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch approval ${id}:`, error);
      return null;
    }
  }

  async createApproval(request: ApprovalRequest): Promise<Approval> {
    const response = await apiClient.post<Approval>('/approvals', request);
    return response.data;
  }

  async approveDocument(id: string, comments?: string): Promise<void> {
    await apiClient.post(`/approvals/${id}/approve`, { comments });
  }

  async rejectDocument(id: string, reason: string, comments?: string): Promise<void> {
    await apiClient.post(`/approvals/${id}/reject`, { reason, comments });
  }

  async assignApproval(id: string, assignedTo: string): Promise<void> {
    await apiClient.post(`/approvals/${id}/assign`, { assignedTo });
  }

  async cancelApproval(id: string, reason?: string): Promise<void> {
    await apiClient.post(`/approvals/${id}/cancel`, { reason });
  }

  async addComment(id: string, comment: string): Promise<void> {
    await apiClient.post(`/approvals/${id}/comments`, { comment });
  }

  async getApprovalHistory(id: string): Promise<ApprovalHistory[]> {
    try {
      const response = await apiClient.get<ApprovalHistory[]>(`/approvals/${id}/history`);
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch approval history for ${id}:`, error);
      return [];
    }
  }

  async getApprovalStats(): Promise<ApprovalStats> {
    try {
      const response = await apiClient.get<ApprovalStats>('/approvals/stats');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch approval stats:', error);
      return {
        total: 0,
        pending: 0,
        approved: 0,
        rejected: 0,
        overdue: 0,
      };
    }
  }

  async getMyApprovals(): Promise<Approval[]> {
    try {
      const response = await apiClient.get<Approval[]>('/approvals/my');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch my approvals:', error);
      return [];
    }
  }

  async getOverdueApprovals(): Promise<Approval[]> {
    try {
      const response = await apiClient.get<Approval[]>('/approvals/overdue');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch overdue approvals:', error);
      return [];
    }
  }

  async bulkApprove(approvalIds: string[], comments?: string): Promise<void> {
    await apiClient.post('/approvals/bulk-approve', { approvalIds, comments });
  }

  async bulkReject(approvalIds: string[], reason: string): Promise<void> {
    await apiClient.post('/approvals/bulk-reject', { approvalIds, reason });
  }
}

// Export singleton instance
export const approvalService = new ApprovalService();

export default approvalService;