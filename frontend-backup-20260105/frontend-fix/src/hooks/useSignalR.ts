// ═══════════════════════════════════════════════════════════════════════════
// SignalR Hooks
// Real-time updates for approvals, documents, and agent status
// Hub URL: /hubs/approval (port 5195)
// ═══════════════════════════════════════════════════════════════════════════

import { useEffect, useRef, useState, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import * as signalR from '@microsoft/signalr';
import { queryKeys } from './useQueries';
import type {
  Document,
  ApprovalRequest,
  Agent,
  AgentType,
  DocumentStatus,
  Notification,
} from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface SignalROptions {
  hubUrl: string;
  handlers: Record<string, (...args: unknown[]) => void>;
  autoConnect?: boolean;
}

interface SignalRConnection {
  connection: signalR.HubConnection | null;
  connectionState: signalR.HubConnectionState;
  error: Error | null;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  invoke: <T = unknown>(methodName: string, ...args: unknown[]) => Promise<T>;
}

// ─────────────────────────────────────────────────────────────────────────────
// Base SignalR Hook
// ─────────────────────────────────────────────────────────────────────────────

export function useSignalR({
  hubUrl,
  handlers,
  autoConnect = true,
}: SignalROptions): SignalRConnection {
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<signalR.HubConnectionState>(
    signalR.HubConnectionState.Disconnected
  );
  const [error, setError] = useState<Error | null>(null);

  // Build connection on mount
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Custom retry delays: 0s, 2s, 5s, 10s, then 30s
          const delays = [0, 2000, 5000, 10000, 30000];
          return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)] ?? 30000;
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Register event handlers
    Object.entries(handlers).forEach(([eventName, handler]) => {
      connection.on(eventName, handler);
    });

    // Connection state change handlers
    connection.onreconnecting((err) => {
      console.log('SignalR reconnecting:', err);
      setConnectionState(signalR.HubConnectionState.Reconnecting);
    });

    connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
      setConnectionState(signalR.HubConnectionState.Connected);
      setError(null);
    });

    connection.onclose((err) => {
      console.log('SignalR closed:', err);
      setConnectionState(signalR.HubConnectionState.Disconnected);
      if (err) setError(err);
    });

    connectionRef.current = connection;

    return () => {
      Object.keys(handlers).forEach((eventName) => {
        connection.off(eventName);
      });
    };
  }, [hubUrl]);

  // Update handlers without recreating connection
  useEffect(() => {
    const connection = connectionRef.current;
    if (!connection) return;

    Object.entries(handlers).forEach(([eventName, handler]) => {
      connection.off(eventName);
      connection.on(eventName, handler);
    });
  }, [handlers]);

  // Auto-connect
  useEffect(() => {
    if (autoConnect && connectionRef.current) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [autoConnect]);

  const connect = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection || connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      setError(null);
      await connection.start();
      setConnectionState(signalR.HubConnectionState.Connected);
      console.log('SignalR connected');
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Failed to connect');
      setError(error);
      setConnectionState(signalR.HubConnectionState.Disconnected);
      console.error('SignalR connection error:', error);
    }
  }, []);

  const disconnect = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection || connection.state === signalR.HubConnectionState.Disconnected) {
      return;
    }

    try {
      await connection.stop();
      setConnectionState(signalR.HubConnectionState.Disconnected);
      console.log('SignalR disconnected');
    } catch (err) {
      console.error('SignalR disconnect error:', err);
    }
  }, []);

  const invoke = useCallback(async <T = unknown>(
    methodName: string,
    ...args: unknown[]
  ): Promise<T> => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR not connected');
    }

    return connection.invoke<T>(methodName, ...args);
  }, []);

  return {
    connection: connectionRef.current,
    connectionState,
    error,
    connect,
    disconnect,
    invoke,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Approval Hub Hook
// ─────────────────────────────────────────────────────────────────────────────

export interface ApprovalHubCallbacks {
  onStatusUpdated?: (docId: string, status: DocumentStatus, approval: ApprovalRequest) => void;
  onNewDocument?: (document: Document) => void;
  onApprovalRequested?: (request: ApprovalRequest) => void;
  onApprovalCompleted?: (request: ApprovalRequest) => void;
  onDocumentGenerated?: (document: Document) => void;
}

export function useApprovalHub(callbacks?: ApprovalHubCallbacks) {
  const queryClient = useQueryClient();
  const [isConnected, setIsConnected] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);

  const addNotification = useCallback((notification: Omit<Notification, 'id' | 'timestamp' | 'read'>) => {
    const newNotification: Notification = {
      ...notification,
      id: `notif-${Date.now()}`,
      timestamp: new Date().toISOString(),
      read: false,
    };
    setNotifications((prev) => [newNotification, ...prev].slice(0, 50));
    return newNotification;
  }, []);

  const handlers = {
    // Document status changed
    StatusUpdated: (documentId: string, newStatus: string, approval?: ApprovalRequest) => {
      console.log(`Document ${documentId} status updated to ${newStatus}`);
      
      // Invalidate relevant queries
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardKpis });
      
      // Call user callback
      callbacks?.onStatusUpdated?.(documentId, newStatus as DocumentStatus, approval!);
      
      // Add notification
      addNotification({
        type: 'approval_completed',
        title: 'Document Status Updated',
        message: `Document ${documentId} is now ${newStatus}`,
        documentId,
      });
    },

    // New document created
    NewDocument: (document: Document) => {
      console.log('New document received:', document.docId);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
      queryClient.invalidateQueries({ queryKey: queryKeys.recentDocuments });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardKpis });
      
      callbacks?.onNewDocument?.(document);
      
      addNotification({
        type: 'document_generated',
        title: 'New Document Created',
        message: `${document.title} has been generated`,
        documentId: document.docId,
      });
    },

    // Approval requested
    ApprovalRequested: (request: ApprovalRequest) => {
      console.log('New approval request:', request.documentTitle);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.approvalStats });
      
      callbacks?.onApprovalRequested?.(request);
      
      addNotification({
        type: 'approval_requested',
        title: 'Approval Required',
        message: `${request.documentTitle} needs your review`,
        documentId: request.documentId,
        priority: request.priority,
      });
    },

    // Approval completed
    ApprovalCompleted: (request: ApprovalRequest) => {
      console.log('Approval completed:', request.documentTitle);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.pendingApprovals });
      queryClient.invalidateQueries({ queryKey: queryKeys.approvalStats });
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
      
      callbacks?.onApprovalCompleted?.(request);
    },

    // Document generated (final output ready)
    DocumentGenerated: (document: Document) => {
      console.log('Document generated:', document.docId);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.documents });
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboardKpis });
      
      callbacks?.onDocumentGenerated?.(document);
      
      addNotification({
        type: 'document_generated',
        title: 'Document Ready',
        message: `${document.title} is ready for download`,
        documentId: document.docId,
      });
    },
  };

  const { connectionState, error, invoke } = useSignalR({
    hubUrl: '/hubs/approval',
    handlers,
  });

  useEffect(() => {
    setIsConnected(connectionState === signalR.HubConnectionState.Connected);
  }, [connectionState]);

  // Hub methods
  const requestApproval = useCallback(async (documentId: string) => {
    return invoke('RequestApproval', documentId);
  }, [invoke]);

  const approveDocument = useCallback(async (documentId: string, comment?: string) => {
    return invoke('ApproveDocument', documentId, comment);
  }, [invoke]);

  const rejectDocument = useCallback(async (documentId: string, reason: string) => {
    return invoke('RejectDocument', documentId, reason);
  }, [invoke]);

  const joinDocumentRoom = useCallback(async (documentId: string) => {
    return invoke('JoinDocumentRoom', documentId);
  }, [invoke]);

  const leaveDocumentRoom = useCallback(async (documentId: string) => {
    return invoke('LeaveDocumentRoom', documentId);
  }, [invoke]);

  return {
    isConnected,
    error,
    notifications,
    requestApproval,
    approveDocument,
    rejectDocument,
    joinDocumentRoom,
    leaveDocumentRoom,
    clearNotifications: () => setNotifications([]),
    markNotificationRead: (id: string) => {
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, read: true } : n))
      );
    },
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Agent Hub Hook (for agent monitoring)
// ─────────────────────────────────────────────────────────────────────────────

