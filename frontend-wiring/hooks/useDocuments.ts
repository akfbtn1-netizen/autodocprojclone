// =============================================
// DOCUMENT HOOKS
// File: frontend/src/hooks/useDocuments.ts
// React Query hooks for document operations
// =============================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { documentService } from '@/services/documents';
import type { Document } from '@/types/api';
import { toast } from 'sonner';

// =============================================
// QUERY KEYS
// =============================================

export const documentKeys = {
  all: ['documents'] as const,
  search: (params: object) => [...documentKeys.all, 'search', params] as const,
  detail: (id: number | string) => [...documentKeys.all, 'detail', id] as const,
};

// =============================================
// QUERIES
// =============================================

/**
 * Search documents with pagination and filters
 */
export function useDocumentSearch(params: {
  query?: string;
  documentType?: string;
  status?: string;
  schemaName?: string;
  businessDomain?: string;
  page?: number;
  pageSize?: number;
}) {
  return useQuery({
    queryKey: documentKeys.search(params),
    queryFn: () => documentService.search(params),
    placeholderData: (previousData) => previousData, // Keep showing old data while fetching
  });
}

/**
 * Get single document by ID
 */
export function useDocument(id: number | string) {
  return useQuery({
    queryKey: documentKeys.detail(id),
    queryFn: () => documentService.getById(id),
    enabled: !!id,
  });
}

// =============================================
// MUTATIONS
// =============================================

/**
 * Update document metadata
 */
export function useUpdateDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, updates }: { id: number | string; updates: Partial<Document> }) =>
      documentService.update(id, updates),
    onSuccess: (data, { id }) => {
      toast.success('Document updated');
      queryClient.invalidateQueries({ queryKey: documentKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: documentKeys.all });
    },
    onError: (error: Error) => {
      toast.error(`Failed to update: ${error.message}`);
    },
  });
}

/**
 * Create new document
 */
export function useCreateDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (document: Partial<Document>) => documentService.create(document),
    onSuccess: () => {
      toast.success('Document created');
      queryClient.invalidateQueries({ queryKey: documentKeys.all });
    },
    onError: (error: Error) => {
      toast.error(`Failed to create: ${error.message}`);
    },
  });
}
