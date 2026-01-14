// =============================================
// PIPELINE SERVICE
// File: frontend/src/services/pipeline.ts
// Maps to: /api/Pipeline/* (queries DocumentationQueue)
// =============================================

import api from './api';
import type { PipelineStatus, PipelineStage, PipelineItem } from '@/types/api';

export const pipelineService = {
  /**
   * Get overall pipeline status counts
   * GET /api/Pipeline/status
   */
  getStatus: async (): Promise<PipelineStatus> => {
    const { data } = await api.get<PipelineStatus>('/Pipeline/status');
    return data;
  },

  /**
   * Get pipeline stages with status
   * GET /api/Pipeline/stages
   */
  getStages: async (): Promise<PipelineStage[]> => {
    const { data } = await api.get<PipelineStage[]>('/Pipeline/stages');
    return data;
  },

  /**
   * Get active pipeline items
   * GET /api/Pipeline/active
   */
  getActiveItems: async (): Promise<PipelineItem[]> => {
    const { data } = await api.get<PipelineItem[]>('/Pipeline/active');
    return data;
  },
};

export default pipelineService;
