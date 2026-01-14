// =============================================
// WORKFLOW SERVICE
// File: frontend/src/services/workflow.ts
// Maps to: /api/Workflow/* (queries WorkflowEvents)
// =============================================

import api from './api';
import type { WorkflowEvent, DashboardStats } from '@/types/api';

export const workflowService = {
  /**
   * Get recent workflow events (activity feed)
   * GET /api/Workflow/events?limit=N
   */
  getEvents: async (limit: number = 50): Promise<WorkflowEvent[]> => {
    const { data } = await api.get<WorkflowEvent[]>('/Workflow/events', {
      params: { limit },
    });
    return data;
  },

  /**
   * Get workflow/dashboard statistics
   * GET /api/Workflow/stats
   */
  getStats: async (): Promise<DashboardStats> => {
    const { data } = await api.get<DashboardStats>('/Workflow/stats');
    return data;
  },
};

export default workflowService;
