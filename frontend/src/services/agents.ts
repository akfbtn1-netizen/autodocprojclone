import { apiClient } from './apiClient';

// Agent Types
export interface Agent {
  id: string;
  name: string;
  description: string;
  status: 'running' | 'stopped' | 'error' | 'idle';
  lastRun?: string;
  nextScheduledRun?: string;
}

export interface AgentHealth {
  agentId: string;
  agentName: string;
  isHealthy: boolean;
  lastCheck: string;
  message?: string;
  uptime?: number;
}

// Agent Service
class AgentService {
  async getAgents(): Promise<Agent[]> {
    try {
      const response = await apiClient.get<Agent[]>('/agents');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch agents:', error);
      return [];
    }
  }

  async getAgentHealth(): Promise<AgentHealth[]> {
    try {
      const response = await apiClient.get<AgentHealth[]>('/agents/health');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch agent health:', error);
      return [];
    }
  }

  async startAgent(agentId: string): Promise<void> {
    await apiClient.post(`/agents/${agentId}/start`);
  }

  async stopAgent(agentId: string): Promise<void> {
    await apiClient.post(`/agents/${agentId}/stop`);
  }

  async restartAgent(agentId: string): Promise<void> {
    await apiClient.post(`/agents/${agentId}/restart`);
  }

  async getAgentLogs(agentId: string, limit = 100): Promise<string[]> {
    try {
      const response = await apiClient.get<string[]>(`/agents/${agentId}/logs?limit=${limit}`);
      return response.data;
    } catch (error) {
      console.error('Failed to fetch agent logs:', error);
      return [];
    }
  }
}

// Export singleton instance
export const agentService = new AgentService();

export default agentService;