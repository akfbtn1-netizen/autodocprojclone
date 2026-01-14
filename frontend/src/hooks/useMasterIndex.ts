// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex React Query Hooks
// Server state management for MasterIndex catalog data
// ═══════════════════════════════════════════════════════════════════════════

import { useEffect, useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { masterIndexApi } from '../services/masterIndexApi';
import { useMasterIndexStore } from '../stores/masterIndexStore';
import { signalRService, SignalREvents } from '../services/signalr';
import type {
  MasterIndexSummary,
  MasterIndexDetail,
  MasterIndexStatistics,
  PaginatedResponse,
} from '../types/masterIndex';

// ─────────────────────────────────────────────────────────────────────────────
// Query Keys (for cache management)
// ─────────────────────────────────────────────────────────────────────────────

export const masterIndexKeys = {
  all: ['masterIndex'] as const,
  lists: () => [...masterIndexKeys.all, 'list'] as const,
  list: (page: number, pageSize: number) =>
    [...masterIndexKeys.lists(), { page, pageSize }] as const,
  details: () => [...masterIndexKeys.all, 'detail'] as const,
  detail: (id: number) => [...masterIndexKeys.details(), id] as const,
  statistics: () => [...masterIndexKeys.all, 'statistics'] as const,
  search: (query: string, page: number, pageSize: number) =>
    [...masterIndexKeys.all, 'search', { query, page, pageSize }] as const,
  byStatus: (status: string) =>
    [...masterIndexKeys.all, 'status', status] as const,
  byDatabase: (db: string) =>
    [...masterIndexKeys.all, 'database', db] as const,
  byTier: (tier: number) => [...masterIndexKeys.all, 'tier', tier] as const,
};

// ─────────────────────────────────────────────────────────────────────────────
// Query Hooks
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Fetches paginated list of MasterIndex entries
 */
export function useMasterIndexList(page = 1, pageSize = 20) {
  const setCachedItems = useMasterIndexStore((state) => state.setCachedItems);

  return useQuery({
    queryKey: masterIndexKeys.list(page, pageSize),
    queryFn: async () => {
      const response = await masterIndexApi.getAll(page, pageSize);
      return response.data;
    },
    staleTime: 60_000, // 1 minute
    gcTime: 300_000, // 5 minutes (formerly cacheTime)
    onSuccess: (data: PaginatedResponse<MasterIndexSummary>) => {
      setCachedItems(data.items);
    },
  });
}

/**
 * Fetches single MasterIndex entry by ID
 */
export function useMasterIndexDetail(id: number | null) {
  return useQuery({
    queryKey: masterIndexKeys.detail(id!),
    queryFn: async () => {
      const response = await masterIndexApi.getById(id!);
      return response.data;
    },
    enabled: id !== null && id > 0,
    staleTime: 60_000,
    gcTime: 300_000,
  });
}

/**
 * Fetches document statistics for dashboard
 */
export function useMasterIndexStatistics() {
  const setCachedStatistics = useMasterIndexStore(
    (state) => state.setCachedStatistics
  );

  return useQuery({
    queryKey: masterIndexKeys.statistics(),
    queryFn: async () => {
      const response = await masterIndexApi.getStatistics();
      return response.data;
    },
    staleTime: 300_000, // 5 minutes
    gcTime: 600_000, // 10 minutes
    onSuccess: (data: MasterIndexStatistics) => {
      setCachedStatistics(data);
    },
  });
}

/**
 * Searches MasterIndex entries
 * Only executes when query length >= 2
 */
export function useMasterIndexSearch(
  query: string,
  page = 1,
  pageSize = 50
) {
  const setIsSearching = useMasterIndexStore((state) => state.setIsSearching);

  return useQuery({
    queryKey: masterIndexKeys.search(query, page, pageSize),
    queryFn: async () => {
      setIsSearching(true);
      try {
        const response = await masterIndexApi.search(query, page, pageSize);
        return response.data;
      } finally {
        setIsSearching(false);
      }
    },
    enabled: query.length >= 2,
    staleTime: 60_000,
    gcTime: 300_000,
  });
}

/**
 * Fetches MasterIndex entries by approval status
 */
export function useMasterIndexByStatus(status: string | undefined) {
  return useQuery({
    queryKey: masterIndexKeys.byStatus(status!),
    queryFn: async () => {
      const response = await masterIndexApi.getByStatus(status!);
      return response.data;
    },
    enabled: !!status && status.length > 0,
    staleTime: 60_000,
  });
}

/**
 * Fetches MasterIndex entries by database name
 */
export function useMasterIndexByDatabase(databaseName: string | undefined) {
  return useQuery({
    queryKey: masterIndexKeys.byDatabase(databaseName!),
    queryFn: async () => {
      const response = await masterIndexApi.getByDatabase(databaseName!);
      return response.data;
    },
    enabled: !!databaseName && databaseName.length > 0,
    staleTime: 60_000,
  });
}

/**
 * Fetches MasterIndex entries by tier level
 */
export function useMasterIndexByTier(tier: number | undefined) {
  return useQuery({
    queryKey: masterIndexKeys.byTier(tier!),
    queryFn: async () => {
      const response = await masterIndexApi.getByTier(tier!);
      return response.data;
    },
    enabled: tier !== undefined && tier > 0,
    staleTime: 60_000,
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Cache Invalidation Helpers
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook for cache invalidation operations
 */
export function useMasterIndexInvalidation() {
  const queryClient = useQueryClient();

  return {
    /**
     * Invalidates all MasterIndex queries
     */
    invalidateAll: () => {
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.all });
    },

    /**
     * Invalidates list queries only
     */
    invalidateLists: () => {
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.lists() });
    },

    /**
     * Invalidates statistics query
     */
    invalidateStatistics: () => {
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.statistics() });
    },

    /**
     * Invalidates a specific detail query
     */
    invalidateDetail: (id: number) => {
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.detail(id) });
    },

    /**
     * Prefetches detail data for hover preview
     */
    prefetchDetail: async (id: number) => {
      await queryClient.prefetchQuery({
        queryKey: masterIndexKeys.detail(id),
        queryFn: async () => {
          const response = await masterIndexApi.getById(id);
          return response.data;
        },
        staleTime: 60_000,
      });
    },
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Combined Hook with Store Integration
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Combined hook that integrates store state with React Query
 */
