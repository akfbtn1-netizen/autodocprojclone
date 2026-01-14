// =============================================
// DOCUMENTS SERVICE
// File: frontend/src/services/documents.ts
// Maps to: /api/Documents/* (queries MasterIndex)
// =============================================

import api from './api';
import type { Document, PaginatedResponse } from '@/types/api';

export const documentService = {
  /**
   * Search documents with filters and pagination
   * GET /api/Documents/search
   */
  search: async (params: {
    query?: string;
    documentType?: string;
    status?: string;
    schemaName?: string;
    businessDomain?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PaginatedResponse<Document>> => {
    const { data } = await api.get<PaginatedResponse<Document>>('/Documents/search', { params });
    return data;
  },

  /**
   * Get single document by ID (from MasterIndex)
   * GET /api/Documents/{id}
   */
  getById: async (id: number | string): Promise<Document> => {
    const { data } = await api.get<Document>(`/Documents/${id}`);
    return data;
  },

  /**
   * Update document metadata
   * PUT /api/Documents/{id}
   */
  update: async (id: number | string, updates: Partial<Document>): Promise<Document> => {
    const { data } = await api.put<Document>(`/Documents/${id}`, updates);
    return data;
  },

  /**
   * Create new document entry
   * POST /api/Documents
   */
  create: async (document: Partial<Document>): Promise<Document> => {
    const { data } = await api.post<Document>('/Documents', document);
    return data;
  },
};

export default documentService;
