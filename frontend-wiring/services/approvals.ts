// =============================================
// APPROVALS SERVICE
// File: frontend/src/services/approvals.ts
// Maps to: /api/approval-workflow/* and /api/Approvals/*
// =============================================

import api from './api';
import type {
  Approval,
  ApprovalStats,
  ApproveRequest,
  RejectRequest,
  EditRequest,
  RepromptRequest,
} from '@/types/api';

export const approvalService = {
  /**
   * Get pending approvals queue
   * GET /api/Approvals/pending
   */
  getPending: async (): Promise<Approval[]> => {
    const { data } = await api.get<Approval[]>('/Approvals/pending');
    return data;
  },

  /**
   * Get all approvals (with optional status filter)
   * GET /api/approval-workflow
   */
  getAll: async (params?: { status?: string }): Promise<Approval[]> => {
    const { data } = await api.get<Approval[]>('/approval-workflow', { params });
    return data;
  },

  /**
   * Get single approval by ID
   * GET /api/approval-workflow/{id}/details
   */
  getById: async (id: number): Promise<Approval> => {
    const { data } = await api.get<Approval>(`/approval-workflow/${id}/details`);
    return data;
  },

  /**
   * Get document content/preview for approval
   * GET /api/approval-workflow/{id}/document
   */
  getDocumentContent: async (id: number): Promise<string> => {
    const { data } = await api.get<string>(`/approval-workflow/${id}/document`);
    return data;
  },

  /**
   * Approve a document
   * POST /api/approval-workflow/{id}/approve
   */
  approve: async (id: number, request: ApproveRequest): Promise<void> => {
    await api.post(`/approval-workflow/${id}/approve`, request);
  },

  /**
   * Reject a document
   * POST /api/approval-workflow/{id}/reject
   */
  reject: async (id: number, request: RejectRequest): Promise<void> => {
    await api.post(`/approval-workflow/${id}/reject`, request);
  },

  /**
   * Track edit during approval (for AI training)
   * POST /api/approval-workflow/{id}/edit
   * Creates entry in DaQa.DocumentEdits table
   */
  edit: async (id: number, request: EditRequest): Promise<void> => {
    await api.post(`/approval-workflow/${id}/edit`, request);
  },

  /**
   * Request AI regeneration with feedback
   * POST /api/approval-workflow/{id}/reprompt
   * Creates entry in DaQa.RegenerationRequests table
   */
  reprompt: async (id: number, request: RepromptRequest): Promise<void> => {
    await api.post(`/approval-workflow/${id}/reprompt`, request);
  },

  /**
   * Add suggestion without full edit
   * POST /api/approval-workflow/{id}/suggestion
   */
  addSuggestion: async (id: number, suggestion: string): Promise<void> => {
    await api.post(`/approval-workflow/${id}/suggestion`, { suggestion });
  },

  /**
   * Get approval statistics for dashboard
   * GET /api/approval-workflow/stats
   */
  getStats: async (): Promise<ApprovalStats> => {
    const { data } = await api.get<ApprovalStats>('/approval-workflow/stats');
    return data;
  },
};

export default approvalService;