export function useMasterIndexData() {
  const filters = useMasterIndexStore((state) => state.filters);
  const selectedId = useMasterIndexStore((state) => state.selectedId);
  const viewMode = useMasterIndexStore((state) => state.viewMode);

  // Determine which query to use based on filters
  const hasSearch = filters.query && filters.query.length >= 2;
  const hasStatusFilter = filters.status && filters.status.length > 0;

  // Fetch list or search results
  const listQuery = useMasterIndexList(filters.pageNumber, filters.pageSize);
  const searchQuery = useMasterIndexSearch(
    filters.query || '',
    filters.pageNumber,
    filters.pageSize
  );

  // Fetch detail if selected
  const detailQuery = useMasterIndexDetail(selectedId);

  // Fetch statistics
  const statisticsQuery = useMasterIndexStatistics();

  // Determine active data source
  const activeQuery = hasSearch ? searchQuery : listQuery;

  return {
    // Data
    items: activeQuery.data?.items ?? [],
    totalCount: activeQuery.data?.totalCount ?? 0,
    totalPages: activeQuery.data?.totalPages ?? 0,
    detail: detailQuery.data,
    statistics: statisticsQuery.data,

    // Loading states
    isLoading: activeQuery.isLoading,
    isDetailLoading: detailQuery.isLoading,
    isStatisticsLoading: statisticsQuery.isLoading,
    isFetching: activeQuery.isFetching,

    // Error states
    error: activeQuery.error,
    detailError: detailQuery.error,
    statisticsError: statisticsQuery.error,

    // Pagination info
    pageNumber: filters.pageNumber,
    pageSize: filters.pageSize,
    hasPreviousPage: activeQuery.data?.hasPreviousPage ?? false,
    hasNextPage: activeQuery.data?.hasNextPage ?? false,

    // View state
    selectedId,
    viewMode,

    // Query instances for manual refetch
    refetch: activeQuery.refetch,
    refetchDetail: detailQuery.refetch,
    refetchStatistics: statisticsQuery.refetch,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// SignalR Real-Time Hook
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook that subscribes to SignalR events for MasterIndex updates
 * Automatically invalidates relevant queries when changes occur
 */
export function useMasterIndexSignalR() {
  const queryClient = useQueryClient();

  const handleMasterIndexUpdated = useCallback(
    (data: { indexId: number }) => {
      // Invalidate the specific detail
      queryClient.invalidateQueries({
        queryKey: masterIndexKeys.detail(data.indexId),
      });
      // Invalidate lists as the item may have changed
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.lists() });
    },
    [queryClient]
  );

  const handleMasterIndexCreated = useCallback(() => {
    // Invalidate lists to show new item
    queryClient.invalidateQueries({ queryKey: masterIndexKeys.lists() });
    // Invalidate statistics
    queryClient.invalidateQueries({ queryKey: masterIndexKeys.statistics() });
  }, [queryClient]);

  const handleMasterIndexDeleted = useCallback(
    (data: { indexId: number }) => {
      // Remove from cache
      queryClient.removeQueries({
        queryKey: masterIndexKeys.detail(data.indexId),
      });
      // Invalidate lists
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.lists() });
      // Invalidate statistics
      queryClient.invalidateQueries({ queryKey: masterIndexKeys.statistics() });
    },
    [queryClient]
  );

  const handleStatisticsChanged = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: masterIndexKeys.statistics() });
  }, [queryClient]);

  useEffect(() => {
    // Subscribe to events
    signalRService.on(SignalREvents.MasterIndexUpdated, handleMasterIndexUpdated);
    signalRService.on(SignalREvents.MasterIndexCreated, handleMasterIndexCreated);
    signalRService.on(SignalREvents.MasterIndexDeleted, handleMasterIndexDeleted);
    signalRService.on(SignalREvents.StatisticsChanged, handleStatisticsChanged);

    // Cleanup on unmount
    return () => {
      signalRService.off(SignalREvents.MasterIndexUpdated, handleMasterIndexUpdated);
      signalRService.off(SignalREvents.MasterIndexCreated, handleMasterIndexCreated);
      signalRService.off(SignalREvents.MasterIndexDeleted, handleMasterIndexDeleted);
      signalRService.off(SignalREvents.StatisticsChanged, handleStatisticsChanged);
    };
  }, [
    handleMasterIndexUpdated,
    handleMasterIndexCreated,
    handleMasterIndexDeleted,
    handleStatisticsChanged,
  ]);

  return {
    isConnected: signalRService.isConnected,
    connectionState: signalRService.connectionState,
  };
}
