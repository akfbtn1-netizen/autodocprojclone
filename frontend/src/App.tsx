// TODO [6]: Add /search route when Smart Search Agent is ready
// TODO [3]: Add /lineage route when Data Lineage Agent is ready
import { useEffect, lazy, Suspense } from 'react';

// Lazy load Gap Intelligence Dashboard
const GapIntelligenceDashboard = lazy(() => import('@/features/gap-intelligence/GapIntelligenceDashboard'));
import { BrowserRouter, Routes, Route, useNavigate, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactFlowProvider } from '@xyflow/react';
import { MainLayout } from '@/components/layout';
import { Dashboard, Documents, Approvals, Settings } from '@/pages';
import { MasterIndexDashboard, MasterIndexDetail } from '@/components/MasterIndex';
import { SchemaChangeDashboard } from '@/components/schemaChange';
import { useUIStore } from '@/stores';

// Create React Query client
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function AppRoutes() {
  const navigate = useNavigate();
  const location = useLocation();
  const { theme } = useUIStore();

  // Apply theme to document
  useEffect(() => {
    const root = document.documentElement;
    
    if (theme === 'system') {
      const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      root.classList.toggle('dark', isDark);
      
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      const handler = (e: MediaQueryListEvent) => {
        root.classList.toggle('dark', e.matches);
      };
      
      mediaQuery.addEventListener('change', handler);
      return () => mediaQuery.removeEventListener('change', handler);
    } else {
      root.classList.toggle('dark', theme === 'dark');
    }
  }, [theme]);

  const handleNavigate = (path: string) => {
    navigate(path);
  };

  return (
    <MainLayout currentPath={location.pathname} onNavigate={handleNavigate}>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/documents" element={<Documents />} />
        <Route path="/catalog" element={<MasterIndexDashboard />} />
        <Route path="/catalog/:id" element={<MasterIndexDetail />} />
        <Route path="/approvals" element={<Approvals />} />
        <Route path="/schema-changes" element={<SchemaChangeDashboard />} />
        <Route path="/gap-intelligence" element={
          <Suspense fallback={<div className="flex items-center justify-center h-full"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-teal-600" /></div>}>
            <GapIntelligenceDashboard />
          </Suspense>
        } />
        <Route path="/settings" element={<Settings />} />
      </Routes>
    </MainLayout>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ReactFlowProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </ReactFlowProvider>
    </QueryClientProvider>
  );
}

export default App;
