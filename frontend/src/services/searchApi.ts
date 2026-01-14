import apiClient from './apiClient';
import type {
  SearchRequest,
  SearchResponse,
  GraphSearchResult,
  PiiFlowPath,
  GraphStats,
  LearningAnalytics,
  CategorySuggestion,
  UserInteraction,
  ExportRequest,
} from '../types/search';

/**
 * Smart Search API service
 */
export const searchApi = {
  /**
   * Execute a search query with automatic routing
   */
  search: (request: SearchRequest) =>
    apiClient.post<SearchResponse>('/search', request),

  /**
   * Get search suggestions based on partial input
   */
  getSuggestions: (query: string, maxSuggestions = 5) =>
    apiClient.get<string[]>('/search/suggestions', {
      params: { query, maxSuggestions },
    }),

  /**
   * Get follow-up suggestions for a previous search
   */
  getFollowUpSuggestions: (queryId: string) =>
    apiClient.get<import('../types/search').FollowUpSuggestion[]>(
      `/search/${queryId}/follow-ups`
    ),

  /**
   * Find objects that depend on the specified object (downstream)
   */
  getDependents: (nodeId: string, maxDepth = 3) =>
    apiClient.get<GraphSearchResult[]>(`/search/lineage/${nodeId}/dependents`, {
      params: { maxDepth },
    }),

  /**
   * Find objects that the specified object depends on (upstream)
   */
  getDependencies: (nodeId: string, maxDepth = 3) =>
    apiClient.get<GraphSearchResult[]>(
      `/search/lineage/${nodeId}/dependencies`,
      { params: { maxDepth } }
    ),

  /**
   * Find the lineage path between two objects
   */
  getLineagePath: (sourceId: string, targetId: string) =>
    apiClient.get<GraphSearchResult[]>('/search/lineage/path', {
      params: { sourceId, targetId },
    }),

  /**
   * Trace PII data flow paths from a source
   */
  tracePiiFlow: (sourceNodeId: string) =>
    apiClient.get<PiiFlowPath[]>(`/search/pii-flow/${sourceNodeId}`),

  /**
   * Get all PII flow paths
   */
  getAllPiiFlows: () => apiClient.get<PiiFlowPath[]>('/search/pii-flows'),

  /**
   * Get graph statistics
   */
  getGraphStats: () => apiClient.get<GraphStats>('/search/graph/stats'),

  /**
   * Record a user interaction for learning
   */
  recordInteraction: (interaction: UserInteraction) =>
    apiClient.post('/search/interactions', interaction),

  /**
   * Get learning analytics (admin only)
   */
  getAnalytics: (since?: string) =>
    apiClient.get<LearningAnalytics>('/search/analytics', {
      params: since ? { since } : undefined,
    }),

  /**
   * Get category suggestions (admin only)
   */
  getCategorySuggestions: (maxSuggestions = 10, minConfidence = 0.7) =>
    apiClient.get<CategorySuggestion[]>('/search/category-suggestions', {
      params: { maxSuggestions, minConfidence },
    }),

  /**
   * Export search results
   */
  exportResults: async (queryId: string, request: ExportRequest) => {
    const response = await apiClient.post(
      `/search/${queryId}/export`,
      request,
      { responseType: 'blob' }
    );
    return response;
  },

  /**
   * Trigger graph rebuild (admin only)
   */
  rebuildGraph: () => apiClient.post('/search/graph/rebuild'),
};

export default searchApi;
