// ═══════════════════════════════════════════════════════════════════════════
// Dashboard Service
// Endpoints: /api/dashboard/*
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient } from './api';
import type {
  DashboardKpis,
  ActivityItem,
  WorkflowStats,
  Result,
  DocumentType,
  BusinessDomain,
  DocumentStatus,
} from '@/types';

export interface DashboardOverview {
  kpis: DashboardKpis;
  workflowStats: WorkflowStats;
  agentSummary: {
    healthy: number;
    processing: number;
    error: number;
    total: number;
  };
  alerts: DashboardAlert[];
}

export interface DashboardAlert {
  id: string;
  type: 'warning' | 'error' | 'info';
  title: string;
  message: string;
  timestamp: string;
  actionUrl?: string;
  dismissible: boolean;
}

export interface TrendData {
  date: string;
  value: number;
}

export interface DashboardTrends {
  documentsCreated: TrendData[];
  approvalsCompleted: TrendData[];
  avgProcessingTime: TrendData[];
  qualityScore: TrendData[];
}

export interface MetadataQualityReport {
  totalDocuments: number;
  avgCompletenessScore: number;
  avgQualityScore: number;
  byBusinessDomain: {
    domain: BusinessDomain;
    count: number;
    avgCompleteness: number;
  }[];
  fieldsPopulationRate: {
    field: string;
    rate: number;
    priority: 'critical' | 'high' | 'medium' | 'low';
  }[];
  improvementSuggestions: string[];
}

export const dashboardService = {
  // ─────────────────────────────────────────────────────────────────────────
  // Overview & KPIs
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get full dashboard overview
   */
  getOverview: async (): Promise<Result<DashboardOverview>> => {
    return apiClient.get<DashboardOverview>('/dashboard/overview');
  },

  /**
   * Get KPIs only
   */
  getKpis: async (): Promise<Result<DashboardKpis>> => {
    return apiClient.get<DashboardKpis>('/dashboard/kpis');
  },

  /**
   * Get workflow statistics
   */
  getWorkflowStats: async (): Promise<Result<WorkflowStats>> => {
    return apiClient.get<WorkflowStats>('/dashboard/workflow-stats');
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Trends & Analytics
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get trend data for charts
   */
  getTrends: async (days = 30): Promise<Result<DashboardTrends>> => {
    return apiClient.get<DashboardTrends>(`/dashboard/trends?days=${days}`);
  },

  /**
   * Get document count by type over time
   */
  getDocumentTypesTrend: async (days = 30): Promise<Result<{
    dates: string[];
    series: { type: DocumentType; data: number[] }[];
  }>> => {
    return apiClient.get(`/dashboard/trends/document-types?days=${days}`);
  },

  /**
   * Get approval metrics over time
   */
  getApprovalTrend: async (days = 30): Promise<Result<{
    dates: string[];
    approved: number[];
    rejected: number[];
    pending: number[];
  }>> => {
    return apiClient.get(`/dashboard/trends/approvals?days=${days}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Metadata Quality
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get metadata quality report
   */
  getMetadataQualityReport: async (): Promise<Result<MetadataQualityReport>> => {
    return apiClient.get<MetadataQualityReport>('/dashboard/metadata-quality');
  },

  /**
   * Get documents with low quality scores
   */
  getLowQualityDocuments: async (
    threshold = 50,
    limit = 20
  ): Promise<Result<{
    docId: string;
    title: string;
    completenessScore: number;
    qualityScore: number;
    missingFields: string[];
  }[]>> => {
    return apiClient.get(`/dashboard/low-quality?threshold=${threshold}&limit=${limit}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Activity Feed
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get recent activity across the platform
   */
  getRecentActivity: async (limit = 50): Promise<Result<ActivityItem[]>> => {
    return apiClient.get<ActivityItem[]>(`/dashboard/activity?limit=${limit}`);
  },

  /**
   * Get activity for specific document
   */
  getDocumentActivity: async (docId: string): Promise<Result<ActivityItem[]>> => {
    return apiClient.get<ActivityItem[]>(`/dashboard/activity/document/${docId}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Alerts & Notifications
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get active alerts
   */
  getAlerts: async (): Promise<Result<DashboardAlert[]>> => {
    return apiClient.get<DashboardAlert[]>('/dashboard/alerts');
  },

  /**
   * Dismiss an alert
   */
  dismissAlert: async (alertId: string): Promise<Result<void>> => {
    return apiClient.delete<void>(`/dashboard/alerts/${alertId}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Distribution Charts
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get document distribution by business domain
   */
  getDistributionByDomain: async (): Promise<Result<{
    domain: string;
    count: number;
    percentage: number;
  }[]>> => {
    return apiClient.get('/dashboard/distribution/domain');
  },

  /**
   * Get document distribution by status
   */
  getDistributionByStatus: async (): Promise<Result<{
    status: DocumentStatus;
    count: number;
    percentage: number;
  }[]>> => {
    return apiClient.get('/dashboard/distribution/status');
  },

  /**
   * Get document distribution by schema
   */
  getDistributionBySchema: async (): Promise<Result<{
    schema: string;
    count: number;
    percentage: number;
  }[]>> => {
    return apiClient.get('/dashboard/distribution/schema');
  },

  /**
   * Get PII distribution
   */
  getPiiDistribution: async (): Promise<Result<{
    containsPii: number;
    noPii: number;
    piiTypes: { type: string; count: number }[];
  }>> => {
    return apiClient.get('/dashboard/distribution/pii');
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Performance Metrics
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get processing time statistics
   */
  getProcessingTimeStats: async (): Promise<Result<{
    avgHours: number;
    medianHours: number;
    p95Hours: number;
    byDocumentType: { type: DocumentType; avgHours: number }[];
    byBusinessDomain: { domain: string; avgHours: number }[];
  }>> => {
    return apiClient.get('/dashboard/metrics/processing-time');
  },

  /**
   * Get AI enhancement statistics
   */
  getAiEnhancementStats: async (): Promise<Result<{
    totalEnhanced: number;
    enhancedPercentage: number;
    avgConfidenceScore: number;
    tokensUsedToday: number;
    costToday: number;
  }>> => {
    return apiClient.get('/dashboard/metrics/ai-enhancement');
  },
};

export default dashboardService;
