// ═══════════════════════════════════════════════════════════════════════════
// Pipeline React Query Hooks
// Real-time updates for end-to-end visibility
// ═══════════════════════════════════════════════════════════════════════════

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { pipelineService } from '@/services';


// ─────────────────────────────────────────────────────────────────────────────
// Query Keys
// ─────────────────────────────────────────────────────────────────────────────

export const pipelineKeys = {
  all: ['pipeline'] as const,
  status: () => [...pipelineKeys.all, 'status'] as const,
  document: (docId: string) => [...pipelineKeys.all, 'document', docId] as const,
  active: () => [...pipelineKeys.all, 'active'] as const,
  stages: () => [...pipelineKeys.all, 'stages'] as const,
};

// ─────────────────────────────────────────────────────────────────────────────
// Hooks
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Get complete pipeline status - metrics, stage counts, recent activity
 * Refreshes every 30 seconds for near-real-time visibility
 */
export function usePipelineStatus() {
  return useQuery({
    queryKey: pipelineKeys.status(),
    queryFn: async () => {
      const result = await pipelineService.getStatus();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 30000, // Refresh every 30 seconds
    staleTime: 15000,
  });
}

/**
 * Get detailed pipeline status for a single document
 * Shows all stages, history, and available actions
 */
export function useDocumentPipelineStatus(docId: string | null) {
  return useQuery({
    queryKey: pipelineKeys.document(docId || ''),
    queryFn: async () => {
      if (!docId) throw new Error('No document ID');
      const result = await pipelineService.getDocumentStatus(docId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    enabled: !!docId,
    refetchInterval: 10000, // Refresh every 10 seconds when viewing a document
  });
}

/**
 * Get all documents currently in the pipeline (not published)
 */
export function useActivePipelineItems() {
  return useQuery({
    queryKey: pipelineKeys.active(),
    queryFn: async () => {
      const result = await pipelineService.getActiveItems();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 15000, // Refresh every 15 seconds
  });
}

/**
 * Get documents grouped by stage for swimlane/kanban view
 */
export function usePipelineStages() {
  return useQuery({
    queryKey: pipelineKeys.stages(),
    queryFn: async () => {
      const result = await pipelineService.getStages();
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    refetchInterval: 30000,
  });
}

/**
 * Advance a document to the next pipeline stage
 */
export function useAdvanceDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ docId, userId }: { docId: string; userId?: string }) => {
      const result = await pipelineService.advanceDocument(docId, userId);
      if (!result.success) throw new Error(result.error.message);
      return result.data;
    },
    onSuccess: (_, variables) => {
      // Invalidate all pipeline queries to refresh the view
      queryClient.invalidateQueries({ queryKey: pipelineKeys.all });
      queryClient.invalidateQueries({ queryKey: pipelineKeys.document(variables.docId) });
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Computed Helpers
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook to get pipeline health summary
 */
export function usePipelineHealth() {
  const { data: status, isLoading, error } = usePipelineStatus();

  if (isLoading || error || !status) {
    return { isLoading, error, health: null };
  }

  const metrics = status.metrics;
  const totalActive = metrics.inStaging + metrics.inDraft + metrics.pendingApproval;
  const completionRate = metrics.totalDocuments > 0 
    ? ((metrics.published / metrics.totalDocuments) * 100).toFixed(1) 
    : '0';
  const rejectionRate = metrics.totalDocuments > 0 
    ? ((metrics.rejected / metrics.totalDocuments) * 100).toFixed(1) 
    : '0';

  // Determine health status
  let healthStatus: 'healthy' | 'warning' | 'critical' = 'healthy';
  if (metrics.pendingApproval > 10) healthStatus = 'warning';
  if (metrics.rejected > 5 || metrics.pendingApproval > 20) healthStatus = 'critical';

  return {
    isLoading: false,
    error: null,
    health: {
      status: healthStatus,
      totalDocuments: metrics.totalDocuments,
      activeInPipeline: totalActive,
      completionRate: parseFloat(completionRate),
      rejectionRate: parseFloat(rejectionRate),
      avgQuality: metrics.avgQualityScore,
      avgCompleteness: metrics.avgCompletenessScore,
      piiDocuments: metrics.piiDocuments,
      stageCounts: status.stageCounts,
      lastExcelImport: status.lastExcelImport,
    },
  };
}