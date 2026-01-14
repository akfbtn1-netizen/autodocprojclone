// ═══════════════════════════════════════════════════════════════════════════
// Approval Workflow React Query Hooks
// Data fetching and mutations for approval workflow
// ═══════════════════════════════════════════════════════════════════════════

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect } from 'react';
import { approvalApi } from '@/services/approvalApi';
import { signalRService, SignalREvents } from '@/services/signalr';
import type {
  Approval,
  ApprovalFilters,
  ApprovalStats,
  RegenerationRequest,
  DocumentEdit,
  ApproveRequest,
  RejectRequest,
} from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Query Keys Factory
// ─────────────────────────────────────────────────────────────────────────────

export const approvalKeys = {
  all: ['approvals'] as const,
  lists: () => [...approvalKeys.all, 'list'] as const,
  list: (filters?: ApprovalFilters) =>
    [...approvalKeys.lists(), { filters }] as const,
  details: () => [...approvalKeys.all, 'detail'] as const,
  detail: (id: number) => [...approvalKeys.details(), id] as const,
  pending: () => [...approvalKeys.all, 'pending'] as const,
  overdue: () => [...approvalKeys.all, 'overdue'] as const,
  stats: () => [...approvalKeys.all, 'stats'] as const,
  history: (documentId: string) =>
    [...approvalKeys.all, 'history', documentId] as const,
  events: (documentId: string) =>
    [...approvalKeys.all, 'events', documentId] as const,
  edits: (approvalId: number) =>
    [...approvalKeys.all, 'edits', approvalId] as const,
  content: (approvalId: number) =>
    [...approvalKeys.all, 'content', approvalId] as const,
  notifications: () => [...approvalKeys.all, 'notifications'] as const,
  unreadCount: () => [...approvalKeys.all, 'unread-count'] as const,
  search: (query: string, page: number) =>
    [...approvalKeys.all, 'search', { query, page }] as const,
};

// ─────────────────────────────────────────────────────────────────────────────
// Query Hooks
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook to fetch approvals with filters
 */
export function useApprovals(filters?: ApprovalFilters) {
  return useQuery({
    queryKey: approvalKeys.list(filters),
    queryFn: () => approvalApi.getApprovals(filters),
    staleTime: 30_000, // 30 seconds
    refetchInterval: 60_000, // Refetch every 60 seconds
  });
}

/**
 * Hook to fetch single approval
 */
export function useApproval(id: number) {
  return useQuery({
    queryKey: approvalKeys.detail(id),
    queryFn: () => approvalApi.getApproval(id),
    staleTime: 30_000,
    enabled: id > 0,
  });
}

/**
 * Hook to fetch pending approvals
 */
export function usePendingApprovals() {
  return useQuery({
    queryKey: approvalKeys.pending(),
    queryFn: () => approvalApi.getPendingApprovals(),
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}

/**
 * Hook to fetch overdue approvals
 */
export function useOverdueApprovals() {
  return useQuery({
    queryKey: approvalKeys.overdue(),
    queryFn: () => approvalApi.getOverdueApprovals(),
    staleTime: 60_000,
    refetchInterval: 120_000,
  });
}

/**
 * Hook to fetch approval stats
 */
export function useApprovalStats() {
  return useQuery({
    queryKey: approvalKeys.stats(),
    queryFn: () => approvalApi.getApprovalStats(),
    staleTime: 60_000,
    refetchInterval: 120_000,
  });
}

/**
 * Hook to fetch approval history
 */
export function useApprovalHistory(documentId: string) {
  return useQuery({
    queryKey: approvalKeys.history(documentId),
    queryFn: () => approvalApi.getApprovalHistory(documentId),
    staleTime: 60_000,
    enabled: !!documentId,
  });
}

/**
 * Hook to fetch workflow events
 */
export function useWorkflowEvents(documentId: string) {
  return useQuery({
    queryKey: approvalKeys.events(documentId),
    queryFn: () => approvalApi.getWorkflowEvents(documentId),
    staleTime: 30_000,
    enabled: !!documentId,
  });
}

/**
 * Hook to fetch document edits
 */
export function useDocumentEdits(approvalId: number) {
  return useQuery({
    queryKey: approvalKeys.edits(approvalId),
    queryFn: () => approvalApi.getDocumentEdits(approvalId),
    staleTime: 60_000,
    enabled: approvalId > 0,
  });
}

/**
 * Hook to fetch document content for editing
 */
export function useDocumentContent(approvalId: number) {
  return useQuery({
    queryKey: approvalKeys.content(approvalId),
    queryFn: () => approvalApi.getDocumentContent(approvalId),
    staleTime: 60_000,
    enabled: approvalId > 0,
  });
}

/**
 * Hook to fetch notifications
 */
export function useNotifications() {
  return useQuery({
    queryKey: approvalKeys.notifications(),
    queryFn: () => approvalApi.getNotifications(),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

/**
 * Hook to fetch unread notification count
 */
export function useUnreadNotificationCount() {
  return useQuery({
    queryKey: approvalKeys.unreadCount(),
    queryFn: () => approvalApi.getUnreadCount(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
}

/**
 * Hook to search approvals
 */
export function useApprovalSearch(query: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: approvalKeys.search(query, page),
    queryFn: () => approvalApi.searchApprovals(query, page, pageSize),
    staleTime: 30_000,
    enabled: query.length >= 2,
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// Mutation Hooks
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook to approve document
 */
export function useApproveDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: ApproveRequest }) =>
      approvalApi.approveDocument(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

/**
 * Hook to reject document
 */
export function useRejectDocument() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, request }: { id: number; request: RejectRequest }) =>
      approvalApi.rejectDocument(id, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

/**
 * Hook to request regeneration (re-prompt)
 */
export function useRequestRegeneration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RegenerationRequest) =>
      approvalApi.requestRegeneration(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

/**
 * Hook to save inline edits
 */
export function useSaveEdits() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalId,
      edits,
    }: {
      approvalId: number;
      edits: Omit<DocumentEdit, 'id' | 'editedAt'>[];
    }) => approvalApi.saveEdits(approvalId, edits),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: approvalKeys.detail(variables.approvalId),
      });
      queryClient.invalidateQueries({
        queryKey: approvalKeys.edits(variables.approvalId),
      });
    },
  });
}

