// TODO [2A]: Register handlers for all ApprovalHub events (DocumentGenerated, ApprovalRequested, etc.)
// TODO [2A]: Connect to approval store for real-time state updates
// TODO [6]: Add search suggestion events when Smart Search is ready
import * as signalR from '@microsoft/signalr';

const SIGNALR_HUB_URL = import.meta.env.VITE_SIGNALR_HUB || 'http://localhost:5195/approvalHub';

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_HUB_URL)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: () => {
          this.reconnectAttempts++;
          if (this.reconnectAttempts >= this.maxReconnectAttempts) {
            return null; // Stop reconnecting
          }
          return Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupConnectionHandlers();
  }

  private setupConnectionHandlers() {
    if (!this.connection) return;

    this.connection.onreconnecting(() => {
      console.log('SignalR reconnecting...');
    });

    this.connection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.reconnectAttempts = 0;
    });

    this.connection.onclose((error) => {
      if (error) {
        console.error('SignalR connection closed with error:', error);
      } else {
        console.log('SignalR connection closed');
      }
    });
  }

  async start(): Promise<void> {
    if (!this.connection || this.connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.start();
      console.log('SignalR connected');
      this.reconnectAttempts = 0;
    } catch (error) {
      console.error('Error starting SignalR connection:', error);
      throw error;
    }
  }

  async stop(): Promise<void> {
    if (!this.connection || this.connection.state === signalR.HubConnectionState.Disconnected) {
      return;
    }

    try {
      await this.connection.stop();
      console.log('SignalR disconnected');
    } catch (error) {
      console.error('Error stopping SignalR connection:', error);
    }
  }

  on(eventName: string, callback: (...args: any[]) => void): void {
    if (!this.connection) return;
    this.connection.on(eventName, callback);
  }

  off(eventName: string, callback?: (...args: any[]) => void): void {
    if (!this.connection) return;
    if (callback) {
      this.connection.off(eventName, callback);
    } else {
      this.connection.off(eventName);
    }
  }

  async invoke(methodName: string, ...args: any[]): Promise<any> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established');
    }

    return await this.connection.invoke(methodName, ...args);
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  get connectionState(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected;
  }
}

// Export singleton instance
export const signalRService = new SignalRService();

// ─────────────────────────────────────────────────────────────────────────────
// SignalR Event Names
// ─────────────────────────────────────────────────────────────────────────────

export const SignalREvents = {
  // Approval events
  ApprovalRequested: 'ApprovalRequested',
  ApprovalCompleted: 'ApprovalCompleted',
  ApprovalRejected: 'ApprovalRejected',

  // Document events
  DocumentGenerated: 'DocumentGenerated',
  DocumentUpdated: 'DocumentUpdated',

  // MasterIndex events
  MasterIndexUpdated: 'MasterIndexUpdated',
  MasterIndexCreated: 'MasterIndexCreated',
  MasterIndexDeleted: 'MasterIndexDeleted',
  StatisticsChanged: 'StatisticsChanged',

  // Agent events
  AgentStatusChanged: 'AgentStatusChanged',
  AgentError: 'AgentError',
} as const;

export type SignalREventName = (typeof SignalREvents)[keyof typeof SignalREvents];

export default signalRService;