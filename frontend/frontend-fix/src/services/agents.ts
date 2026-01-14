// ═══════════════════════════════════════════════════════════════════════════
// Agent Service
// Monitors: SchemaDetector, DocGenerator, ExcelChangeIntegrator, MetadataManager
// Endpoints: /api/agents/*
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient } from './api';
import type { Agent, AgentActivity, AgentType, AgentStatus, Result } from '@/types';

export interface AgentStats {
  totalProcessed: number;
  processedToday: number;
  errorCount: number;
  avgProcessingTimeMs: number;
  successRate: number;
  lastHourActivity: number;
}

export interface AgentHealthCheck {
  agentType: AgentType;
  isHealthy: boolean;
  lastHeartbeat: string;
  uptime: string;
  memoryUsageMb: number;
  cpuUsagePercent: number;
  queueDepth: number;
  errors: string[];
}

export interface AgentCommand {
  command: 'start' | 'stop' | 'restart' | 'pause' | 'resume';
  parameters?: Record<string, unknown>;
}

export const agentService = {
  // ─────────────────────────────────────────────────────────────────────────
  // Agent Status & Monitoring
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get status of all agents
   */
  getAllAgents: async (): Promise<Result<Agent[]>> => {
    return apiClient.get<Agent[]>('/agents');
  },

  /**
   * Get status of specific agent
   */
  getAgent: async (agentType: AgentType): Promise<Result<Agent>> => {
    return apiClient.get<Agent>(`/agents/${agentType}`);
  },

  /**
   * Get health check for all agents
   */
  getHealthChecks: async (): Promise<Result<AgentHealthCheck[]>> => {
    return apiClient.get<AgentHealthCheck[]>('/agents/health');
  },

  /**
   * Get health check for specific agent
   */
  getAgentHealth: async (agentType: AgentType): Promise<Result<AgentHealthCheck>> => {
    return apiClient.get<AgentHealthCheck>(`/agents/${agentType}/health`);
  },

  /**
   * Get statistics for all agents
   */
  getAgentStats: async (): Promise<Result<Record<AgentType, AgentStats>>> => {
    return apiClient.get<Record<AgentType, AgentStats>>('/agents/stats');
  },

  /**
   * Get statistics for specific agent
   */
  getAgentStatsByType: async (agentType: AgentType): Promise<Result<AgentStats>> => {
    return apiClient.get<AgentStats>(`/agents/${agentType}/stats`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Agent Activity & Logs
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get recent activity across all agents
   */
  getRecentActivity: async (limit = 50): Promise<Result<AgentActivity[]>> => {
    return apiClient.get<AgentActivity[]>(`/agents/activity?limit=${limit}`);
  },

  /**
   * Get activity for specific agent
   */
  getAgentActivity: async (
    agentType: AgentType,
    options?: {
      limit?: number;
      since?: string;
      success?: boolean;
    }
  ): Promise<Result<AgentActivity[]>> => {
    const params = new URLSearchParams();
    if (options?.limit) params.set('limit', options.limit.toString());
    if (options?.since) params.set('since', options.since);
    if (options?.success !== undefined) params.set('success', options.success.toString());
    
    return apiClient.get<AgentActivity[]>(`/agents/${agentType}/activity?${params.toString()}`);
  },

  /**
   * Get error logs for agent
   */
  getAgentErrors: async (
    agentType: AgentType,
    limit = 20
  ): Promise<Result<AgentActivity[]>> => {
    return apiClient.get<AgentActivity[]>(`/agents/${agentType}/errors?limit=${limit}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Agent Control
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Send command to agent
   */
  sendCommand: async (agentType: AgentType, command: AgentCommand): Promise<Result<{ success: boolean; message: string }>> => {
    return apiClient.post(`/agents/${agentType}/command`, command);
  },

  /**
   * Start agent
   */
  startAgent: async (agentType: AgentType): Promise<Result<{ success: boolean; message: string }>> => {
    return agentService.sendCommand(agentType, { command: 'start' });
  },

  /**
   * Stop agent
   */
  stopAgent: async (agentType: AgentType): Promise<Result<{ success: boolean; message: string }>> => {
    return agentService.sendCommand(agentType, { command: 'stop' });
  },

  /**
   * Restart agent
   */
  restartAgent: async (agentType: AgentType): Promise<Result<{ success: boolean; message: string }>> => {
    return agentService.sendCommand(agentType, { command: 'restart' });
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Queue Management
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get queue status for all agents
   */
  getQueueStatus: async (): Promise<Result<{
    agentType: AgentType;
    queueDepth: number;
    oldestItem?: string;
    estimatedWaitMinutes: number;
  }[]>> => {
    return apiClient.get('/agents/queues');
  },

  /**
   * Get queue items for specific agent
   */
  getQueueItems: async (agentType: AgentType, limit = 20): Promise<Result<{
    id: string;
    documentId?: string;
    status: string;
    priority: number;
    createdAt: string;
    attempts: number;
  }[]>> => {
    return apiClient.get(`/agents/${agentType}/queue?limit=${limit}`);
  },

  /**
   * Clear agent queue (admin only)
   */
  clearQueue: async (agentType: AgentType): Promise<Result<{ cleared: number }>> => {
    return apiClient.delete(`/agents/${agentType}/queue`);
  },

  /**
   * Retry failed items in queue
   */
  retryFailed: async (agentType: AgentType): Promise<Result<{ retried: number }>> => {
    return apiClient.post(`/agents/${agentType}/queue/retry`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Agent Configuration
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get agent configuration
   */
  getAgentConfig: async (agentType: AgentType): Promise<Result<Record<string, unknown>>> => {
    return apiClient.get(`/agents/${agentType}/config`);
  },

  /**
   * Update agent configuration
   */
  updateAgentConfig: async (
    agentType: AgentType,
    config: Record<string, unknown>
  ): Promise<Result<Record<string, unknown>>> => {
    return apiClient.put(`/agents/${agentType}/config`, config);
  },
};

// ─────────────────────────────────────────────────────────────────────────────
// Default Agent Data (for initial load/offline mode)
// ─────────────────────────────────────────────────────────────────────────────

export const defaultAgents: Agent[] = [
  {
    id: 'agent-schema-detector',
    name: 'Schema Detector',
    type: 'SchemaDetector',
    status: 'idle',
    description: 'Monitors database schema changes and triggers documentation updates',
    processedCount: 0,
    processedToday: 0,
    errorCount: 0,
    queueDepth: 0,
  },
  {
    id: 'agent-doc-generator',
    name: 'Document Generator',
    type: 'DocGenerator',
    status: 'idle',
    description: 'Generates Word documents using AI-enhanced templates',
    processedCount: 0,
    processedToday: 0,
    errorCount: 0,
    queueDepth: 0,
  },
  {
    id: 'agent-excel-integrator',
    name: 'Excel Change Integrator',
    type: 'ExcelChangeIntegrator',
    status: 'idle',
    description: 'Watches Excel spreadsheets for change requests and syncs to database',
    processedCount: 0,
    processedToday: 0,
    errorCount: 0,
    queueDepth: 0,
  },
  {
    id: 'agent-metadata-manager',
    name: 'Metadata Manager',
    type: 'MetadataManager',
    status: 'idle',
    description: 'Populates MasterIndex with extracted and inferred metadata',
    processedCount: 0,
    processedToday: 0,
    errorCount: 0,
    queueDepth: 0,
  },
];

export default agentService;
