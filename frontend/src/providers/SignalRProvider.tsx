// =============================================
// SIGNALR PROVIDER
// File: frontend/src/providers/SignalRProvider.tsx
// Context provider for SignalR connection state
// =============================================

import { createContext, useContext, ReactNode } from 'react';
import { useSignalR } from '@/hooks/useSignalR';

// =============================================
// CONTEXT TYPE
// =============================================

interface SignalRContextValue {
  isConnected: boolean;
  isConnecting: boolean;
  error: Error | null;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  joinDocumentGroup: (documentId: string) => Promise<void>;
  leaveDocumentGroup: (documentId: string) => Promise<void>;
}

const SignalRContext = createContext<SignalRContextValue | null>(null);

// =============================================
// PROVIDER COMPONENT
// =============================================

interface SignalRProviderProps {
  children: ReactNode;
}

export function SignalRProvider({ children }: SignalRProviderProps) {
  const signalR = useSignalR();

  return (
    <SignalRContext.Provider value={signalR}>
      {children}
    </SignalRContext.Provider>
  );
}

// =============================================
// CONSUMER HOOK
// =============================================

export function useSignalRContext() {
  const context = useContext(SignalRContext);
  
  if (!context) {
    throw new Error('useSignalRContext must be used within a SignalRProvider');
  }
  
  return context;
}

export default SignalRProvider;
