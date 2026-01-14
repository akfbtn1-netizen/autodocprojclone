// ═══════════════════════════════════════════════════════════════════════════
// React Query Hooks
// Data fetching hooks with caching, refetching, and real-time updates
// ═══════════════════════════════════════════════════════════════════════════

import { useQuery, useMutation, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import {
  api,
  agentService,
  approvalService,
  pipelineService,
} from '@/services';
import type {
  Document,
  Agent,
  ApprovalRequest,
  DashboardKpis,
  LineageGraph,
  SearchFilters,
  GetDocumentsParams,
  CreateDocumentRequest,
  ApprovalAction,
  MasterIndexMetadata,
} from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Query Keys (for cache management)
// ─────────────────────────────────────────────────────────────────────────────

export const queryKeys = {
  // Documents
  documents: ['documents'] as const,
  document: (id: string) => ['documents', id] as const,
  documentByDocId: (docId: string) => ['documents', 'docId', docId] as const,
  documentMetadata: (docId: string) => ['documents', docId, 'metadata'] as const,
  recentDocuments: ['documents', 'recent'] as const,
  searchResults: (filters: SearchFilters) => ['documents', 'search', filters] as const,
  facets: ['documents', 'facets'] as const,

  // Agents
  agents: ['agents'] as const,
  agent: (type: string) => ['agents', type] as const,
  agentHealth: ['agents', 'health'] as const,
  agentActivity: (type?: string) => ['agents', 'activity', type] as const,
  agentStats: ['agents', 'stats'] as const,

  // Approvals
  approvals: ['approvals'] as const,
  pendingApprovals: ['approvals', 'pending'] as const,
  approval: (id: string) => ['approvals', id] as const,
  approvalStats: ['approvals', 'stats'] as const,
  myApprovalHistory: ['approvals', 'my-history'] as const,

  // Dashboard
  dashboard: ['dashboard'] as const,
  dashboardKpis: ['dashboard', 'kpis'] as const,
  dashboardTrends: (days: number) => ['dashboard', 'trends', days] as const,
  metadataQuality: ['dashboard', 'metadata-quality'] as const,
  recentActivity: ['dashboard', 'activity'] as const,

  // Lineage
  lineage: ['lineage'] as const,
  tableLineage: (schema: string, table: string) => ['lineage', 'table', schema, table] as const,
  columnLineage: (schema: string, table: string, column: string) =>
    ['lineage', 'column', schema, table, column] as const,
  documentLineage: (docId: string) => ['lineage', 'document', docId] as const,
  impactAnalysis: (schema: string, table: string) => ['lineage', 'impact', schema, table] as const,
};

// ─────────────────────────────────────────────────────────────────────────────
// Document Hooks
// ─────────────────────────────────────────────────────────────────────────────

export function useDocuments(params?: GetDocumentsParams) {
  return useQuery({
    queryKey: [...queryKeys.documents, params],
    queryFn: async () => {
      const result = await api.documents.getAll();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    staleTime: 30 * 1000, // 30 seconds
  });
}

export function useDocument(id: string, options?: Partial<UseQueryOptions<Document>>) {
  return useQuery({
    queryKey: queryKeys.document(id),
    queryFn: async () => {
      const result = await api.documents.getById(id);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!id,
    ...options,
  });
}

export function useDocumentByDocId(docId: string) {
  return useQuery({
    queryKey: queryKeys.documentByDocId(docId),
    queryFn: async () => {
      // TODO: Implement getDocumentByDocId in api.documents
      throw new Error('Document by DocId not implemented yet');
    },
    enabled: !!docId,
  });
}

export function useDocumentMetadata(docId: string) {
  return useQuery({
    queryKey: queryKeys.documentMetadata(docId),
    queryFn: async () => {
      // TODO: Implement getMetadata in api.documents
      throw new Error('Document metadata not implemented yet');
    },
    enabled: !!docId,
  });
}

export function useRecentDocuments(limit = 10) {
  return useQuery({
    queryKey: [...queryKeys.recentDocuments, limit],
    queryFn: async () => {
      const result = await api.documents.getRecent(limit);
      return result; // api service returns data directly
    },
  });
}

export function useDocumentSearch(filters: SearchFilters, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...queryKeys.searchResults(filters), page, pageSize],
    queryFn: async () => {
      // TODO: Implement search in api.documents
      throw new Error('Document search not implemented yet');
    },
    enabled: Object.keys(filters).length > 0,
  });
}

