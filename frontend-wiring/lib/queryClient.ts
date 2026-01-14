// =============================================
// QUERY CLIENT CONFIGURATION
// File: frontend/src/lib/queryClient.ts
// React Query client with defaults
// =============================================

import { QueryClient } from '@tanstack/react-query';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Data is considered fresh for 2 minutes
      staleTime: 1000 * 60 * 2,
      
      // Cached data kept for 10 minutes after becoming unused
      gcTime: 1000 * 60 * 10,
      
      // Retry failed requests twice
      retry: 2,
      
      // Don't refetch when window regains focus (we have SignalR for real-time)
      refetchOnWindowFocus: false,
      
      // Don't refetch on mount if data exists
      refetchOnMount: false,
    },
    mutations: {
      // Mutations don't retry by default
      retry: 0,
    },
  },
});

export default queryClient;
