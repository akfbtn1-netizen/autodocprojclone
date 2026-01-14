// =============================================
// APP.TSX - WIRED VERSION
// File: frontend/src/App.tsx
// Main app with all providers configured
// =============================================

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'sonner';

import { queryClient } from '@/lib/queryClient';
import { SignalRProvider } from '@/providers/SignalRProvider';

// Layout
import { Layout } from '@/components/layout/Layout';

// Pages
import { Dashboard } from '@/pages/Dashboard';
import { Documents } from '@/pages/Documents';
import { Approvals } from '@/pages/Approvals';
import { Settings } from '@/pages/Settings';

// =============================================
// APP COMPONENT
// =============================================

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SignalRProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<Layout />}>
              {/* Dashboard - main landing page */}
              <Route index element={<Dashboard />} />
              
              {/* Documents - search and browse MasterIndex */}
              <Route path="documents" element={<Documents />} />
              <Route path="documents/:id" element={<DocumentDetail />} />
              
              {/* Approvals - pending approval queue */}
              <Route path="approvals" element={<Approvals />} />
              <Route path="approvals/:id" element={<ApprovalDetail />} />
              
              {/* Settings */}
              <Route path="settings" element={<Settings />} />
              
              {/* Catch all - redirect to dashboard */}
              <Route path="*" element={<Navigate to="/" replace />} />
            </Route>
          </Routes>
        </BrowserRouter>
        
        {/* Toast notifications */}
        <Toaster 
          position="top-right"
          expand={false}
          richColors
          toastOptions={{
            duration: 4000,
            style: {
              background: '#fafaf9', // stone-50
              border: '1px solid #e7e5e4', // stone-200
              color: '#1c1917', // stone-900
            },
          }}
        />
      </SignalRProvider>
      
      {/* React Query DevTools - only in development */}
      {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
    </QueryClientProvider>
  );
}

// =============================================
// PLACEHOLDER COMPONENTS
// These should be replaced with actual page components
// =============================================

function DocumentDetail() {
  return <div>Document Detail - Wire with useDocument hook</div>;
}

function ApprovalDetail() {
  return <div>Approval Detail - Wire with useApproval hook</div>;
}