export function useDocumentFacets() {
  return useQuery({
    queryKey: queryKeys.facets,
    queryFn: async () => {
      // TODO: Implement getFacets in api.documents
      throw new Error('Document facets not implemented yet');
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useCreateDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateDocumentRequest) => {
      // TODO: Implement createDocument in api.documents
      throw new Error('Create document not implemented yet');
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
      queryClient.invalidateQueries({ queryKey: queryKeys.recentDocuments });
    },
  });
}

export function useUpdateDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, updates }: { id: string; updates: Partial<MasterIndexMetadata> }) => {
      // TODO: Implement updateDocument in api.documents
      throw new Error('Update document not implemented yet');
    },
    onSuccess: (data, variables) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.document(variables.id) });
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
    },
  });
}

export function useEnrichMetadata() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (docId: string) => {
      // TODO: Implement enrichMetadata in api.documents
      throw new Error('Enrich metadata not implemented yet');
    },
    onSuccess: (_, docId) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.documentMetadata(docId) });
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Agent Hooks
// ─────────────────────────────────────────────────────────────────────────────

export function useAgents() {
  return useQuery({
    queryKey: queryKeys.agents,
    queryFn: async () => {
      const result = await agentService.getAgents();
      return result; // agentService returns data directly, not wrapped in result object
    },
    refetchInterval: 10 * 1000, // Refetch every 10 seconds
  });
}

export function useAgentHealth() {
  return useQuery({
    queryKey: queryKeys.agentHealth,
    queryFn: async () => {
      const result = await agentService.getAgentHealth();
      return result; // agentService returns data directly, not wrapped in result object
    },
    refetchInterval: 30 * 1000, // Every 30 seconds
  });
}

export function useAgentActivity(agentType?: string, limit = 50) {
  return useQuery({
    queryKey: [...queryKeys.agentActivity(agentType), limit],
    queryFn: async () => {
      // TODO: Implement agent activity methods
      throw new Error('Agent activity not implemented yet');
    },
    refetchInterval: 15 * 1000,
  });
}

export function useAgentStats() {
  return useQuery({
    queryKey: queryKeys.agentStats,
    queryFn: async () => {
      // TODO: Implement agent stats
      throw new Error('Agent stats not implemented yet');
    },
    staleTime: 60 * 1000,
  });
}

export function useAgentCommand() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      agentType,
      command,
    }: {
      agentType: string;
      command: 'start' | 'stop' | 'restart';
    }) => {
      // TODO: Implement agent commands
      throw new Error('Agent commands not implemented yet');
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.agents });
      queryClient.invalidateQueries({ queryKey: queryKeys.agentHealth });
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Approval Hooks
// ─────────────────────────────────────────────────────────────────────────────

export function usePendingApprovals(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...queryKeys.pendingApprovals, page, pageSize],
    queryFn: async () => {
      const result = await approvalService.getPendingApprovals(page, pageSize);
      return result; // approvalService returns data directly
    },
    refetchInterval: 30 * 1000, // Refetch every 30 seconds
  });
}

export function useApproval(id: string) {
  return useQuery({
    queryKey: queryKeys.approval(id),
    queryFn: async () => {
      const result = await approvalService.getApproval(id);
      return result; // approvalService returns data directly
    },
    enabled: !!id,
  });
}

