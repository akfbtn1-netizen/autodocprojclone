// =============================================================================
// Agent #7: Gap Intelligence Agent - React Dashboard
// Real-time gap detection visualization with RLHF feedback
// =============================================================================

import React, { useEffect, useState, useCallback } from 'react';
import { create } from 'zustand';
import * as signalR from '@microsoft/signalr';

// Types
interface DetectedGap {
  gapId: number;
  schemaName: string;
  objectName: string;
  objectType: string;
  gapType: string;
  severity: string;
  priority: number;
  confidence: number;
  ageInDays: number;
  status: string;
}

interface GapDashboardData {
  gapsBySeverity: Record<string, number>;
  gapsByType: Record<string, number>;
  topPriorityGaps: DetectedGap[];
  coverageBySchema: { schemaName: string; totalObjects: number; documentedObjects: number; coveragePercent: number }[];
  patternEffectiveness: { patternId: number; patternName: string; precision: number; triggerCount: number; activeGaps: number }[];
  upcomingPredictions: { schemaName: string; objectName: string; predictedGapType: string; predictionConfidence: number; daysUntilGap: number }[];
  totalOpenGaps: number;
  criticalGaps: number;
  highPriorityGaps: number;
}

// Zustand Store
interface GapStore {
  dashboardData: GapDashboardData | null;
  isConnected: boolean;
  detectionInProgress: boolean;
  detectionProgress: number;
  error: string | null;
  setDashboardData: (data: GapDashboardData) => void;
  setConnected: (connected: boolean) => void;
  setDetectionProgress: (progress: number) => void;
  setDetectionInProgress: (inProgress: boolean) => void;
  setError: (error: string | null) => void;
}

const useGapStore = create<GapStore>((set) => ({
  dashboardData: null,
  isConnected: false,
  detectionInProgress: false,
  detectionProgress: 0,
  error: null,
  setDashboardData: (data) => set({ dashboardData: data }),
  setConnected: (connected) => set({ isConnected: connected }),
  setDetectionProgress: (progress) => set({ detectionProgress: progress }),
  setDetectionInProgress: (inProgress) => set({ detectionInProgress: inProgress }),
  setError: (error) => set({ error }),
}));

// Colors
const SEVERITY_COLORS: Record<string, string> = {
  CRITICAL: 'bg-red-500',
  HIGH: 'bg-orange-500',
  MEDIUM: 'bg-yellow-500',
  LOW: 'bg-green-500',
};

const SEVERITY_TEXT_COLORS: Record<string, string> = {
  CRITICAL: 'text-red-800 bg-red-100',
  HIGH: 'text-orange-800 bg-orange-100',
  MEDIUM: 'text-yellow-800 bg-yellow-100',
  LOW: 'text-green-800 bg-green-100',
};

const TYPE_COLORS: Record<string, string> = {
  STRUCTURAL: 'bg-indigo-500',
  USAGE: 'bg-purple-500',
  TEMPORAL: 'bg-sky-500',
  LINEAGE: 'bg-teal-500',
  COMPLIANCE: 'bg-red-500',
  SEMANTIC: 'bg-fuchsia-500',
};

// API Service
const API_BASE = '/api/gap-intelligence';

