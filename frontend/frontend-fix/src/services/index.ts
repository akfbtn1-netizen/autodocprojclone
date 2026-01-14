// ═══════════════════════════════════════════════════════════════════════════
// Services Index
// Re-exports all service modules for clean imports
// ═══════════════════════════════════════════════════════════════════════════

export { apiClient, api } from './api';
export { documentService } from './documents';
export { agentService, defaultAgents } from './agents';
export { approvalService } from './approvals';
export { dashboardService } from './dashboard';
export { lineageService } from './lineage';

// Re-export service types
export type { GetDocumentsParams, CreateDocumentRequest } from './documents';
export type { AgentStats, AgentHealthCheck, AgentCommand } from './agents';
export type { ApprovalAction, ApprovalStats, ApprovalListResponse } from './approvals';
export type { DashboardOverview, DashboardAlert, DashboardTrends, MetadataQualityReport } from './dashboard';
export type { ColumnLineage, TableLineage, ImpactAnalysis, LineageSearchResult } from './lineage';