export function useApprovalStats() {
  return useQuery({
    queryKey: queryKeys.approvalStats,
    queryFn: async () => {
      const result = await approvalService.getApprovalStats();
      return result; // approvalService returns data directly
    },
    staleTime: 60 * 1000,
  });
}

export function useApproveDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action?: ApprovalAction }) => {
      await approvalService.approveDocument(id, action?.comments);
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.approvalStats });
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
    },
  });
}

export function useRejectDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action: ApprovalAction }) => {
      await approvalService.rejectDocument(id, action.rejectionReason || 'No reason provided', action.comments);
      return { success: true };
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.approvalStats });
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
    },
  });
}

export function useRequestApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (params: {
      documentId: string;
      priority?: 'low' | 'medium' | 'high' | 'urgent';
      comments?: string;
    }) => {
      const result = await approvalService.createApproval({
        documentId: params.documentId,
        priority: params.priority,
        comments: params.comments
      });
      return result;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Dashboard Hooks
// ─────────────────────────────────────────────────────────────────────────────

export function useDashboard() {
  return useQuery({
    queryKey: queryKeys.dashboard,
    queryFn: async () => {
      const result = await api.dashboard.getKpis();
      return result; // api service returns data directly
    },
    refetchInterval: 60 * 1000, // Every minute
  });
}

export function useDashboardKpis() {
  return useQuery({
    queryKey: queryKeys.dashboardKpis,
    queryFn: async () => {
      const result = await api.dashboard.getKpis();
      return result; // api service returns data directly
    },
    refetchInterval: 60 * 1000,
  });
}

export function useDashboardTrends(days = 30) {
  return useQuery({
    queryKey: queryKeys.dashboardTrends(days),
    queryFn: async () => {
      // TODO: Implement trends in api.dashboard
      throw new Error('Dashboard trends not implemented yet');
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useMetadataQualityReport() {
  return useQuery({
    queryKey: queryKeys.metadataQuality,
    queryFn: async () => {
      // TODO: Implement metadata quality report in api.dashboard
      throw new Error('Metadata quality report not implemented yet');
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useRecentActivity(limit = 50) {
  return useQuery({
    queryKey: [...queryKeys.recentActivity, limit],
    queryFn: async () => {
      const result = await api.dashboard.getActivity(limit);
      return result; // api service returns data directly
    },
    refetchInterval: 30 * 1000,
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Lineage Hooks
// ─────────────────────────────────────────────────────────────────────────────

export function useTableLineage(
  schema: string,
  table: string,
  options?: { depth?: number; direction?: 'upstream' | 'downstream' | 'both' }
) {
  return useQuery({
    queryKey: [...queryKeys.tableLineage(schema, table), options],
    queryFn: async () => {
      // TODO: Implement lineage service
      throw new Error('Table lineage not implemented yet');
    },
    enabled: !!schema && !!table,
    staleTime: 5 * 60 * 1000,
  });
}

export function useColumnLineage(schema: string, table: string, column: string) {
  return useQuery({
    queryKey: queryKeys.columnLineage(schema, table, column),
    queryFn: async () => {
      // TODO: Implement lineage service
      throw new Error('Column lineage not implemented yet');
    },
    enabled: !!schema && !!table && !!column,
    staleTime: 5 * 60 * 1000,
  });
}

export function useDocumentLineage(docId: string) {
  return useQuery({
    queryKey: queryKeys.documentLineage(docId),
    queryFn: async () => {
      // TODO: Implement lineage service
      throw new Error('Document lineage not implemented yet');
    },
    enabled: !!docId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useImpactAnalysis(schema: string, table: string) {
  return useQuery({
    queryKey: queryKeys.impactAnalysis(schema, table),
    queryFn: async () => {
      // TODO: Implement lineage service
      throw new Error('Impact analysis not implemented yet');
    },
    enabled: !!schema && !!table,
  });
}
