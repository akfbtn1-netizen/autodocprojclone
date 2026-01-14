// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - API Service
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Add error handling with toast notifications
// TODO [4]: Add retry logic for transient failures

import type {
  SchemaChangeDto,
  SchemaChangeDetailDto,
  DetectionRunDto,
  SchemaSnapshotDto,
  SchemaChangeStatsDto,
  SchemaChangeFilter,
} from '../types/schemaChange';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5195';

async function fetchWithAuth<T>(url: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem('auth_token');
  const response = await fetch(`${API_BASE}${url}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  });

  if (!response.ok) {
    const error = await response.text();
    throw new Error(error || `HTTP ${response.status}`);
  }

  return response.json();
}

// Schema Changes
export const schemaChangeApi = {
  // Get changes with filtering
  getChanges: (filter: SchemaChangeFilter = {}) => {
    const params = new URLSearchParams();
    Object.entries(filter).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params.append(key, String(value));
      }
    });
    return fetchWithAuth<SchemaChangeDto[]>(`/api/schema-changes?${params}`);
  },

  // Get pending changes
  getPendingChanges: (maxCount = 100) =>
    fetchWithAuth<SchemaChangeDto[]>(`/api/schema-changes/pending?maxCount=${maxCount}`),

  // Get change detail
  getChangeDetail: (changeId: string) =>
    fetchWithAuth<SchemaChangeDetailDto>(`/api/schema-changes/${changeId}`),

  // Acknowledge change
  acknowledgeChange: (changeId: string, acknowledgedBy: string, notes?: string) =>
    fetchWithAuth<void>(`/api/schema-changes/${changeId}/acknowledge`, {
      method: 'POST',
      body: JSON.stringify({ acknowledgedBy, notes }),
    }),

  // Trigger documentation
  triggerDocumentation: (changeId: string) =>
    fetchWithAuth<void>(`/api/schema-changes/${changeId}/trigger-documentation`, {
      method: 'POST',
    }),

  // Trigger approval
  triggerApproval: (changeId: string) =>
    fetchWithAuth<{ approvalWorkflowId: string }>(`/api/schema-changes/${changeId}/trigger-approval`, {
      method: 'POST',
    }),

  // Get impacts
  getImpacts: (changeId: string) =>
    fetchWithAuth<any[]>(`/api/schema-changes/${changeId}/impacts`),

  // Find dependencies
  findDependencies: (schemaName: string, objectName: string, columnName?: string) => {
    const params = new URLSearchParams({ schemaName, objectName });
    if (columnName) params.append('columnName', columnName);
    return fetchWithAuth<string[]>(`/api/schema-changes/dependencies?${params}`);
  },
};

// Detection Runs
export const detectionRunApi = {
  // Start detection
  startDetection: (scanScope: string, schemaFilter?: string, triggeredBy = 'User') =>
    fetchWithAuth<DetectionRunDto>('/api/schema-changes/runs', {
      method: 'POST',
      body: JSON.stringify({ scanScope, schemaFilter, triggeredBy }),
    }),

  // Get run status
  getRunStatus: (runId: string) =>
    fetchWithAuth<DetectionRunDto>(`/api/schema-changes/runs/${runId}`),

  // Get recent runs
  getRecentRuns: (count = 10) =>
    fetchWithAuth<DetectionRunDto[]>(`/api/schema-changes/runs?count=${count}`),

  // Cancel run
  cancelRun: (runId: string) =>
    fetchWithAuth<void>(`/api/schema-changes/runs/${runId}/cancel`, {
      method: 'POST',
    }),
};

// Snapshots
export const snapshotApi = {
  // Create snapshot
  createSnapshot: (snapshotType = 'FULL', schemaFilter?: string) => {
    const params = new URLSearchParams({ snapshotType });
    if (schemaFilter) params.append('schemaFilter', schemaFilter);
    return fetchWithAuth<SchemaSnapshotDto>(`/api/schema-changes/snapshots?${params}`, {
      method: 'POST',
    });
  },

  // Create baseline
  createBaseline: (createdBy: string, schemaFilter?: string) =>
    fetchWithAuth<SchemaSnapshotDto>('/api/schema-changes/snapshots/baseline', {
      method: 'POST',
      body: JSON.stringify({ createdBy, schemaFilter }),
    }),

  // Get snapshots
  getSnapshots: (count = 20) =>
    fetchWithAuth<SchemaSnapshotDto[]>(`/api/schema-changes/snapshots?count=${count}`),

  // Get latest baseline
  getLatestBaseline: () =>
    fetchWithAuth<SchemaSnapshotDto>('/api/schema-changes/snapshots/baseline'),
};

// Statistics
export const statsApi = {
  getStatistics: () =>
    fetchWithAuth<SchemaChangeStatsDto>('/api/schema-changes/statistics'),
};
