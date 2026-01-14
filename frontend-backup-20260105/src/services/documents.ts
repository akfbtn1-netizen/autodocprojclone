import { apiClient } from './api';
import type { Document, ApiResponse, Result, WorkflowStats, ApprovalRequest } from '@/types';

export interface DocumentFilters {
  status?: string;
  documentType?: string;
  search?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateDocumentRequest {
  title: string;
  description?: string;
  documentType: string;
  sourceObject: string;
  jiraTicket?: string;
}

export interface ApprovalActionRequest {
  documentId: string;
  comment?: string;
  reason?: string;
}

// Document API calls
export const documentService = {
  // Get all documents with optional filters
  getDocuments: async (filters?: DocumentFilters): Promise<Result<ApiResponse<Document[]>>> => {
    const params = new URLSearchParams();
    if (filters?.status) params.append('status', filters.status);
    if (filters?.documentType) params.append('documentType', filters.documentType);
    if (filters?.search) params.append('search', filters.search);
    if (filters?.dateFrom) params.append('dateFrom', filters.dateFrom);
    if (filters?.dateTo) params.append('dateTo', filters.dateTo);
    if (filters?.page) params.append('page', String(filters.page));
    if (filters?.pageSize) params.append('pageSize', String(filters.pageSize));

    return apiClient.get<ApiResponse<Document[]>>(`/documents?${params.toString()}`);
  },

  // Get single document by ID
  getDocument: async (id: string): Promise<Result<Document>> => {
    return apiClient.get<Document>(`/documents/${id}`);
  },

  // Create new document
  createDocument: async (data: CreateDocumentRequest): Promise<Result<Document>> => {
    return apiClient.post<Document>('/documents', data);
  },

  // Update document
  updateDocument: async (id: string, data: Partial<Document>): Promise<Result<Document>> => {
    return apiClient.put<Document>(`/documents/${id}`, data);
  },

  // Delete document
  deleteDocument: async (id: string): Promise<Result<void>> => {
    return apiClient.delete<void>(`/documents/${id}`);
  },

  // Request approval for a document
  requestApproval: async (documentId: string): Promise<Result<ApprovalRequest>> => {
    return apiClient.post<ApprovalRequest>(`/documents/${documentId}/request-approval`);
  },

  // Approve a document
  approveDocument: async (data: ApprovalActionRequest): Promise<Result<Document>> => {
    return apiClient.post<Document>(`/documents/${data.documentId}/approve`, {
      comment: data.comment,
    });
  },

  // Reject a document
  rejectDocument: async (data: ApprovalActionRequest): Promise<Result<Document>> => {
    return apiClient.post<Document>(`/documents/${data.documentId}/reject`, {
      reason: data.reason,
    });
  },

  // Get workflow statistics
  getWorkflowStats: async (): Promise<Result<WorkflowStats>> => {
    return apiClient.get<WorkflowStats>('/documents/stats');
  },

  // Get pending approvals for current user
  getPendingApprovals: async (): Promise<Result<ApprovalRequest[]>> => {
    return apiClient.get<ApprovalRequest[]>('/approvals/pending');
  },

  // Generate document (trigger AI enhancement)
  generateDocument: async (documentId: string): Promise<Result<Document>> => {
    return apiClient.post<Document>(`/documents/${documentId}/generate`);
  },

  // Download document
  downloadDocument: async (documentId: string): Promise<Result<Blob>> => {
    return apiClient.get<Blob>(`/documents/${documentId}/download`, {
      responseType: 'blob',
    });
  },
};

// Export for convenience
export default documentService;