const gapApi = {
  getDashboard: async (): Promise<GapDashboardData> => {
    const response = await fetch(`${API_BASE}/dashboard`);
    if (!response.ok) throw new Error('Failed to fetch dashboard');
    return response.json();
  },

  runFullDetection: async (): Promise<void> => {
    const response = await fetch(`${API_BASE}/detection/full`, { method: 'POST' });
    if (!response.ok) throw new Error('Failed to start detection');
  },

  recordFeedback: async (gap: DetectedGap, feedbackType: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/gaps/feedback`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        schemaName: gap.schemaName,
        objectName: gap.objectName,
        patternId: null,
        detectedGapType: gap.gapType,
        detectedConfidence: gap.confidence,
        feedbackType,
        feedbackBy: 'dashboard-user',
      }),
    });
    if (!response.ok) throw new Error('Failed to record feedback');
  },

  refreshHeatmap: async (): Promise<void> => {
    const response = await fetch(`${API_BASE}/analysis/refresh-heatmap`, { method: 'POST' });
    if (!response.ok) throw new Error('Failed to refresh heatmap');
  },
};

// SignalR Hook
const useGapIntelligenceHub = () => {
  const { setConnected, setDashboardData, setDetectionProgress, setDetectionInProgress } = useGapStore();
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

  const fetchDashboard = useCallback(async () => {
    try {
      const data = await gapApi.getDashboard();
      setDashboardData(data);
    } catch (error) {
      console.error('Failed to fetch dashboard:', error);
    }
  }, [setDashboardData]);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/gap-intelligence')
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build();

    newConnection.on('DetectionStarted', () => {
      setDetectionInProgress(true);
      setDetectionProgress(0);
    });

    newConnection.on('DetectionProgress', (data) => {
      setDetectionProgress(data.percentComplete);
    });

    newConnection.on('DetectionCompleted', () => {
      setDetectionInProgress(false);
      fetchDashboard();
    });

    newConnection.on('NewGapDetected', () => {
      fetchDashboard();
    });

    newConnection.on('GapResolved', () => {
      fetchDashboard();
    });

    newConnection.on('FeedbackRecorded', () => {
      fetchDashboard();
    });

    newConnection.onreconnecting(() => setConnected(false));
    newConnection.onreconnected(() => {
      setConnected(true);
      fetchDashboard();
    });

    newConnection
      .start()
      .then(() => {
        setConnected(true);
        fetchDashboard();
      })
      .catch((err) => console.error('SignalR connection error:', err));

    setConnection(newConnection);
    return () => {
      newConnection.stop();
    };
  }, [fetchDashboard, setConnected, setDetectionInProgress, setDetectionProgress]);

  return { connection, fetchDashboard };
};

// Components
const SummaryCard: React.FC<{ title: string; value: number; color: string }> = ({ title, value, color }) => (
  <div className={`bg-white rounded-lg shadow p-6 border-l-4 ${color}`}>
    <p className="text-sm text-stone-500">{title}</p>
    <p className="text-3xl font-bold text-stone-900">{value}</p>
  </div>
);

const SeverityChart: React.FC<{ data: Record<string, number> }> = ({ data }) => {
  const total = Object.values(data).reduce((a, b) => a + b, 0);
  if (total === 0) return <p className="text-stone-400 text-center py-8">No gaps detected</p>;

  return (
    <div className="space-y-3">
      {Object.entries(data).map(([severity, count]) => {
        const percentage = Math.round((count / total) * 100);
        return (
          <div key={severity}>
            <div className="flex justify-between text-sm mb-1">
              <span className="font-medium text-stone-700">{severity}</span>
              <span className="text-stone-500">{count} ({percentage}%)</span>
            </div>
            <div className="w-full bg-stone-200 rounded-full h-2">
              <div
                className={`h-2 rounded-full ${SEVERITY_COLORS[severity] || 'bg-stone-400'}`}
                style={{ width: `${percentage}%` }}
              />
            </div>
          </div>
        );
      })}
    </div>
  );
};

const TypeChart: React.FC<{ data: Record<string, number> }> = ({ data }) => {
  const max = Math.max(...Object.values(data), 1);

  return (
    <div className="space-y-3">
      {Object.entries(data).map(([type, count]) => (
        <div key={type} className="flex items-center gap-3">
          <span className="w-24 text-sm text-stone-600 truncate">{type}</span>
          <div className="flex-1 bg-stone-200 rounded-full h-3">
            <div
              className={`h-3 rounded-full ${TYPE_COLORS[type] || 'bg-stone-400'}`}
              style={{ width: `${(count / max) * 100}%` }}
            />
          </div>
          <span className="w-8 text-right text-sm font-medium text-stone-700">{count}</span>
        </div>
      ))}
    </div>
  );
};

const PriorityGapList: React.FC<{
  gaps: DetectedGap[];
  onFeedback: (gap: DetectedGap, type: string) => void;
}> = ({ gaps, onFeedback }) => (
  <div className="space-y-2">
    {gaps.length === 0 ? (
      <p className="text-stone-400 text-center py-4">No priority gaps</p>
    ) : (
      gaps.map((gap) => (
        <div
          key={gap.gapId}
          className="flex items-center justify-between p-3 bg-stone-50 rounded-lg hover:bg-stone-100 transition-colors"
        >
          <div className="flex items-center gap-3">
            <span className={`px-2 py-1 rounded text-xs font-medium ${SEVERITY_TEXT_COLORS[gap.severity]}`}>
              {gap.severity}
            </span>
            <div>
              <span className="font-medium text-stone-900">{gap.schemaName}.{gap.objectName}</span>
              <span className="text-stone-500 text-sm ml-2">({gap.gapType})</span>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-stone-400 text-xs">{gap.ageInDays}d</span>
            <button
              onClick={() => onFeedback(gap, 'CONFIRMED')}
              className="p-1 text-green-600 hover:bg-green-100 rounded"
              title="Confirm gap"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </button>
            <button
              onClick={() => onFeedback(gap, 'REJECTED')}
              className="p-1 text-red-600 hover:bg-red-100 rounded"
              title="Reject gap"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      ))
    )}
  </div>
);

// Main Dashboard Component
const GapIntelligenceDashboard: React.FC = () => {
  const { dashboardData, isConnected, detectionInProgress, detectionProgress, error } = useGapStore();
  const { fetchDashboard } = useGapIntelligenceHub();
  const [isLoading, setIsLoading] = useState(false);

  const handleRunDetection = async () => {
    setIsLoading(true);
    try {
      await gapApi.runFullDetection();
    } catch (err) {
      console.error('Failed to run detection:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleFeedback = async (gap: DetectedGap, feedbackType: string) => {
    try {
      await gapApi.recordFeedback(gap, feedbackType);
      fetchDashboard();
    } catch (err) {
      console.error('Failed to record feedback:', err);
    }
  };

  const handleRefreshHeatmap = async () => {
    try {
      await gapApi.refreshHeatmap();
      fetchDashboard();
    } catch (err) {
      console.error('Failed to refresh heatmap:', err);
    }
  };

  if (!dashboardData) {
    return (
      <div className="flex items-center justify-center h-screen bg-stone-100">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-teal-600 mx-auto mb-4" />
          <p className="text-stone-500">Loading Gap Intelligence...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-stone-100 p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-stone-900">Gap Intelligence</h1>
          <p className="text-stone-500">ML-powered documentation gap detection</p>
        </div>
        <div className="flex items-center gap-4">
          {/* Connection Status */}
          <span
            className={`flex items-center px-3 py-1 rounded-full text-sm ${
              isConnected ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
            }`}
          >
            <span className={`w-2 h-2 rounded-full mr-2 ${isConnected ? 'bg-green-500' : 'bg-red-500'}`} />
            {isConnected ? 'Connected' : 'Disconnected'}
          </span>

          {/* Actions */}
          <button
            onClick={handleRefreshHeatmap}
            className="px-3 py-2 text-stone-600 hover:bg-stone-200 rounded-lg transition-colors"
            title="Refresh Heatmap"
          >
            Refresh Heatmap
          </button>
          <button
            onClick={handleRunDetection}
            disabled={detectionInProgress || isLoading}
            className="px-4 py-2 bg-teal-600 text-white rounded-lg hover:bg-teal-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {detectionInProgress ? 'Running...' : 'Run Full Detection'}
          </button>
        </div>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="mb-6 bg-red-50 border border-red-200 rounded-lg p-4 text-red-800">
          {error}
        </div>
      )}

      {/* Progress Bar */}
      {detectionInProgress && (
        <div className="mb-6 bg-white rounded-lg shadow p-4">
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm text-stone-600">Detection Progress</span>
            <span className="text-sm font-medium text-teal-600">{detectionProgress}%</span>
          </div>
          <div className="w-full bg-stone-200 rounded-full h-2">
            <div
              className="bg-teal-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${detectionProgress}%` }}
            />
          </div>
        </div>
      )}

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-6">
        <SummaryCard title="Total Open Gaps" value={dashboardData.totalOpenGaps} color="border-stone-500" />
        <SummaryCard title="Critical" value={dashboardData.criticalGaps} color="border-red-500" />
        <SummaryCard title="High Priority" value={dashboardData.highPriorityGaps} color="border-orange-500" />
        <SummaryCard title="Predictions" value={dashboardData.upcomingPredictions.length} color="border-purple-500" />
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-semibold text-stone-900 mb-4">Gaps by Severity</h3>
          <SeverityChart data={dashboardData.gapsBySeverity} />
        </div>
        <div className="bg-white rounded-lg shadow p-6">
          <h3 className="text-lg font-semibold text-stone-900 mb-4">Gaps by Type</h3>
          <TypeChart data={dashboardData.gapsByType} />
        </div>
      </div>

      {/* Priority Queue */}
      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <h3 className="text-lg font-semibold text-stone-900 mb-4">Priority Queue</h3>
        <PriorityGapList gaps={dashboardData.topPriorityGaps} onFeedback={handleFeedback} />
      </div>

      {/* Pattern Effectiveness */}
      <div className="bg-white rounded-lg shadow p-6">
        <h3 className="text-lg font-semibold text-stone-900 mb-4">Pattern Effectiveness</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-stone-500 border-b">
                <th className="pb-2 font-medium">Pattern</th>
                <th className="pb-2 font-medium">Precision</th>
                <th className="pb-2 font-medium">Triggers</th>
                <th className="pb-2 font-medium">Active Gaps</th>
              </tr>
            </thead>
            <tbody>
              {dashboardData.patternEffectiveness.map((pattern) => (
                <tr key={pattern.patternId} className="border-b border-stone-100">
                  <td className="py-3 font-medium text-stone-900">{pattern.patternName}</td>
                  <td className="py-3">
                    <span className={`px-2 py-1 rounded text-xs ${pattern.precision > 0.7 ? 'bg-green-100 text-green-800' : pattern.precision > 0.5 ? 'bg-yellow-100 text-yellow-800' : 'bg-red-100 text-red-800'}`}>
                      {(pattern.precision * 100).toFixed(0)}%
                    </span>
                  </td>
                  <td className="py-3 text-stone-600">{pattern.triggerCount}</td>
                  <td className="py-3 text-stone-600">{pattern.activeGaps}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default GapIntelligenceDashboard;
