// ═══════════════════════════════════════════════════════════════════════════
// Document Service
// Endpoints: /api/documents/*
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient, api } from './api';
import type {
  Document,
  MasterIndexMetadata,
  SearchFilters,
  SearchResult,
  Result,
  PaginationMeta,
} from '@/types';

export interface GetDocumentsParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  filters?: SearchFilters;
}

export interface CreateDocumentRequest {
  jiraTicket: string;
  documentType: string;
  schemaName: string;
  tableName: string;
  columnName?: string;
  description: string;
  assignedTo?: string;
}

export interface DocumentListResponse {
  documents: Document[];
  meta: PaginationMeta;
}

export const documentService = {
  // ─────────────────────────────────────────────────────────────────────────
  // CRUD Operations
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get paginated list of documents with optional filters
   */
  getDocuments: async (params?: GetDocumentsParams): Promise<Result<DocumentListResponse>> => {
    const queryParams = new URLSearchParams();
    
    if (params?.page) queryParams.set('page', params.page.toString());
    if (params?.pageSize) queryParams.set('pageSize', params.pageSize.toString());
    if (params?.sortBy) queryParams.set('sortBy', params.sortBy);
    if (params?.sortOrder) queryParams.set('sortOrder', params.sortOrder);
    
    // Add filter params
    if (params?.filters) {
      const f = params.filters;
      if (f.query) queryParams.set('q', f.query);
      if (f.documentTypes?.length) queryParams.set('types', f.documentTypes.join(','));
      if (f.statuses?.length) queryParams.set('statuses', f.statuses.join(','));
      if (f.businessDomains?.length) queryParams.set('domains', f.businessDomains.join(','));
      if (f.schemas?.length) queryParams.set('schemas', f.schemas.join(','));
      if (f.containsPii !== undefined) queryParams.set('pii', f.containsPii.toString());
      if (f.completenessScoreMin) queryParams.set('minCompleteness', f.completenessScoreMin.toString());
    }

    const url = `/documents?${queryParams.toString()}`;
    return apiClient.get<DocumentListResponse>(url);
  },

  /**
   * Get single document by ID
   */
  getDocument: async (id: string): Promise<Result<Document>> => {
    return apiClient.get<Document>(`/documents/${id}`);
  },

  /**
   * Get document by DocId (e.g., EN-0001_gwpcDaily.irf_policy_pol_lapse_ind)
   */
  getDocumentByDocId: async (docId: string): Promise<Result<Document>> => {
    return apiClient.get<Document>(`/documents/by-docid/${encodeURIComponent(docId)}`);
  },

  /**
   * Create new document request
   */
  createDocument: async (request: CreateDocumentRequest): Promise<Result<Document>> => {
    return apiClient.post<Document>('/documents', request);
  },

  /**
   * Update document metadata
   */
  updateDocument: async (id: string, updates: Partial<MasterIndexMetadata>): Promise<Result<Document>> => {
    return apiClient.patch<Document>(`/documents/${id}`, updates);
  },

  /**
   * Delete (archive) document
   */
  deleteDocument: async (id: string): Promise<Result<void>> => {
    return apiClient.delete<void>(`/documents/${id}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Metadata Operations
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get full MasterIndex metadata for a document
   */
  getMetadata: async (docId: string): Promise<Result<MasterIndexMetadata>> => {
    return apiClient.get<MasterIndexMetadata>(`/documents/${docId}/metadata`);
  },

  /**
   * Trigger AI enrichment for a document
   */
  enrichMetadata: async (docId: string): Promise<Result<MasterIndexMetadata>> => {
    return apiClient.post<MasterIndexMetadata>(`/documents/${docId}/enrich`);
  },

  /**
   * Get metadata completeness report
   */
  getCompletenessReport: async (docId: string): Promise<Result<{
    score: number;
    populatedFields: string[];
    missingFields: string[];
    suggestions: string[];
  }>> => {
    return apiClient.get(`/documents/${docId}/completeness`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Search Operations
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Full-text search with facets
   */
  search: async (filters: SearchFilters, page = 1, pageSize = 20): Promise<Result<SearchResult>> => {
    return apiClient.post<SearchResult>('/documents/search', {
      ...filters,
      page,
      pageSize,
    });
  },

  /**
   * Get search facets for filtering UI
   */
  getFacets: async (): Promise<Result<{
    businessDomains: { value: string; count: number }[];
    semanticCategories: { value: string; count: number }[];
    schemas: { value: string; count: number }[];
    documentTypes: { value: string; count: number }[];
    complianceTags: { value: string; count: number }[];
  }>> => {
    return apiClient.get('/documents/facets');
  },

  // ─────────────────────────────────────────────────────────────────────────
  // File Operations
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Download document file
   */
  downloadDocument: async (docId: string): Promise<Blob> => {
    const response = await api.get(`/documents/${docId}/download`, {
      responseType: 'blob',
    });
    return response.data;
  },

  /**
   * Get document preview URL
   */
  getPreviewUrl: (docId: string): string => {
    return `/api/documents/${docId}/preview`;
  },

  /**
   * Get document thumbnail/preview image
   */
  getThumbnail: async (docId: string): Promise<Result<string>> => {
    return apiClient.get<string>(`/documents/${docId}/thumbnail`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Recent & Statistics
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get recent documents
   */
  getRecentDocuments: async (limit = 10): Promise<Result<Document[]>> => {
    return apiClient.get<Document[]>(`/documents/recent?limit=${limit}`);
  },

  /**
   * Get documents by status
   */
  getDocumentsByStatus: async (status: string): Promise<Result<Document[]>> => {
    return apiClient.get<Document[]>(`/documents/by-status/${status}`);
  },

  /**
   * Get documents needing attention (low quality, missing metadata)
   */
  getDocumentsNeedingAttention: async (): Promise<Result<Document[]>> => {
    return apiClient.get<Document[]>('/documents/needs-attention');
  },
};

export default documentService;
