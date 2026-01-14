// ═══════════════════════════════════════════════════════════════════════════
// Hooks Index
// Re-exports all custom hooks
// ═══════════════════════════════════════════════════════════════════════════

// Query hooks
export {
  queryKeys,
  // Documents
  useDocuments,
  useDocument,
  useDocumentByDocId,
  useDocumentMetadata,
  useRecentDocuments,
  useDocumentSearch,
  useDocumentFacets,
  useCreateDocument,
  useUpdateDocument,
  useEnrichMetadata,
  // Agents
  useAgents,
  useAgentHealth,
  useAgentActivity,
  useAgentStats,
  useAgentCommand,
  // Approvals
  usePendingApprovals,
  useApproval,
  useApprovalStats,
  useApproveDocument,
  useRejectDocument,
  useRequestApproval,
  // Dashboard
  useDashboard,
  useDashboardKpis,
  useDashboardTrends,
  useMetadataQualityReport,
  useRecentActivity,
  // Lineage
  useTableLineage,
  useColumnLineage,
  useDocumentLineage,
  useImpactAnalysis,
} from './useQueries';

// SignalR hooks
export {
  useSignalR,
  useApprovalHub,
  useAgentHub,
  useRealTimeUpdates,
} from './useSignalR';

export type { ApprovalHubCallbacks, AgentHubCallbacks } from './useSignalR';
