// ═══════════════════════════════════════════════════════════════════════════
// React Query Hooks
// Data fetching hooks with caching, refetching, and real-time updates
// ═══════════════════════════════════════════════════════════════════════════

import { useQuery, useMutation, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import {
  documentService,
  agentService,
  approvalService,
  dashboardService,
  lineageService,
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
      const result = await documentService.getDocuments(params);
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
      const result = await documentService.getDocument(id);
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
      const result = await documentService.getDocumentByDocId(docId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!docId,
  });
}

export function useDocumentMetadata(docId: string) {
  return useQuery({
    queryKey: queryKeys.documentMetadata(docId),
    queryFn: async () => {
      const result = await documentService.getMetadata(docId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!docId,
  });
}

export function useRecentDocuments(limit = 10) {
  return useQuery({
    queryKey: [...queryKeys.recentDocuments, limit],
    queryFn: async () => {
      const result = await documentService.getRecentDocuments(limit);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
  });
}

export function useDocumentSearch(filters: SearchFilters, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: [...queryKeys.searchResults(filters), page, pageSize],
    queryFn: async () => {
      const result = await documentService.search(filters, page, pageSize);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: Object.keys(filters).length > 0,
  });
}

export function useDocumentFacets() {
  return useQuery({
    queryKey: queryKeys.facets,
    queryFn: async () => {
      const result = await documentService.getFacets();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

export function useCreateDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateDocumentRequest) => {
      const result = await documentService.createDocument(request);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await documentService.updateDocument(id, updates);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await documentService.enrichMetadata(docId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await agentService.getAllAgents();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 10 * 1000, // Refetch every 10 seconds
  });
}

export function useAgentHealth() {
  return useQuery({
    queryKey: queryKeys.agentHealth,
    queryFn: async () => {
      const result = await agentService.getHealthChecks();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 30 * 1000, // Every 30 seconds
  });
}

export function useAgentActivity(agentType?: string, limit = 50) {
  return useQuery({
    queryKey: [...queryKeys.agentActivity(agentType), limit],
    queryFn: async () => {
      const result = agentType
        ? await agentService.getAgentActivity(agentType as any, { limit })
        : await agentService.getRecentActivity(limit);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 15 * 1000,
  });
}

export function useAgentStats() {
  return useQuery({
    queryKey: queryKeys.agentStats,
    queryFn: async () => {
      const result = await agentService.getAgentStats();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await agentService.sendCommand(agentType as any, { command });
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 30 * 1000, // Refetch every 30 seconds
  });
}

export function useApproval(id: string) {
  return useQuery({
    queryKey: queryKeys.approval(id),
    queryFn: async () => {
      const result = await approvalService.getApproval(id);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!id,
  });
}

export function useApprovalStats() {
  return useQuery({
    queryKey: queryKeys.approvalStats,
    queryFn: async () => {
      const result = await approvalService.getStats();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    staleTime: 60 * 1000,
  });
}

export function useApproveDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, action }: { id: string; action?: ApprovalAction }) => {
      const result = await approvalService.approve(id, action);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await approvalService.reject(id, action);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await approvalService.requestApproval(params.documentId, params);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await dashboardService.getOverview();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 60 * 1000, // Every minute
  });
}

export function useDashboardKpis() {
  return useQuery({
    queryKey: queryKeys.dashboardKpis,
    queryFn: async () => {
      const result = await dashboardService.getKpis();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 60 * 1000,
  });
}

export function useDashboardTrends(days = 30) {
  return useQuery({
    queryKey: queryKeys.dashboardTrends(days),
    queryFn: async () => {
      const result = await dashboardService.getTrends(days);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useMetadataQualityReport() {
  return useQuery({
    queryKey: queryKeys.metadataQuality,
    queryFn: async () => {
      const result = await dashboardService.getMetadataQualityReport();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useRecentActivity(limit = 50) {
  return useQuery({
    queryKey: [...queryKeys.recentActivity, limit],
    queryFn: async () => {
      const result = await dashboardService.getRecentActivity(limit);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
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
      const result = await lineageService.getTableLineage(schema, table, options);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!schema && !!table,
    staleTime: 5 * 60 * 1000,
  });
}

export function useColumnLineage(schema: string, table: string, column: string) {
  return useQuery({
    queryKey: queryKeys.columnLineage(schema, table, column),
    queryFn: async () => {
      const result = await lineageService.getColumnLineage(schema, table, column);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!schema && !!table && !!column,
    staleTime: 5 * 60 * 1000,
  });
}

export function useDocumentLineage(docId: string) {
  return useQuery({
    queryKey: queryKeys.documentLineage(docId),
    queryFn: async () => {
      const result = await lineageService.getDocumentLineage(docId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!docId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useImpactAnalysis(schema: string, table: string) {
  return useQuery({
    queryKey: queryKeys.impactAnalysis(schema, table),
    queryFn: async () => {
      const result = await lineageService.analyzeTableImpact(schema, table);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!schema && !!table,
  });
}
