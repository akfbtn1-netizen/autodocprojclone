// Export API Client
export { apiClient } from './apiClient';
export { default as apiClientDefault } from './apiClient';

// Export API Service and Types
export { api } from './api';
export { default as apiDefault } from './api';
export type {
  DashboardKpi,
  Activity,
  Document,
  Approval,
} from './api';

// Export Agent Service and Types
export { agentService } from './agents';
export { default as agentServiceDefault } from './agents';
export type {
  Agent,
  AgentHealth,
} from './agents';

// Export Approval Service and Types
export { approvalService } from './approvalService';
export { default as approvalServiceDefault } from './approvalService';
export type {
  Approval,
  ApprovalStats,
  ApprovalHistory,
  ApprovalRequest,
  ApprovalDecision,
} from './approvalService';

// Export SignalR Service
export { signalRService, SignalRService } from './signalr';
export { default as signalRServiceDefault } from './signalr';

// Export Pipeline Service and Types
export { pipelineService } from './pipeline';
export { default as pipelineServiceDefault } from './pipeline';
export type {
  Pipeline,
  PipelineStage,
  PipelineRun,
  PipelineStats,
} from './pipeline';

// Export Axios types for convenience
export type { AxiosInstance, AxiosError } from 'axios';
