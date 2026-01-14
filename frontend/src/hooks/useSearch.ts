import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { searchApi } from '../services/searchApi';
import type {
  SearchRequest,
  UserInteraction,
  ExportRequest,
} from '../types/search';
import { useSearchStore } from '../stores/searchStore';

// Query keys
export const searchKeys = {
  all: ['search'] as const,
  suggestions: (query: string) =>
    [...searchKeys.all, 'suggestions', query] as const,
  followUps: (queryId: string) =>
    [...searchKeys.all, 'followUps', queryId] as const,
  dependents: (nodeId: string, depth: number) =>
    [...searchKeys.all, 'dependents', nodeId, depth] as const,
  dependencies: (nodeId: string, depth: number) =>
    [...searchKeys.all, 'dependencies', nodeId, depth] as const,
  lineagePath: (sourceId: string, targetId: string) =>
    [...searchKeys.all, 'lineagePath', sourceId, targetId] as const,
  piiFlow: (nodeId: string) => [...searchKeys.all, 'piiFlow', nodeId] as const,
  allPiiFlows: () => [...searchKeys.all, 'allPiiFlows'] as const,
  graphStats: () => [...searchKeys.all, 'graphStats'] as const,
  analytics: (since?: string) => [...searchKeys.all, 'analytics', since] as const,
  categorySuggestions: () => [...searchKeys.all, 'categorySuggestions'] as const,
};

/**
 * Hook for executing searches
 */
export function useSearchMutation() {
  const queryClient = useQueryClient();
  const store = useSearchStore();

  return useMutation({
    mutationFn: (request: SearchRequest) => searchApi.search(request),
    onMutate: () => {
      store.setIsLoading(true);
      store.setError(null);
    },
    onSuccess: (response) => {
      const data = response.data;
      store.setResults(data.results);
      store.setCurrentQueryId(data.queryId);
      store.setRoutingPath(data.routingPath);
      store.setFollowUpSuggestions(data.followUpSuggestions);
      store.setMetadata(data.metadata);
      store.setIsLoading(false);
    },
    onError: (error: Error) => {
      store.setError(error.message);
      store.setIsLoading(false);
    },
  });
}

/**
 * Hook for search suggestions (autocomplete)
 */
export function useSearchSuggestions(query: string, enabled = true) {
  return useQuery({
    queryKey: searchKeys.suggestions(query),
    queryFn: () => searchApi.getSuggestions(query).then((r) => r.data),
    enabled: enabled && query.length >= 2,
    staleTime: 60_000,
  });
}

/**
 * Hook for follow-up suggestions
 */
export function useFollowUpSuggestions(queryId: string | null) {
  return useQuery({
    queryKey: searchKeys.followUps(queryId ?? ''),
    queryFn: () =>
      searchApi.getFollowUpSuggestions(queryId!).then((r) => r.data),
    enabled: queryId !== null,
    staleTime: 60_000,
  });
}

/**
 * Hook for getting downstream dependents
 */
export function useDependents(nodeId: string | null, maxDepth = 3) {
  return useQuery({
    queryKey: searchKeys.dependents(nodeId ?? '', maxDepth),
    queryFn: () => searchApi.getDependents(nodeId!, maxDepth).then((r) => r.data),
    enabled: nodeId !== null,
    staleTime: 60_000,
  });
}

/**
 * Hook for getting upstream dependencies
 */
export function useDependencies(nodeId: string | null, maxDepth = 3) {
  return useQuery({
    queryKey: searchKeys.dependencies(nodeId ?? '', maxDepth),
    queryFn: () =>
      searchApi.getDependencies(nodeId!, maxDepth).then((r) => r.data),
    enabled: nodeId !== null,
    staleTime: 60_000,
  });
}

/**
 * Hook for getting lineage path between two nodes
 */
export function useLineagePath(
  sourceId: string | null,
  targetId: string | null
) {
  return useQuery({
    queryKey: searchKeys.lineagePath(sourceId ?? '', targetId ?? ''),
    queryFn: () =>
      searchApi.getLineagePath(sourceId!, targetId!).then((r) => r.data),
    enabled: sourceId !== null && targetId !== null,
    staleTime: 60_000,
  });
}

/**
 * Hook for tracing PII flow
 */
export function usePiiFlow(nodeId: string | null) {
  return useQuery({
    queryKey: searchKeys.piiFlow(nodeId ?? ''),
    queryFn: () => searchApi.tracePiiFlow(nodeId!).then((r) => r.data),
    enabled: nodeId !== null,
    staleTime: 120_000,
  });
}

/**
 * Hook for all PII flows
 */
export function useAllPiiFlows() {
  return useQuery({
    queryKey: searchKeys.allPiiFlows(),
    queryFn: () => searchApi.getAllPiiFlows().then((r) => r.data),
    staleTime: 300_000,
  });
}

/**
 * Hook for graph statistics
 */
export function useGraphStats() {
  return useQuery({
    queryKey: searchKeys.graphStats(),
    queryFn: () => searchApi.getGraphStats().then((r) => r.data),
    staleTime: 60_000,
  });
}

/**
 * Hook for recording user interactions
 */
export function useRecordInteraction() {
  return useMutation({
    mutationFn: (interaction: UserInteraction) =>
      searchApi.recordInteraction(interaction),
  });
}

/**
 * Hook for exporting results
 */
export function useExportResults() {
  return useMutation({
    mutationFn: ({
      queryId,
      request,
    }: {
      queryId: string;
      request: ExportRequest;
    }) => searchApi.exportResults(queryId, request),
    onSuccess: (response, variables) => {
      // Create download link
      const blob = new Blob([response.data], {
        type: response.headers['content-type'],
      });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download =
        response.headers['content-disposition']?.split('filename=')[1] ||
        `export.${variables.request.format.toLowerCase()}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}

/**
 * Hook for learning analytics (admin)
 */
export function useLearningAnalytics(since?: string) {
  return useQuery({
    queryKey: searchKeys.analytics(since),
    queryFn: () => searchApi.getAnalytics(since).then((r) => r.data),
    staleTime: 300_000,
  });
}

/**
 * Hook for category suggestions (admin)
 */
export function useCategorySuggestions() {
  return useQuery({
    queryKey: searchKeys.categorySuggestions(),
    queryFn: () => searchApi.getCategorySuggestions().then((r) => r.data),
    staleTime: 300_000,
  });
}

/**
 * Hook for rebuilding graph (admin)
 */
export function useRebuildGraph() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => searchApi.rebuildGraph(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: searchKeys.graphStats() });
    },
  });
}
