// ═══════════════════════════════════════════════════════════════════════════
// Approval Dashboard Component
// Main dashboard for approval workflow with stats, filters, and multiple views
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useMemo, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  LayoutGrid,
  List,
  Kanban,
  Search,
  Filter,
  RefreshCw,
  Clock,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Edit3,
  Loader2,
  Wifi,
  WifiOff,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  useApprovals,
  useApprovalStats,
  usePendingApprovals,
  useOverdueApprovals,
  useApprovalSignalR,
  useApprovalInvalidation,
} from '@/hooks/useApprovals';
import { useApprovalStore } from '@/stores/approvalStore';
import type { Approval, ApprovalStatus, ApprovalFilters } from '@/types/approval';
import { getStatusDisplayName, statusColors } from '@/types/approval';
import { ApprovalCard } from './ApprovalCard';
import { ApprovalTable } from './ApprovalTable';
import { ApprovalKanban } from './ApprovalKanban';
import { ApprovalFiltersPanel } from './ApprovalFiltersPanel';
import { ApprovalDetailsModal } from './ApprovalDetailsModal';
import { RepromptModal } from './RepromptModal';
import { ApproveRejectModal } from './ApproveRejectModal';

// ─────────────────────────────────────────────────────────────────────────────
// Stats Card Component
// ─────────────────────────────────────────────────────────────────────────────

interface StatCardProps {
  title: string;
  value: number;
  icon: React.ReactNode;
  color: string;
  onClick?: () => void;
  isActive?: boolean;
}

