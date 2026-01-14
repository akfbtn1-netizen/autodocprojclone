// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Zustand Store
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Wire SignalR events for real-time updates
// TODO [4]: Add optimistic updates for acknowledge actions

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type {
  SchemaChangeDto,
  SchemaChangeDetailDto,
  DetectionRunDto,
  SchemaSnapshotDto,
  SchemaChangeStatsDto,
  SchemaChangeFilter,
  RiskLevel,
} from '../types/schemaChange';
import { schemaChangeApi, detectionRunApi, snapshotApi, statsApi } from '../services/schemaChangeApi';

interface SchemaChangeState {
  // Data
  changes: SchemaChangeDto[];
  selectedChange: SchemaChangeDetailDto | null;
  detectionRuns: DetectionRunDto[];
  activeRun: DetectionRunDto | null;
  snapshots: SchemaSnapshotDto[];
  statistics: SchemaChangeStatsDto | null;

  // UI State
  isLoading: boolean;
  error: string | null;
  filter: SchemaChangeFilter;

  // Actions - Changes
  fetchChanges: (filter?: SchemaChangeFilter) => Promise<void>;
  fetchPendingChanges: () => Promise<void>;
  fetchChangeDetail: (changeId: string) => Promise<void>;
  acknowledgeChange: (changeId: string, notes?: string) => Promise<void>;
  triggerDocumentation: (changeId: string) => Promise<void>;
  triggerApproval: (changeId: string) => Promise<void>;

  // Actions - Detection Runs
  startDetection: (scanScope: string, schemaFilter?: string) => Promise<void>;
  fetchRecentRuns: () => Promise<void>;
  cancelDetection: (runId: string) => Promise<void>;

  // Actions - Snapshots
  createSnapshot: (snapshotType?: string, schemaFilter?: string) => Promise<void>;
  createBaseline: () => Promise<void>;
  fetchSnapshots: () => Promise<void>;

  // Actions - Statistics
  fetchStatistics: () => Promise<void>;

  // Actions - UI
  setFilter: (filter: Partial<SchemaChangeFilter>) => void;
  clearError: () => void;
  setSelectedChange: (change: SchemaChangeDetailDto | null) => void;

  // SignalR handlers
  onChangeDetected: (change: SchemaChangeDto) => void;
  onRunProgress: (runId: string, progress: number, state: string) => void;
}

export const useSchemaChangeStore = create<SchemaChangeState>()(
  devtools(
    (set, get) => ({
      // Initial state
      changes: [],
      selectedChange: null,
      detectionRuns: [],
      activeRun: null,
      snapshots: [],
      statistics: null,
      isLoading: false,
      error: null,
      filter: { page: 1, pageSize: 20 },

      // Changes
      fetchChanges: async (filter) => {
        set({ isLoading: true, error: null });
        try {
          const f = filter || get().filter;
          const changes = await schemaChangeApi.getChanges(f);
          set({ changes, isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      fetchPendingChanges: async () => {
        set({ isLoading: true, error: null });
        try {
          const changes = await schemaChangeApi.getPendingChanges();
          set({ changes, isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      fetchChangeDetail: async (changeId) => {
        set({ isLoading: true, error: null });
        try {
          const detail = await schemaChangeApi.getChangeDetail(changeId);
          set({ selectedChange: detail, isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      acknowledgeChange: async (changeId, notes) => {
        set({ isLoading: true, error: null });
        try {
          await schemaChangeApi.acknowledgeChange(changeId, 'CurrentUser', notes);
          // Refresh list
          await get().fetchChanges();
          set({ isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      triggerDocumentation: async (changeId) => {
        try {
          await schemaChangeApi.triggerDocumentation(changeId);
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      triggerApproval: async (changeId) => {
        try {
          await schemaChangeApi.triggerApproval(changeId);
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      // Detection Runs
      startDetection: async (scanScope, schemaFilter) => {
        set({ isLoading: true, error: null });
        try {
          const run = await detectionRunApi.startDetection(scanScope, schemaFilter);
          set({ activeRun: run, isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      fetchRecentRuns: async () => {
        try {
          const runs = await detectionRunApi.getRecentRuns();
          set({ detectionRuns: runs });
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      cancelDetection: async (runId) => {
        try {
          await detectionRunApi.cancelRun(runId);
          set({ activeRun: null });
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      // Snapshots
      createSnapshot: async (snapshotType, schemaFilter) => {
        set({ isLoading: true, error: null });
        try {
          await snapshotApi.createSnapshot(snapshotType, schemaFilter);
          await get().fetchSnapshots();
          set({ isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      createBaseline: async () => {
        set({ isLoading: true, error: null });
        try {
          await snapshotApi.createBaseline('CurrentUser');
          await get().fetchSnapshots();
          set({ isLoading: false });
        } catch (err) {
          set({ error: (err as Error).message, isLoading: false });
        }
      },

      fetchSnapshots: async () => {
        try {
          const snapshots = await snapshotApi.getSnapshots();
          set({ snapshots });
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      // Statistics
      fetchStatistics: async () => {
        try {
          const statistics = await statsApi.getStatistics();
          set({ statistics });
        } catch (err) {
          set({ error: (err as Error).message });
        }
      },

      // UI
      setFilter: (filter) => {
        set({ filter: { ...get().filter, ...filter } });
      },

      clearError: () => set({ error: null }),

      setSelectedChange: (change) => set({ selectedChange: change }),

      // SignalR handlers (called from SignalR service)
      onChangeDetected: (change) => {
        set((state) => ({
          changes: [change, ...state.changes],
        }));
      },

      onRunProgress: (runId, progress, state) => {
        set((s) => ({
          activeRun: s.activeRun?.runId === runId
            ? { ...s.activeRun, progressPercent: progress, currentState: state as any }
            : s.activeRun,
        }));
      },
    }),
    { name: 'schema-change-store' }
  )
);

// Selector hooks for common queries
export const useHighRiskChanges = () =>
  useSchemaChangeStore((state) =>
    state.changes.filter((c) => c.riskLevel === 'HIGH' || c.riskLevel === 'CRITICAL')
  );

export const usePendingChanges = () =>
  useSchemaChangeStore((state) =>
    state.changes.filter((c) => c.processingStatus === 'Pending')
  );

export const useChangesByRisk = (riskLevel: RiskLevel) =>
  useSchemaChangeStore((state) =>
    state.changes.filter((c) => c.riskLevel === riskLevel)
  );
