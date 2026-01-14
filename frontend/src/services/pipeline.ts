import { apiClient } from './apiClient';

// Pipeline Types
export interface Pipeline {
  id: string;
  name: string;
  description: string;
  status: 'idle' | 'running' | 'completed' | 'failed' | 'paused';
  stages: PipelineStage[];
  createdAt: string;
  updatedAt: string;
  lastRun?: string;
}

export interface PipelineStage {
  id: string;
  name: string;
  status: 'pending' | 'running' | 'completed' | 'failed' | 'skipped';
  order: number;
  duration?: number;
  error?: string;
}

export interface PipelineRun {
  id: string;
  pipelineId: string;
  status: 'running' | 'completed' | 'failed';
  startedAt: string;
  completedAt?: string;
  duration?: number;
  stages: PipelineStage[];
  logs?: string[];
}

export interface PipelineStats {
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  averageDuration: number;
}

// Pipeline Service
class PipelineService {
  async getPipelines(): Promise<Pipeline[]> {
    try {
      const response = await apiClient.get<Pipeline[]>('/pipeline');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch pipelines:', error);
      return [];
    }
  }

  async getPipeline(id: string): Promise<Pipeline | null> {
    try {
      const response = await apiClient.get<Pipeline>(`/pipeline/${id}`);
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch pipeline ${id}:`, error);
      return null;
    }
  }

  async startPipeline(id: string, params?: Record<string, any>): Promise<PipelineRun> {
    const response = await apiClient.post<PipelineRun>(`/pipeline/${id}/start`, params);
    return response.data;
  }

  async stopPipeline(id: string): Promise<void> {
    await apiClient.post(`/pipeline/${id}/stop`);
  }

  async pausePipeline(id: string): Promise<void> {
    await apiClient.post(`/pipeline/${id}/pause`);
  }

  async resumePipeline(id: string): Promise<void> {
    await apiClient.post(`/pipeline/${id}/resume`);
  }

  async getPipelineRuns(pipelineId: string, limit = 10): Promise<PipelineRun[]> {
    try {
      const response = await apiClient.get<PipelineRun[]>(
        `/pipeline/${pipelineId}/runs?limit=${limit}`
      );
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch pipeline runs for ${pipelineId}:`, error);
      return [];
    }
  }

  async getPipelineRun(pipelineId: string, runId: string): Promise<PipelineRun | null> {
    try {
      const response = await apiClient.get<PipelineRun>(
        `/pipeline/${pipelineId}/runs/${runId}`
      );
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch pipeline run ${runId}:`, error);
      return null;
    }
  }

  async getPipelineStats(pipelineId: string): Promise<PipelineStats | null> {
    try {
      const response = await apiClient.get<PipelineStats>(`/pipeline/${pipelineId}/stats`);
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch pipeline stats for ${pipelineId}:`, error);
      return null;
    }
  }

  async retryPipelineRun(pipelineId: string, runId: string): Promise<PipelineRun> {
    const response = await apiClient.post<PipelineRun>(
      `/pipeline/${pipelineId}/runs/${runId}/retry`
    );
    return response.data;
  }

  async cancelPipelineRun(pipelineId: string, runId: string): Promise<void> {
    await apiClient.post(`/pipeline/${pipelineId}/runs/${runId}/cancel`);
  }

  async getPipelineLogs(pipelineId: string, runId: string): Promise<string[]> {
    try {
      const response = await apiClient.get<string[]>(
        `/pipeline/${pipelineId}/runs/${runId}/logs`
      );
      return response.data;
    } catch (error) {
      console.error(`Failed to fetch pipeline logs for run ${runId}:`, error);
      return [];
    }
  }
}

// Export singleton instance
export const pipelineService = new PipelineService();

export default pipelineService;
