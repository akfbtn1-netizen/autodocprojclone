// =============================================
// APPROVAL HOOKS
// File: frontend/src/hooks/useApprovals.ts
// React Query hooks for approval operations
// =============================================

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { approvalService } from '@/services/approvals';
import type { ApproveRequest, RejectRequest, EditRequest, RepromptRequest } from '@/types/api';
import { toast } from 'sonner';

// =============================================
// QUERY KEYS
// =============================================

export const approvalKeys = {
  all: ['approvals'] as const,
  pending: () => [...approvalKeys.all, 'pending'] as const,
  detail: (id: number) => [...approvalKeys.all, 'detail', id] as const,
  document: (id: number) => [...approvalKeys.all, 'document', id] as const,
  stats: () => [...approvalKeys.all, 'stats'] as const,
};

// =============================================
// QUERIES
// =============================================

/**
 * Get pending approvals queue
 * Auto-refreshes every 30 seconds
 */
export function usePendingApprovals() {
  return useQuery({
    queryKey: approvalKeys.pending(),
    queryFn: approvalService.getPending,
    refetchInterval: 30000, // Refresh every 30s
  });
}

/**
 * Get all approvals (optionally filtered)
 */
export function useApprovals(params?: { status?: string }) {
  return useQuery({
    queryKey: [...approvalKeys.all, params],
    queryFn: () => approvalService.getAll(params),
  });
}

/**
 * Get single approval details
 */
export function useApproval(id: number) {
  return useQuery({
    queryKey: approvalKeys.detail(id),
    queryFn: () => approvalService.getById(id),
    enabled: id > 0,
  });
}

/**
 * Get document content for approval preview
 */
export function useApprovalDocument(id: number) {
  return useQuery({
    queryKey: approvalKeys.document(id),
    queryFn: () => approvalService.getDocumentContent(id),
    enabled: id > 0,
  });
}

/**
 * Get approval statistics for dashboard
 */
export function useApprovalStats() {
  return useQuery({
    queryKey: approvalKeys.stats(),
    queryFn: approvalService.getStats,
    refetchInterval: 60000, // Refresh every minute
  });
}

// =============================================
// MUTATIONS
// =============================================

/**
 * Approve a document
 */
export function useApproveDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: ApproveRequest }) =>
      approvalService.approve(id, request),
    onSuccess: (_, { id }) => {
      toast.success('Document approved successfully');
      // Invalidate related queries to refresh data
      queryClient.invalidateQueries({ queryKey: approvalKeys.pending() });
      queryClient.invalidateQueries({ queryKey: approvalKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: approvalKeys.stats() });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['documents'] });
    },
    onError: (error: Error) => {
      toast.error(`Failed to approve: ${error.message}`);
    },
  });
}

/**
 * Reject a document
 */
export function useRejectDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: RejectRequest }) =>
      approvalService.reject(id, request),
    onSuccess: (_, { id }) => {
      toast.success('Document rejected');
      queryClient.invalidateQueries({ queryKey: approvalKeys.pending() });
      queryClient.invalidateQueries({ queryKey: approvalKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: approvalKeys.stats() });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (error: Error) => {
      toast.error(`Failed to reject: ${error.message}`);
    },
  });
}

/**
 * Edit document during approval (tracks for AI training)
 * Creates entry in DaQa.DocumentEdits
 */
export function useEditDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: EditRequest }) =>
      approvalService.edit(id, request),
    onSuccess: (_, { id }) => {
      toast.success('Edit saved - will be used for AI training');
      queryClient.invalidateQueries({ queryKey: approvalKeys.document(id) });
    },
    onError: (error: Error) => {
      toast.error(`Failed to save edit: ${error.message}`);
    },
  });
}

/**
 * Request AI regeneration with feedback
 * Creates entry in DaQa.RegenerationRequests
 */
export function useRepromptDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: RepromptRequest }) =>
      approvalService.reprompt(id, request),
    onSuccess: (_, { id }) => {
      toast.success('Regeneration requested - new version will be created');
      queryClient.invalidateQueries({ queryKey: approvalKeys.pending() });
      queryClient.invalidateQueries({ queryKey: approvalKeys.detail(id) });
    },
    onError: (error: Error) => {
      toast.error(`Failed to request regeneration: ${error.message}`);
    },
  });
}

/**
 * Add suggestion to document
 */
export function useAddSuggestion() {
  return useMutation({
    mutationFn: ({ id, suggestion }: { id: number; suggestion: string }) =>
      approvalService.addSuggestion(id, suggestion),
    onSuccess: () => {
      toast.success('Suggestion added');
    },
    onError: (error: Error) => {
      toast.error(`Failed to add suggestion: ${error.message}`);
    },
  });
}