function StatCard({ title, value, icon, color, onClick, isActive }: StatCardProps) {
  return (
    <motion.button
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      onClick={onClick}
      className={cn(
        'flex items-center gap-4 p-4 rounded-xl transition-all',
        'bg-white dark:bg-stone-900 border',
        isActive
          ? 'border-teal-500 ring-2 ring-teal-500/20'
          : 'border-stone-200 dark:border-stone-700 hover:border-teal-300'
      )}
    >
      <div
        className={cn(
          'flex items-center justify-center w-12 h-12 rounded-xl',
          color
        )}
      >
        {icon}
      </div>
      <div className="text-left">
        <p className="text-2xl font-bold text-stone-900 dark:text-stone-100">
          {value}
        </p>
        <p className="text-sm text-stone-500 dark:text-stone-400">{title}</p>
      </div>
    </motion.button>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// View Toggle Component
// ─────────────────────────────────────────────────────────────────────────────

interface ViewToggleProps {
  viewMode: 'cards' | 'table' | 'kanban';
  onChange: (mode: 'cards' | 'table' | 'kanban') => void;
}

function ViewToggle({ viewMode, onChange }: ViewToggleProps) {
  const views = [
    { mode: 'cards' as const, icon: <LayoutGrid className="w-4 h-4" />, label: 'Cards' },
    { mode: 'table' as const, icon: <List className="w-4 h-4" />, label: 'Table' },
    { mode: 'kanban' as const, icon: <Kanban className="w-4 h-4" />, label: 'Kanban' },
  ];

  return (
    <div className="flex items-center bg-stone-100 dark:bg-stone-800 rounded-lg p-1">
      {views.map(({ mode, icon, label }) => (
        <button
          key={mode}
          onClick={() => onChange(mode)}
          className={cn(
            'flex items-center gap-2 px-3 py-1.5 rounded-md text-sm font-medium transition-all',
            viewMode === mode
              ? 'bg-white dark:bg-stone-700 text-teal-600 dark:text-teal-400 shadow-sm'
              : 'text-stone-600 dark:text-stone-400 hover:text-stone-900 dark:hover:text-stone-200'
          )}
        >
          {icon}
          <span className="hidden sm:inline">{label}</span>
        </button>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Dashboard Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalDashboard() {
  // Store state
  const {
    filters,
    viewMode,
    showFilters,
    selectedApproval,
    isDetailsModalOpen,
    isRepromptModalOpen,
    isApproveModalOpen,
    isRejectModalOpen,
    setFilters,
    updateFilter,
    resetFilters,
    setViewMode,
    toggleFilters,
    openDetailsModal,
    closeDetailsModal,
    openRepromptModal,
    closeRepromptModal,
    openApproveModal,
    closeApproveModal,
    openRejectModal,
    closeRejectModal,
    setSearchQuery,
  } = useApprovalStore();

  // Queries
  const { data: approvals, isLoading, error, refetch } = useApprovals(filters);
  const { data: stats, isLoading: statsLoading } = useApprovalStats();
  const { data: overdueApprovals } = useOverdueApprovals();

  // SignalR connection
  const { isConnected } = useApprovalSignalR();
  const { invalidateAll } = useApprovalInvalidation();

  // Local state
  const [searchInput, setSearchInput] = useState(filters.searchQuery || '');

  // Debounced search
  const handleSearchChange = useCallback(
    (value: string) => {
      setSearchInput(value);
      // Debounce search
      const timeoutId = setTimeout(() => {
        setSearchQuery(value);
      }, 300);
      return () => clearTimeout(timeoutId);
    },
    [setSearchQuery]
  );

  // Filter by status
  const handleStatusFilter = useCallback(
    (status: ApprovalStatus | null) => {
      if (status === null) {
        updateFilter('status', []);
      } else {
        updateFilter('status', [status]);
      }
    },
    [updateFilter]
  );

  // Stats data
  const statsData = useMemo(
    () => [
      {
        title: 'Pending',
        value: stats?.pending ?? 0,
        icon: <Clock className="w-6 h-6 text-amber-600" />,
        color: 'bg-amber-100 dark:bg-amber-900/30',
        status: 'PendingApproval' as ApprovalStatus,
      },
      {
        title: 'Approved',
        value: stats?.approved ?? 0,
        icon: <CheckCircle2 className="w-6 h-6 text-emerald-600" />,
        color: 'bg-emerald-100 dark:bg-emerald-900/30',
        status: 'Approved' as ApprovalStatus,
      },
      {
        title: 'Rejected',
        value: stats?.rejected ?? 0,
        icon: <XCircle className="w-6 h-6 text-red-600" />,
        color: 'bg-red-100 dark:bg-red-900/30',
        status: 'Rejected' as ApprovalStatus,
      },
      {
        title: 'In Editing',
        value: stats?.editing ?? 0,
        icon: <Edit3 className="w-6 h-6 text-purple-600" />,
        color: 'bg-purple-100 dark:bg-purple-900/30',
        status: 'Editing' as ApprovalStatus,
      },
      {
        title: 'Re-prompt',
        value: stats?.rePromptRequested ?? 0,
        icon: <RefreshCw className="w-6 h-6 text-blue-600" />,
        color: 'bg-blue-100 dark:bg-blue-900/30',
        status: 'RePromptRequested' as ApprovalStatus,
      },
    ],
    [stats]
  );

  // Active status filter
  const activeStatus = filters.status?.[0] ?? null;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-stone-900 dark:text-stone-100">
            Approval Dashboard
          </h1>
          <p className="text-sm text-stone-500 dark:text-stone-400 mt-1">
            Review and approve generated documentation
          </p>
        </div>

        {/* Connection Status */}
        <div className="flex items-center gap-4">
          <div
            className={cn(
              'flex items-center gap-2 px-3 py-1.5 rounded-full text-sm',
              isConnected
                ? 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400'
                : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
            )}
          >
            {isConnected ? (
              <>
                <Wifi className="w-4 h-4" />
                <span>Live</span>
              </>
            ) : (
              <>
                <WifiOff className="w-4 h-4" />
                <span>Offline</span>
              </>
            )}
          </div>

          <button
            onClick={() => invalidateAll()}
            className="p-2 rounded-lg text-stone-500 hover:text-stone-700 hover:bg-stone-100 dark:hover:bg-stone-800 transition-colors"
            title="Refresh"
          >
            <RefreshCw className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        {statsData.map((stat) => (
          <StatCard
            key={stat.title}
            title={stat.title}
            value={stat.value}
            icon={stat.icon}
            color={stat.color}
            isActive={activeStatus === stat.status}
            onClick={() =>
              handleStatusFilter(activeStatus === stat.status ? null : stat.status)
            }
          />
        ))}
      </div>

      {/* Overdue Alert */}
      {overdueApprovals && overdueApprovals.length > 0 && (
        <motion.div
          initial={{ opacity: 0, y: -10 }}
          animate={{ opacity: 1, y: 0 }}
          className="flex items-center gap-3 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-xl"
        >
          <AlertTriangle className="w-5 h-5 text-red-600 dark:text-red-400 flex-shrink-0" />
          <div className="flex-1">
            <p className="font-medium text-red-800 dark:text-red-200">
              {overdueApprovals.length} overdue approval
              {overdueApprovals.length > 1 ? 's' : ''} require attention
            </p>
            <p className="text-sm text-red-600 dark:text-red-400">
              Oldest: {overdueApprovals[0]?.objectName}
            </p>
          </div>
          <button
            onClick={() => handleStatusFilter('PendingApproval')}
            className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
          >
            View Overdue
          </button>
        </motion.div>
      )}

      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        {/* Search */}
        <div className="relative w-full sm:w-96">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-stone-400" />
          <input
            type="text"
            placeholder="Search approvals..."
            value={searchInput}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="w-full pl-10 pr-4 py-2 bg-white dark:bg-stone-900 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
          />
        </div>

        {/* Actions */}
        <div className="flex items-center gap-3">
          <button
            onClick={toggleFilters}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-lg border transition-colors',
              showFilters
                ? 'bg-teal-50 dark:bg-teal-900/30 border-teal-300 dark:border-teal-700 text-teal-700 dark:text-teal-400'
                : 'bg-white dark:bg-stone-900 border-stone-200 dark:border-stone-700 text-stone-600 dark:text-stone-400 hover:border-stone-300'
            )}
          >
            <Filter className="w-4 h-4" />
            <span>Filters</span>
          </button>

          <ViewToggle viewMode={viewMode} onChange={setViewMode} />
        </div>
      </div>

      {/* Filters Panel */}
      <AnimatePresence>
        {showFilters && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="overflow-hidden"
          >
            <ApprovalFiltersPanel
              filters={filters}
              onFilterChange={updateFilter}
              onReset={resetFilters}
            />
          </motion.div>
        )}
      </AnimatePresence>

      {/* Content */}
      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 text-teal-500 animate-spin" />
        </div>
      ) : error ? (
        <div className="text-center py-20">
          <XCircle className="w-12 h-12 text-red-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-stone-800 dark:text-stone-200 mb-2">
            Failed to load approvals
          </h3>
          <p className="text-sm text-stone-500 dark:text-stone-400 mb-4">
            {error instanceof Error ? error.message : 'An error occurred'}
          </p>
          <button
            onClick={() => refetch()}
            className="px-4 py-2 bg-teal-600 text-white rounded-lg hover:bg-teal-700 transition-colors"
          >
            Retry
          </button>
        </div>
      ) : !approvals || approvals.length === 0 ? (
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          className="text-center py-20"
        >
          <CheckCircle2 className="w-16 h-16 text-emerald-400 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-stone-800 dark:text-stone-200 mb-1">
            {activeStatus ? 'No matching approvals' : 'All caught up!'}
          </h3>
          <p className="text-sm text-stone-500 dark:text-stone-400">
            {activeStatus
              ? `No approvals with status "${getStatusDisplayName(activeStatus)}"`
              : 'No pending approvals at the moment'}
          </p>
          {activeStatus && (
            <button
              onClick={() => handleStatusFilter(null)}
              className="mt-4 text-teal-600 dark:text-teal-400 hover:underline text-sm"
            >
              Clear filters
            </button>
          )}
        </motion.div>
      ) : (
        <>
          {viewMode === 'cards' && (
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
              {approvals.map((approval, index) => (
                <ApprovalCard
                  key={approval.id}
                  approval={approval}
                  index={index}
                  onViewDetails={() => openDetailsModal(approval)}
                  onApprove={() => openApproveModal(approval)}
                  onReject={() => openRejectModal(approval)}
                  onReprompt={() => openRepromptModal(approval)}
                />
              ))}
            </div>
          )}

          {viewMode === 'table' && (
            <ApprovalTable
              approvals={approvals}
              onViewDetails={openDetailsModal}
              onApprove={openApproveModal}
              onReject={openRejectModal}
              onReprompt={openRepromptModal}
            />
          )}

          {viewMode === 'kanban' && (
            <ApprovalKanban
              approvals={approvals}
              onViewDetails={openDetailsModal}
              onApprove={openApproveModal}
              onReject={openRejectModal}
              onReprompt={openRepromptModal}
            />
          )}
        </>
      )}

      {/* Modals */}
      <ApprovalDetailsModal
        approval={selectedApproval}
        isOpen={isDetailsModalOpen}
        onClose={closeDetailsModal}
        onApprove={() => selectedApproval && openApproveModal(selectedApproval)}
        onReject={() => selectedApproval && openRejectModal(selectedApproval)}
        onReprompt={() => selectedApproval && openRepromptModal(selectedApproval)}
      />

      <RepromptModal
        approval={selectedApproval}
        isOpen={isRepromptModalOpen}
        onClose={closeRepromptModal}
      />

      <ApproveRejectModal
        approval={selectedApproval}
        mode={isApproveModalOpen ? 'approve' : 'reject'}
        isOpen={isApproveModalOpen || isRejectModalOpen}
        onClose={() => {
          closeApproveModal();
          closeRejectModal();
        }}
      />
    </div>
  );
}

export default ApprovalDashboard;
