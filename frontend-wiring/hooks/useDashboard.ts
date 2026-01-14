// =============================================
// DASHBOARD HOOKS
// File: frontend/src/hooks/useDashboard.ts
// React Query hooks for dashboard data
// =============================================

import { useQuery } from '@tanstack/react-query';
import { workflowService } from '@/services/workflow';
import { pipelineService } from '@/services/pipeline';
import { approvalService } from '@/services/approvals';

// =============================================
// QUERY KEYS
// =============================================

export const dashboardKeys = {
  stats: ['dashboard', 'stats'] as const,
  pipeline: ['dashboard', 'pipeline'] as const,
  stages: ['dashboard', 'stages'] as const,
  events: (limit: number) => ['dashboard', 'events', limit] as const,
  approvalStats: ['dashboard', 'approvalStats'] as const,
};

// =============================================
// QUERIES
// =============================================

/**
 * Get main dashboard statistics
 * Auto-refreshes every 30 seconds
 */
export function useDashboardStats() {
  return useQuery({
    queryKey: dashboardKeys.stats,
    queryFn: workflowService.getStats,
    refetchInterval: 30000,
  });
}

/**
 * Get pipeline status counts
 * Auto-refreshes every 15 seconds
 */
export function usePipelineStatus() {
  return useQuery({
    queryKey: dashboardKeys.pipeline,
    queryFn: pipelineService.getStatus,
    refetchInterval: 15000,
  });
}

/**
 * Get pipeline stages with status
 * For workflow visualization
 */
export function usePipelineStages() {
  return useQuery({
    queryKey: dashboardKeys.stages,
    queryFn: pipelineService.getStages,
    refetchInterval: 10000,
  });
}

/**
 * Get active pipeline items
 */
export function usePipelineActive() {
  return useQuery({
    queryKey: ['pipeline', 'active'],
    queryFn: pipelineService.getActiveItems,
    refetchInterval: 10000,
  });
}

/**
 * Get recent workflow events for activity feed
 * Auto-refreshes every 10 seconds
 */
export function useRecentEvents(limit: number = 20) {
  return useQuery({
    queryKey: dashboardKeys.events(limit),
    queryFn: () => workflowService.getEvents(limit),
    refetchInterval: 10000,
  });
}

/**
 * Get approval statistics for dashboard
 */
export function useApprovalDashboardStats() {
  return useQuery({
    queryKey: dashboardKeys.approvalStats,
    queryFn: approvalService.getStats,
    refetchInterval: 60000,
  });
}

// =============================================
// COMBINED DASHBOARD HOOK
// =============================================

/**
 * Combined hook for all dashboard data
 * Returns all data needed for the main dashboard
 */
export function useDashboardData() {
  const stats = useDashboardStats();
  const pipeline = usePipelineStatus();
  const stages = usePipelineStages();
  const events = useRecentEvents(15);
  const approvalStats = useApprovalDashboardStats();

  const isLoading = stats.isLoading || pipeline.isLoading || stages.isLoading;
  const isError = stats.isError || pipeline.isError || stages.isError;

  const refetch = () => {
    stats.refetch();
    pipeline.refetch();
    stages.refetch();
    events.refetch();
    approvalStats.refetch();
  };

  return {
    // Data
    stats: stats.data,
    pipeline: pipeline.data,
    stages: stages.data,
    events: events.data,
    approvalStats: approvalStats.data,
    
    // Status
    isLoading,
    isError,
    error: stats.error || pipeline.error || stages.error,
    
    // Actions
    refetch,
  };
}
