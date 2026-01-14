// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex API Service
// Connects to backend MasterIndexController endpoints
// ═══════════════════════════════════════════════════════════════════════════

import apiClient from './apiClient';
import type {
  MasterIndexSummary,
  MasterIndexDetail,
  MasterIndexStatistics,
  PaginatedResponse,
} from '../types/masterIndex';

/**
 * MasterIndex API service for document catalog operations.
 * All endpoints require JWT authentication.
 */
export const masterIndexApi = {
  /**
   * Gets a paginated list of all MasterIndex entries.
   * @param page Page number (1-based, default: 1)
   * @param pageSize Items per page (default: 20, max: 100)
   */
  getAll: (page = 1, pageSize = 20) =>
    apiClient.get<PaginatedResponse<MasterIndexSummary>>('/masterindex', {
      params: { pageNumber: page, pageSize },
    }),

  /**
   * Gets a single MasterIndex entry by ID.
   * @param id The IndexId to retrieve
   */
  getById: (id: number) =>
    apiClient.get<MasterIndexDetail>(`/masterindex/${id}`),

  /**
   * Gets MasterIndex entries filtered by approval status.
   * @param status Approval status (Draft, Pending, Approved, Rejected)
   */
  getByStatus: (status: string) =>
    apiClient.get<MasterIndexSummary[]>(`/masterindex/by-status/${encodeURIComponent(status)}`),

  /**
   * Gets document statistics for dashboard display.
   */
  getStatistics: () =>
    apiClient.get<MasterIndexStatistics>('/masterindex/statistics'),

  /**
   * Searches MasterIndex entries by text query.
   * @param query Search query (searches across multiple fields)
   * @param page Page number (1-based)
   * @param pageSize Items per page (max: 100)
   */
  search: (query: string, page = 1, pageSize = 50) =>
    apiClient.get<PaginatedResponse<MasterIndexSummary>>('/masterindex/search', {
      params: { query, pageNumber: page, pageSize },
    }),

  /**
   * Gets MasterIndex entries by database name.
   * @param databaseName Database name to filter by
   */
  getByDatabase: (databaseName: string) =>
    apiClient.get<MasterIndexSummary[]>(`/masterindex/by-database/${encodeURIComponent(databaseName)}`),

  /**
   * Gets MasterIndex entries by tier level.
   * @param tier Tier level (1=Complex, 2=Standard, 3=Simple)
   */
  getByTier: (tier: number) =>
    apiClient.get<MasterIndexSummary[]>(`/masterindex/by-tier/${tier}`),
};

export default masterIndexApi;
