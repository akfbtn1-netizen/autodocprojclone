// =============================================
// SIGNALR HOOK
// File: frontend/src/hooks/useSignalR.ts
// Real-time connection to /approvalHub
// =============================================

import { useEffect, useRef, useCallback, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { approvalKeys } from './useApprovals';
import { documentKeys } from './useDocuments';
import { dashboardKeys } from './useDashboard';
import type {
  DocumentGeneratedEvent,
  ApprovalRequestedEvent,
  ApprovalDecisionEvent,
  MasterIndexUpdatedEvent,
} from '@/types/api';

// =============================================
// CONFIGURATION
// =============================================

const SIGNALR_URL = import.meta.env.VITE_SIGNALR_URL ?? 'http://localhost:5195/approvalHub';

interface SignalRState {
  isConnected: boolean;
  isConnecting: boolean;
  error: Error | null;
}

// =============================================
// SIGNALR HOOK
// =============================================

export function useSignalR() {
  const queryClient = useQueryClient();
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [state, setState] = useState<SignalRState>({
    isConnected: false,
    isConnecting: false,
    error: null,
  });

  // =============================================
  // EVENT HANDLERS
  // =============================================

  const handleDocumentGenerated = useCallback((event: DocumentGeneratedEvent) => {
    console.log('📄 Document Generated:', event);
    
    toast.success(`New document generated`, {
      description: `${event.docIdString} - ${event.objectName}`,
      duration: 5000,
    });

    // Invalidate queries to refresh data
    queryClient.invalidateQueries({ queryKey: dashboardKeys.stats });
    queryClient.invalidateQueries({ queryKey: dashboardKeys.events(20) });
    queryClient.invalidateQueries({ queryKey: documentKeys.all });
    queryClient.invalidateQueries({ queryKey: ['pipeline'] });
  }, [queryClient]);

  const handleApprovalRequested = useCallback((event: ApprovalRequestedEvent) => {
    console.log('📋 Approval Requested:', event);
    
    toast.info(`New approval request`, {
      description: `${event.documentId} from ${event.requestedBy}`,
      duration: 5000,
    });

    queryClient.invalidateQueries({ queryKey: approvalKeys.pending() });
    queryClient.invalidateQueries({ queryKey: approvalKeys.stats() });
    queryClient.invalidateQueries({ queryKey: dashboardKeys.stats });
  }, [queryClient]);

  const handleApprovalDecision = useCallback((event: ApprovalDecisionEvent) => {
    console.log('✅ Approval Decision:', event);
    
    const icon = event.decision === 'Approved' ? '✅' : 
                 event.decision === 'Rejected' ? '❌' : '📝';
    
    toast.info(`${icon} Document ${event.decision.toLowerCase()}`, {
      description: `${event.documentId} by ${event.decidedBy}`,
      duration: 4000,
    });

    queryClient.invalidateQueries({ queryKey: approvalKeys.pending() });
    queryClient.invalidateQueries({ queryKey: approvalKeys.stats() });
    queryClient.invalidateQueries({ queryKey: approvalKeys.detail(event.approvalId) });
    queryClient.invalidateQueries({ queryKey: dashboardKeys.stats });
    queryClient.invalidateQueries({ queryKey: documentKeys.all });
  }, [queryClient]);

  const handleMasterIndexUpdated = useCallback((event: MasterIndexUpdatedEvent) => {
    console.log('📚 MasterIndex Updated:', event);

    queryClient.invalidateQueries({ queryKey: documentKeys.all });
    queryClient.invalidateQueries({ queryKey: documentKeys.detail(event.indexId) });
    queryClient.invalidateQueries({ queryKey: dashboardKeys.stats });
  }, [queryClient]);

  const handleAgentStatusChanged = useCallback((event: { agentName: string; status: string }) => {
    console.log('🤖 Agent Status Changed:', event);
    queryClient.invalidateQueries({ queryKey: ['agents'] });
  }, [queryClient]);

  const handleBulkOperationCompleted = useCallback((event: { operation: string; count: number }) => {
    console.log('📦 Bulk Operation Completed:', event);
    
    toast.success(`Bulk operation completed`, {
      description: `${event.operation}: ${event.count} items processed`,
    });

    queryClient.invalidateQueries({ queryKey: dashboardKeys.stats });
    queryClient.invalidateQueries({ queryKey: approvalKeys.all });
    queryClient.invalidateQueries({ queryKey: documentKeys.all });
  }, [queryClient]);

  // =============================================
  // CONNECTION MANAGEMENT
  // =============================================

  const connect = useCallback(async () => {
    // Don't reconnect if already connected
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    setState(prev => ({ ...prev, isConnecting: true, error: null }));

    try {
      const connection = new signalR.HubConnectionBuilder()
        .withUrl(SIGNALR_URL, {
          accessTokenFactory: () => localStorage.getItem('authToken') || '',
          withCredentials: false,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Exponential backoff: 0, 2s, 4s, 8s, 16s, then 30s max
            if (retryContext.previousRetryCount < 5) {
              return Math.min(Math.pow(2, retryContext.previousRetryCount) * 1000, 30000);
            }
            return 30000;
          },
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      // Register event handlers
      connection.on('DocumentGenerated', handleDocumentGenerated);
      connection.on('ApprovalRequested', handleApprovalRequested);
      connection.on('ApprovalDecision', handleApprovalDecision);
      connection.on('MasterIndexUpdated', handleMasterIndexUpdated);
      connection.on('AgentStatusChanged', handleAgentStatusChanged);
      connection.on('BulkOperationCompleted', handleBulkOperationCompleted);
      connection.on('DocumentSyncStatusChanged', (event) => {
        console.log('🔄 Document Sync Status:', event);
        queryClient.invalidateQueries({ queryKey: documentKeys.all });
      });

      // Connection lifecycle events
      connection.onreconnecting((error) => {
        console.warn('⚠️ SignalR reconnecting...', error);
        setState(prev => ({ ...prev, isConnected: false, isConnecting: true }));
        toast.warning('Reconnecting to server...');
      });

      connection.onreconnected((connectionId) => {
        console.log('✅ SignalR reconnected:', connectionId);
        setState(prev => ({ ...prev, isConnected: true, isConnecting: false }));
        toast.success('Reconnected to server');
        
        // Rejoin groups after reconnection
        const userId = localStorage.getItem('userId') || localStorage.getItem('userEmail');
        if (userId) {
          connection.invoke('JoinApprovalGroup', userId).catch(console.error);
        }
      });

      connection.onclose((error) => {
        console.log('🔌 SignalR connection closed:', error);
        setState({ isConnected: false, isConnecting: false, error: error || null });
      });

      // Start connection
      await connection.start();
      connectionRef.current = connection;

      // Join approval group
      const userId = localStorage.getItem('userId') || localStorage.getItem('userEmail');
      if (userId) {
        await connection.invoke('JoinApprovalGroup', userId);
        console.log('👥 Joined approval group:', userId);
      }

      setState({ isConnected: true, isConnecting: false, error: null });
      console.log('✅ SignalR connected to:', SIGNALR_URL);

    } catch (error) {
      console.error('❌ SignalR connection failed:', error);
      setState({ isConnected: false, isConnecting: false, error: error as Error });
    }
  }, [
    queryClient,
    handleDocumentGenerated,
    handleApprovalRequested,
    handleApprovalDecision,
    handleMasterIndexUpdated,
    handleAgentStatusChanged,
    handleBulkOperationCompleted,
  ]);

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop();
      } catch (error) {
        console.error('Error disconnecting SignalR:', error);
      }
      connectionRef.current = null;
      setState({ isConnected: false, isConnecting: false, error: null });
    }
  }, []);

  // =============================================
  // GROUP MANAGEMENT
  // =============================================

  const joinDocumentGroup = useCallback(async (documentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('JoinDocumentGroup', documentId);
      console.log('👥 Joined document group:', documentId);
    }
  }, []);

  const leaveDocumentGroup = useCallback(async (documentId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      await connectionRef.current.invoke('LeaveDocumentGroup', documentId);
      console.log('👋 Left document group:', documentId);
    }
  }, []);

  // =============================================
  // AUTO-CONNECT ON MOUNT
  // =============================================

  useEffect(() => {
    connect();

    return () => {
      disconnect();
    };
  }, [connect, disconnect]);

  // =============================================
  // RETURN VALUES
  // =============================================

  return {
    // State
    isConnected: state.isConnected,
    isConnecting: state.isConnecting,
    error: state.error,

    // Actions
    connect,
    disconnect,
    joinDocumentGroup,
    leaveDocumentGroup,

    // Connection reference (for advanced usage)
    connection: connectionRef.current,
  };
}

export default useSignalR;
