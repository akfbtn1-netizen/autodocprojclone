import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Menu, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Header } from './Header';
import { Sidebar } from './Sidebar';

interface MainLayoutProps {
  children: React.ReactNode;
  currentPath?: string;
  onNavigate?: (path: string) => void;
}

export function MainLayout({ children, currentPath = '/', onNavigate }: MainLayoutProps) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  // Close mobile menu on route change
  useEffect(() => {
    setMobileMenuOpen(false);
  }, [currentPath]);

  // Close mobile menu on escape key
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setMobileMenuOpen(false);
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, []);

  // Prevent body scroll when mobile menu is open
  useEffect(() => {
    if (mobileMenuOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [mobileMenuOpen]);

  return (
    <div className="min-h-screen bg-stone-100 dark:bg-stone-950">
      {/* Desktop Sidebar */}
      <div className="hidden lg:block">
        <Sidebar currentPath={currentPath} onNavigate={onNavigate} />
      </div>

      {/* Mobile Sidebar Overlay */}
      <AnimatePresence>
        {mobileMenuOpen && (
          <>
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.2 }}
              className="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm lg:hidden"
              onClick={() => setMobileMenuOpen(false)}
            />

            {/* Mobile Sidebar */}
            <motion.div
              initial={{ x: '-100%' }}
              animate={{ x: 0 }}
              exit={{ x: '-100%' }}
              transition={{ type: 'spring', damping: 25, stiffness: 300 }}
              className="fixed inset-y-0 left-0 z-50 w-72 lg:hidden"
            >
              <Sidebar currentPath={currentPath} onNavigate={onNavigate} />

              {/* Close button */}
              <button
                onClick={() => setMobileMenuOpen(false)}
                className="absolute right-3 top-4 flex h-8 w-8 items-center justify-center rounded-md bg-stone-200 dark:bg-stone-800 text-stone-600 dark:text-stone-400 hover:bg-stone-300 dark:hover:bg-stone-700 transition-colors"
              >
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {/* Main Content Area */}
      <div
        className={cn(
          'flex flex-col min-h-screen transition-all duration-200',
          'lg:pl-[280px]' // Matches sidebar width
        )}
      >
        {/* Header */}
        <Header onMenuClick={() => setMobileMenuOpen(true)} />

        {/* Page Content */}
        <main className="flex-1 p-4 lg:p-6">
          <motion.div
            key={currentPath}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.2 }}
          >
            {children}
          </motion.div>
        </main>

        {/* Footer */}
        <footer className="border-t border-stone-200 dark:border-stone-800 bg-stone-50 dark:bg-stone-900 px-4 lg:px-6 py-4">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-4 text-sm text-stone-500 dark:text-stone-400">
            <div className="flex items-center gap-4">
              <span>© 2025 DocAgent Enterprise</span>
              <span className="hidden sm:inline">•</span>
              <a
                href="#"
                className="hover:text-teal-600 dark:hover:text-teal-400 transition-colors"
              >
                Documentation
              </a>
              <a
                href="#"
                className="hover:text-teal-600 dark:hover:text-teal-400 transition-colors"
              >
                Support
              </a>
            </div>
            <div className="flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
              <span className="text-xs">v2.0.0</span>
            </div>
          </div>
        </footer>
      </div>
    </div>
  );
}

export default MainLayout;