/**
 * Hook to submit feedback
 */
export function useSubmitFeedback() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalId,
      qualityRating,
      feedbackText,
    }: {
      approvalId: number;
      qualityRating: number;
      feedbackText?: string;
    }) => approvalApi.submitFeedback(approvalId, { qualityRating, feedbackText }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: approvalKeys.detail(variables.approvalId),
      });
    },
  });
}

/**
 * Hook to mark notification as read
 */
export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (notificationId: number) =>
      approvalApi.markNotificationRead(notificationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.notifications() });
      queryClient.invalidateQueries({ queryKey: approvalKeys.unreadCount() });
    },
  });
}

/**
 * Hook to mark all notifications as read
 */
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => approvalApi.markAllNotificationsRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.notifications() });
      queryClient.invalidateQueries({ queryKey: approvalKeys.unreadCount() });
    },
  });
}

/**
 * Hook for bulk approve
 */
export function useBulkApprove() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalIds,
      comments,
    }: {
      approvalIds: number[];
      comments?: string;
    }) => approvalApi.bulkApprove(approvalIds, comments),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

/**
 * Hook for bulk reject
 */
export function useBulkReject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalIds,
      reason,
    }: {
      approvalIds: number[];
      reason: string;
    }) => approvalApi.bulkReject(approvalIds, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

/**
 * Hook to assign approval
 */
export function useAssignApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalId,
      assignedTo,
    }: {
      approvalId: number;
      assignedTo: string;
    }) => approvalApi.assignApproval(approvalId, assignedTo),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: approvalKeys.detail(variables.approvalId),
      });
      queryClient.invalidateQueries({ queryKey: approvalKeys.lists() });
    },
  });
}

/**
 * Hook to escalate approval
 */
export function useEscalateApproval() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      approvalId,
      message,
    }: {
      approvalId: number;
      message?: string;
    }) => approvalApi.escalateApproval(approvalId, message),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    },
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SignalR Real-time Hook
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook to manage SignalR connection and real-time approval updates
 */
export function useApprovalSignalR() {
  const queryClient = useQueryClient();

  const invalidateApprovals = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: approvalKeys.all });
  }, [queryClient]);

  useEffect(() => {
    // Start SignalR connection
    signalRService.start().catch(console.error);

    // Subscribe to approval events
    const handleApprovalRequested = () => {
      invalidateApprovals();
    };

    const handleApprovalCompleted = () => {
      invalidateApprovals();
    };

    const handleApprovalRejected = () => {
      invalidateApprovals();
    };

    signalRService.on(SignalREvents.ApprovalRequested, handleApprovalRequested);
    signalRService.on(SignalREvents.ApprovalCompleted, handleApprovalCompleted);
    signalRService.on(SignalREvents.ApprovalRejected, handleApprovalRejected);

    return () => {
      signalRService.off(
        SignalREvents.ApprovalRequested,
        handleApprovalRequested
      );
      signalRService.off(
        SignalREvents.ApprovalCompleted,
        handleApprovalCompleted
      );
      signalRService.off(SignalREvents.ApprovalRejected, handleApprovalRejected);
    };
  }, [invalidateApprovals]);

  return {
    isConnected: signalRService.isConnected,
    connectionState: signalRService.connectionState,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Cache Invalidation Helper
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Hook to get cache invalidation functions
 */
export function useApprovalInvalidation() {
  const queryClient = useQueryClient();

  return {
    invalidateAll: () =>
      queryClient.invalidateQueries({ queryKey: approvalKeys.all }),
    invalidateLists: () =>
      queryClient.invalidateQueries({ queryKey: approvalKeys.lists() }),
    invalidateDetail: (id: number) =>
      queryClient.invalidateQueries({ queryKey: approvalKeys.detail(id) }),
    invalidateStats: () =>
      queryClient.invalidateQueries({ queryKey: approvalKeys.stats() }),
    invalidateNotifications: () =>
      queryClient.invalidateQueries({ queryKey: approvalKeys.notifications() }),
  };
}
