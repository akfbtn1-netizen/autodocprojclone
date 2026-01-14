// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex Store
// Zustand store for MasterIndex catalog state management
// ═══════════════════════════════════════════════════════════════════════════

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { MasterIndexFilters, MasterIndexSummary, MasterIndexStatistics } from '../types/masterIndex';

/**
 * Default filter state
 */
const defaultFilters: MasterIndexFilters = {
  pageNumber: 1,
  pageSize: 20,
};

/**
 * MasterIndex store state interface
 */
interface MasterIndexState {
  // Filter and pagination state
  filters: MasterIndexFilters;

  // Selection state
  selectedId: number | null;

  // View mode
  viewMode: 'list' | 'detail';

  // Cached data (for optimistic UI)
  cachedStatistics: MasterIndexStatistics | null;
  cachedItems: MasterIndexSummary[];

  // Loading states
  isSearching: boolean;

  // Actions
  setFilters: (filters: Partial<MasterIndexFilters>) => void;
  setPageNumber: (page: number) => void;
  setPageSize: (size: number) => void;
  setStatus: (status: string | undefined) => void;
  setDatabase: (database: string | undefined) => void;
  setTier: (tier: string | undefined) => void;
  setQuery: (query: string | undefined) => void;
  setSelectedId: (id: number | null) => void;
  setViewMode: (mode: 'list' | 'detail') => void;
  setCachedStatistics: (stats: MasterIndexStatistics | null) => void;
  setCachedItems: (items: MasterIndexSummary[]) => void;
  setIsSearching: (searching: boolean) => void;
  resetFilters: () => void;
  clearSelection: () => void;
}

/**
 * MasterIndex Zustand store with persistence
 */
export const useMasterIndexStore = create<MasterIndexState>()(
  persist(
    (set) => ({
      // Initial state
      filters: defaultFilters,
      selectedId: null,
      viewMode: 'list',
      cachedStatistics: null,
      cachedItems: [],
      isSearching: false,

      // Filter actions
      setFilters: (newFilters) =>
        set((state) => ({
          filters: { ...state.filters, ...newFilters },
        })),

      setPageNumber: (page) =>
        set((state) => ({
          filters: { ...state.filters, pageNumber: page },
        })),

      setPageSize: (size) =>
        set((state) => ({
          filters: { ...state.filters, pageSize: size, pageNumber: 1 },
        })),

      setStatus: (status) =>
        set((state) => ({
          filters: { ...state.filters, status, pageNumber: 1 },
        })),

      setDatabase: (database) =>
        set((state) => ({
          filters: { ...state.filters, database, pageNumber: 1 },
        })),

      setTier: (tier) =>
        set((state) => ({
          filters: { ...state.filters, tier, pageNumber: 1 },
        })),

      setQuery: (query) =>
        set((state) => ({
          filters: { ...state.filters, query, pageNumber: 1 },
        })),

      // Selection actions
      setSelectedId: (id) => set({ selectedId: id }),

      // View mode actions
      setViewMode: (mode) => set({ viewMode: mode }),

      // Cache actions
      setCachedStatistics: (stats) => set({ cachedStatistics: stats }),
      setCachedItems: (items) => set({ cachedItems: items }),

      // Loading actions
      setIsSearching: (searching) => set({ isSearching: searching }),

      // Reset actions
      resetFilters: () =>
        set({
          filters: defaultFilters,
          selectedId: null,
        }),

      clearSelection: () => set({ selectedId: null, viewMode: 'list' }),
    }),
    {
      name: 'master-index-store',
      // Only persist view preferences, not data
      partialize: (state) => ({
        filters: {
          pageSize: state.filters.pageSize,
        },
        viewMode: state.viewMode,
      }),
    }
  )
);

export type { MasterIndexState };