export interface AgentHubCallbacks {
  onAgentStatusChanged?: (agentType: AgentType, status: string, agent: Agent) => void;
  onAgentError?: (agentType: AgentType, error: string) => void;
  onProcessingStarted?: (agentType: AgentType, documentId: string) => void;
  onProcessingCompleted?: (agentType: AgentType, documentId: string, duration: number) => void;
}

export function useAgentHub(callbacks?: AgentHubCallbacks) {
  const queryClient = useQueryClient();
  const [isConnected, setIsConnected] = useState(false);

  const handlers = {
    AgentStatusChanged: (agentType: string, status: string, agent?: Agent) => {
      console.log(`Agent ${agentType} status: ${status}`);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.agents });
      queryClient.invalidateQueries({ queryKey: queryKeys.agentHealth });
      
      callbacks?.onAgentStatusChanged?.(agentType as AgentType, status, agent!);
    },

    AgentError: (agentType: string, errorMessage: string) => {
      console.error(`Agent ${agentType} error:`, errorMessage);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.agents });
      
      callbacks?.onAgentError?.(agentType as AgentType, errorMessage);
    },

    ProcessingStarted: (agentType: string, documentId: string) => {
      console.log(`Agent ${agentType} started processing ${documentId}`);
      
      callbacks?.onProcessingStarted?.(agentType as AgentType, documentId);
    },

    ProcessingCompleted: (agentType: string, documentId: string, durationMs: number) => {
      console.log(`Agent ${agentType} completed ${documentId} in ${durationMs}ms`);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.agentActivity(agentType) });
      queryClient.invalidateQueries({ queryKey: queryKeys.agentStats });
      
      callbacks?.onProcessingCompleted?.(agentType as AgentType, documentId, durationMs);
    },

    QueueUpdated: (agentType: string, queueDepth: number) => {
      console.log(`Agent ${agentType} queue depth: ${queueDepth}`);
      
      queryClient.invalidateQueries({ queryKey: queryKeys.agents });
    },
  };

  const { connectionState, error, invoke } = useSignalR({
    hubUrl: '/hubs/agents',
    handlers,
  });

  useEffect(() => {
    setIsConnected(connectionState === signalR.HubConnectionState.Connected);
  }, [connectionState]);

  return {
    isConnected,
    error,
  };
}

// ─────────────────────────────────────────────────────────────────────────────
// Combined Real-Time Hook
// ─────────────────────────────────────────────────────────────────────────────

export function useRealTimeUpdates() {
  const approvalHub = useApprovalHub();
  const agentHub = useAgentHub();

  return {
    isConnected: approvalHub.isConnected && agentHub.isConnected,
    approvalHub,
    agentHub,
  };
}
