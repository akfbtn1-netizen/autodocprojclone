import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  LayoutDashboard,
  FileText,
  CheckSquare,
  Settings,
  Bot,
  ChevronLeft,
  ChevronRight,
  Activity,
  Zap,
  Database,
  Shield,
  GitBranch,
  Sparkles,
  Circle,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItem {
  id: string;
  label: string;
  icon: React.ElementType;
  href: string;
  badge?: number;
}

interface Agent {
  id: string;
  name: string;
  status: 'active' | 'idle' | 'processing' | 'error';
  description: string;
  icon: React.ElementType;
}

const navItems: NavItem[] = [
  { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, href: '/' },
  { id: 'documents', label: 'Documents', icon: FileText, href: '/documents' },
  { id: 'catalog', label: 'Catalog', icon: Database, href: '/catalog' },
  { id: 'approvals', label: 'Approvals', icon: CheckSquare, href: '/approvals', badge: 5 },
  { id: 'settings', label: 'Settings', icon: Settings, href: '/settings' },
];

const agents: Agent[] = [
  {
    id: 'doc-automation',
    name: 'Doc Automation',
    status: 'active',
    description: 'Documentation generation',
    icon: FileText,
  },
  {
    id: 'schema-mapper',
    name: 'Schema Mapper',
    status: 'idle',
    description: 'Database schema analysis',
    icon: Database,
  },
  {
    id: 'lineage-tracer',
    name: 'Lineage Tracer',
    status: 'idle',
    description: 'Data lineage tracking',
    icon: GitBranch,
  },
  {
    id: 'qa-validator',
    name: 'QA Validator',
    status: 'processing',
    description: 'Quality assurance checks',
    icon: Shield,
  },
  {
    id: 'ai-enhancer',
    name: 'AI Enhancer',
    status: 'active',
    description: 'Content enhancement',
    icon: Sparkles,
  },
];

const statusColors = {
  active: 'bg-emerald-500',
  idle: 'bg-stone-400',
  processing: 'bg-amber-500',
  error: 'bg-red-500',
};

const statusLabels = {
  active: 'Active',
  idle: 'Idle',
  processing: 'Processing',
  error: 'Error',
};

interface SidebarProps {
  currentPath?: string;
  onNavigate?: (path: string) => void;
}

