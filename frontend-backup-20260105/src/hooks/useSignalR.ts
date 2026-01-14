import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

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
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register event handlers
    Object.entries(handlers).forEach(([eventName, handler]) => {
      connection.on(eventName, handler);
    });

    // Connection state change handlers
    connection.onreconnecting((error) => {
      console.log('SignalR reconnecting:', error);
      setConnectionState(signalR.HubConnectionState.Reconnecting);
    });

    connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
      setConnectionState(signalR.HubConnectionState.Connected);
      setError(null);
    });

    connection.onclose((error) => {
      console.log('SignalR closed:', error);
      setConnectionState(signalR.HubConnectionState.Disconnected);
      if (error) setError(error);
    });

    connectionRef.current = connection;

    return () => {
      // Cleanup: unregister handlers
      Object.keys(handlers).forEach((eventName) => {
        connection.off(eventName);
      });
    };
  }, [hubUrl]); // Only recreate when hubUrl changes

  // Update handlers without recreating connection
  useEffect(() => {
    const connection = connectionRef.current;
    if (!connection) return;

    // Remove old handlers and add new ones
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

// Simplified hook for common approval hub usage
export function useApprovalHub(onStatusUpdate?: (docId: string, status: string) => void) {
  const [isConnected, setIsConnected] = useState(false);

  const { connectionState, error, invoke } = useSignalR({
    hubUrl: '/hubs/approval',
    handlers: {
      StatusUpdated: (documentId: string, newStatus: string) => {
        console.log(`Document ${documentId} status updated to ${newStatus}`);
        onStatusUpdate?.(documentId, newStatus);
      },
      NewDocument: (document: unknown) => {
        console.log('New document received:', document);
      },
      ApprovalRequested: (request: unknown) => {
        console.log('New approval request:', request);
      },
    },
  });

  useEffect(() => {
    setIsConnected(connectionState === signalR.HubConnectionState.Connected);
  }, [connectionState]);

  const requestApproval = useCallback(async (documentId: string) => {
    return invoke('RequestApproval', documentId);
  }, [invoke]);

  const approveDocument = useCallback(async (documentId: string, comment?: string) => {
    return invoke('ApproveDocument', documentId, comment);
  }, [invoke]);

  const rejectDocument = useCallback(async (documentId: string, reason: string) => {
    return invoke('RejectDocument', documentId, reason);
  }, [invoke]);

  return {
    isConnected,
    error,
    requestApproval,
    approveDocument,
    rejectDocument,
  };
}