export function Sidebar({ currentPath = '/', onNavigate }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false);
  const [hoveredAgent, setHoveredAgent] = useState<string | null>(null);

  const handleNavClick = (href: string) => {
    onNavigate?.(href);
  };

  return (
    <motion.aside
      initial={false}
      animate={{ width: collapsed ? 72 : 280 }}
      transition={{ duration: 0.2, ease: 'easeInOut' }}
      className={cn(
        'fixed left-0 top-0 z-40 h-screen',
        'bg-stone-50 dark:bg-stone-900',
        'border-r border-stone-200 dark:border-stone-800',
        'flex flex-col'
      )}
    >
      {/* Logo & Collapse Toggle */}
      <div className="flex h-16 items-center justify-between px-4 border-b border-stone-200 dark:border-stone-800">
        <AnimatePresence mode="wait">
          {!collapsed && (
            <motion.div
              initial={{ opacity: 0, x: -10 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -10 }}
              transition={{ duration: 0.15 }}
              className="flex items-center gap-3"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-teal-500 to-teal-600 shadow-md">
                <Bot className="h-5 w-5 text-white" />
              </div>
              <div className="flex flex-col">
                <span className="font-display font-semibold text-stone-900 dark:text-stone-100 text-sm">
                  DocAgent
                </span>
                <span className="text-[10px] text-stone-500 dark:text-stone-400 font-medium tracking-wide uppercase">
                  Enterprise
                </span>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {collapsed && (
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-teal-500 to-teal-600 shadow-md mx-auto">
            <Bot className="h-5 w-5 text-white" />
          </div>
        )}

        <button
          onClick={() => setCollapsed(!collapsed)}
          className={cn(
            'flex h-8 w-8 items-center justify-center rounded-md',
            'text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200',
            'hover:bg-stone-200 dark:hover:bg-stone-800',
            'transition-colors duration-150',
            collapsed && 'absolute -right-3 top-6 bg-stone-50 dark:bg-stone-900 border border-stone-200 dark:border-stone-700 shadow-sm'
          )}
        >
          {collapsed ? (
            <ChevronRight className="h-4 w-4" />
          ) : (
            <ChevronLeft className="h-4 w-4" />
          )}
        </button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-4 px-3">
        <div className="space-y-1">
          {navItems.map((item) => {
            const isActive = currentPath === item.href;
            const Icon = item.icon;

            return (
              <button
                key={item.id}
                onClick={() => handleNavClick(item.href)}
                className={cn(
                  'group relative flex w-full items-center gap-3 rounded-lg px-3 py-2.5',
                  'transition-all duration-150',
                  isActive
                    ? 'bg-teal-50 dark:bg-teal-950/50 text-teal-700 dark:text-teal-300'
                    : 'text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-800 hover:text-stone-900 dark:hover:text-stone-200',
                  collapsed && 'justify-center px-0'
                )}
              >
                {isActive && (
                  <motion.div
                    layoutId="activeNav"
                    className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-6 bg-teal-500 rounded-r-full"
                    transition={{ duration: 0.2 }}
                  />
                )}

                <Icon
                  className={cn(
                    'h-5 w-5 flex-shrink-0',
                    isActive && 'text-teal-600 dark:text-teal-400'
                  )}
                />

                <AnimatePresence mode="wait">
                  {!collapsed && (
                    <motion.span
                      initial={{ opacity: 0, width: 0 }}
                      animate={{ opacity: 1, width: 'auto' }}
                      exit={{ opacity: 0, width: 0 }}
                      transition={{ duration: 0.15 }}
                      className="font-medium text-sm whitespace-nowrap"
                    >
                      {item.label}
                    </motion.span>
                  )}
                </AnimatePresence>

                {item.badge && !collapsed && (
                  <span className="ml-auto flex h-5 min-w-5 items-center justify-center rounded-full bg-teal-100 dark:bg-teal-900 px-1.5 text-xs font-semibold text-teal-700 dark:text-teal-300">
                    {item.badge}
                  </span>
                )}

                {item.badge && collapsed && (
                  <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-teal-500 px-1 text-[10px] font-bold text-white">
                    {item.badge}
                  </span>
                )}

                {/* Tooltip for collapsed state */}
                {collapsed && (
                  <div className="absolute left-full ml-2 hidden group-hover:block z-50">
                    <div className="rounded-md bg-stone-900 dark:bg-stone-700 px-2 py-1 text-xs font-medium text-white whitespace-nowrap shadow-lg">
                      {item.label}
                      {item.badge && (
                        <span className="ml-1.5 text-teal-300">({item.badge})</span>
                      )}
                    </div>
                  </div>
                )}
              </button>
            );
          })}
        </div>

        {/* Agent Status Section */}
        <div className="mt-8">
          <AnimatePresence mode="wait">
            {!collapsed && (
              <motion.div
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                className="flex items-center gap-2 px-3 mb-3"
              >
                <Activity className="h-4 w-4 text-stone-400" />
                <span className="text-xs font-semibold text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                  Agents
                </span>
                <div className="flex-1 h-px bg-stone-200 dark:bg-stone-700" />
              </motion.div>
            )}
          </AnimatePresence>

          {collapsed && (
            <div className="flex justify-center mb-3">
              <Activity className="h-4 w-4 text-stone-400" />
            </div>
          )}

          <div className="space-y-1">
            {agents.map((agent) => {
              const Icon = agent.icon;
              const isHovered = hoveredAgent === agent.id;

              return (
                <div
                  key={agent.id}
                  onMouseEnter={() => setHoveredAgent(agent.id)}
                  onMouseLeave={() => setHoveredAgent(null)}
                  className={cn(
                    'group relative flex items-center gap-3 rounded-lg px-3 py-2',
                    'text-stone-600 dark:text-stone-400',
                    'hover:bg-stone-100 dark:hover:bg-stone-800',
                    'transition-colors duration-150',
                    collapsed && 'justify-center px-0'
                  )}
                >
                  <div className="relative">
                    <Icon className="h-4 w-4" />
                    <span
                      className={cn(
                        'absolute -bottom-0.5 -right-0.5 h-2 w-2 rounded-full ring-2 ring-stone-50 dark:ring-stone-900',
                        statusColors[agent.status],
                        agent.status === 'processing' && 'animate-pulse'
                      )}
                    />
                  </div>

                  <AnimatePresence mode="wait">
                    {!collapsed && (
                      <motion.div
                        initial={{ opacity: 0, width: 0 }}
                        animate={{ opacity: 1, width: 'auto' }}
                        exit={{ opacity: 0, width: 0 }}
                        transition={{ duration: 0.15 }}
                        className="flex-1 min-w-0"
                      >
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium truncate">
                            {agent.name}
                          </span>
                          <span
                            className={cn(
                              'text-[10px] font-medium px-1.5 py-0.5 rounded-full',
                              agent.status === 'active' && 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-400',
                              agent.status === 'idle' && 'bg-stone-100 text-stone-500 dark:bg-stone-800 dark:text-stone-400',
                              agent.status === 'processing' && 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-400',
                              agent.status === 'error' && 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-400'
                            )}
                          >
                            {statusLabels[agent.status]}
                          </span>
                        </div>
                        <p className="text-xs text-stone-500 dark:text-stone-500 truncate">
                          {agent.description}
                        </p>
                      </motion.div>
                    )}
                  </AnimatePresence>

                  {/* Tooltip for collapsed state */}
                  {collapsed && (
                    <div className="absolute left-full ml-2 hidden group-hover:block z-50">
                      <div className="rounded-lg bg-stone-900 dark:bg-stone-700 px-3 py-2 shadow-lg">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="text-sm font-medium text-white">
                            {agent.name}
                          </span>
                          <span
                            className={cn(
                              'h-2 w-2 rounded-full',
                              statusColors[agent.status]
                            )}
                          />
                        </div>
                        <p className="text-xs text-stone-300 whitespace-nowrap">
                          {agent.description}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </nav>

      {/* System Status Footer */}
      <div className={cn(
        'border-t border-stone-200 dark:border-stone-800 p-4',
        collapsed && 'px-2'
      )}>
        <AnimatePresence mode="wait">
          {!collapsed ? (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="flex items-center gap-3"
            >
              <div className="flex items-center gap-2">
                <div className="relative">
                  <Zap className="h-4 w-4 text-emerald-500" />
                  <span className="absolute -bottom-0.5 -right-0.5 h-2 w-2 rounded-full bg-emerald-500 ring-2 ring-stone-50 dark:ring-stone-900 animate-pulse" />
                </div>
                <div>
                  <p className="text-xs font-medium text-stone-700 dark:text-stone-300">
                    System Online
                  </p>
                  <p className="text-[10px] text-stone-500 dark:text-stone-500">
                    All services operational
                  </p>
                </div>
              </div>
            </motion.div>
          ) : (
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="flex justify-center"
            >
              <div className="relative">
                <Circle className="h-3 w-3 text-emerald-500 fill-emerald-500" />
                <span className="absolute inset-0 rounded-full bg-emerald-500 animate-ping opacity-25" />
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </motion.aside>
  );
}

export default Sidebar;
