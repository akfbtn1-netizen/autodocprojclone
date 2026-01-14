// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex Dashboard Component
// Main view for browsing the document catalog
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useCallback, useMemo } from 'react';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import {
  Database,
  FileText,
  Search,
  Filter,
  RefreshCw,
  ChevronDown,
  LayoutGrid,
  List,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Card, Badge, Button, Input } from '@/components/ui';
import { KpiCard, KpiGrid } from '@/components/dashboard/KpiCard';
import { MasterIndexTable } from './MasterIndexTable';
import {
  useMasterIndexData,
  useMasterIndexStatistics,
  useMasterIndexSignalR,
  useMasterIndexInvalidation,
} from '@/hooks/useMasterIndex';
import { useMasterIndexStore } from '@/stores/masterIndexStore';
import { getStatusVariant } from '@/components/ui/Badge';
import type { MasterIndexStatistics } from '@/types/masterIndex';

// ─────────────────────────────────────────────────────────────────────────────
// Statistics Cards
// ─────────────────────────────────────────────────────────────────────────────

interface StatisticsCardsProps {
  statistics: MasterIndexStatistics | undefined;
  isLoading: boolean;
}

function StatisticsCards({ statistics, isLoading }: StatisticsCardsProps) {
  if (isLoading) {
    return (
      <KpiGrid>
        {[...Array(4)].map((_, i) => (
          <Card key={i} className="animate-pulse">
            <div className="h-24 bg-surface-100 rounded" />
          </Card>
        ))}
      </KpiGrid>
    );
  }

  if (!statistics) return null;

  return (
    <KpiGrid>
      <KpiCard
        title="Total Documents"
        value={statistics.totalDocuments.toLocaleString()}
        icon="documents"
        variant="brand"
        delay={0}
      />
      <KpiCard
        title="Draft"
        value={statistics.draftCount.toLocaleString()}
        icon="pending"
        variant="default"
        delay={1}
      />
      <KpiCard
        title="Pending Approval"
        value={statistics.pendingCount.toLocaleString()}
        icon="time"
        variant="warning"
        delay={2}
      />
      <KpiCard
        title="Approved"
        value={statistics.approvedCount.toLocaleString()}
        icon="approved"
        variant="success"
        delay={3}
      />
    </KpiGrid>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Filter Bar
// ─────────────────────────────────────────────────────────────────────────────

interface FilterBarProps {
  onSearch: (query: string) => void;
  onStatusChange: (status: string | undefined) => void;
  onDatabaseChange: (database: string | undefined) => void;
  onTierChange: (tier: string | undefined) => void;
  onRefresh: () => void;
  isLoading: boolean;
  statistics: MasterIndexStatistics | undefined;
  currentFilters: {
    query?: string;
    status?: string;
    database?: string;
    tier?: string;
  };
}

function FilterBar({
  onSearch,
  onStatusChange,
  onDatabaseChange,
  onTierChange,
  onRefresh,
  isLoading,
  statistics,
  currentFilters,
}: FilterBarProps) {
  const [searchInput, setSearchInput] = useState(currentFilters.query || '');

  // Debounced search
  const handleSearchChange = useCallback(
    (value: string) => {
      setSearchInput(value);
      // Debounce the actual search
      const timer = setTimeout(() => {
        onSearch(value);
      }, 300);
      return () => clearTimeout(timer);
    },
    [onSearch]
  );

  // Get unique values from statistics
  const databases = useMemo(
    () => (statistics ? Object.keys(statistics.byDatabase) : []),
    [statistics]
  );
  const tiers = useMemo(
    () => (statistics ? Object.keys(statistics.byTier) : []),
    [statistics]
  );

  return (
    <div className="flex flex-col gap-4 p-4 bg-white rounded-xl border border-surface-200 shadow-sm">
      <div className="flex flex-wrap items-center gap-4">
        {/* Search */}
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-surface-400" />
          <Input
            type="text"
            placeholder="Search documents..."
            value={searchInput}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="pl-10"
          />
        </div>

        {/* Status Filter */}
        <select
          value={currentFilters.status || ''}
          onChange={(e) => onStatusChange(e.target.value || undefined)}
          className="px-3 py-2 text-sm border border-surface-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
        >
          <option value="">All Statuses</option>
          <option value="Draft">Draft</option>
          <option value="Pending">Pending</option>
          <option value="Approved">Approved</option>
          <option value="Rejected">Rejected</option>
        </select>

        {/* Database Filter */}
        <select
          value={currentFilters.database || ''}
          onChange={(e) => onDatabaseChange(e.target.value || undefined)}
          className="px-3 py-2 text-sm border border-surface-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
        >
          <option value="">All Databases</option>
          {databases.map((db) => (
            <option key={db} value={db}>
              {db}
            </option>
          ))}
        </select>

        {/* Tier Filter */}
        <select
          value={currentFilters.tier || ''}
          onChange={(e) => onTierChange(e.target.value || undefined)}
          className="px-3 py-2 text-sm border border-surface-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-brand-500"
        >
          <option value="">All Tiers</option>
          {tiers.map((tier) => (
            <option key={tier} value={tier}>
              Tier {tier}
            </option>
          ))}
        </select>

        {/* Refresh Button */}
        <Button
          variant="outline"
          size="sm"
          onClick={onRefresh}
          disabled={isLoading}
          className="gap-2"
        >
          <RefreshCw className={cn('w-4 h-4', isLoading && 'animate-spin')} />
          Refresh
        </Button>
      </div>

      {/* Active Filters */}
      {(currentFilters.query ||
        currentFilters.status ||
        currentFilters.database ||
        currentFilters.tier) && (
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-surface-500">Active filters:</span>
          {currentFilters.query && (
            <Badge variant="brand" size="sm">
              Search: {currentFilters.query}
            </Badge>
          )}
          {currentFilters.status && (
            <Badge variant={getStatusVariant(currentFilters.status)} size="sm">
              {currentFilters.status}
            </Badge>
          )}
          {currentFilters.database && (
            <Badge variant="info" size="sm">
              DB: {currentFilters.database}
            </Badge>
          )}
          {currentFilters.tier && (
            <Badge variant="default" size="sm">
              Tier {currentFilters.tier}
            </Badge>
          )}
        </div>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Dashboard Component
// ─────────────────────────────────────────────────────────────────────────────

export function MasterIndexDashboard() {
  const navigate = useNavigate();

  // Store state
  const filters = useMasterIndexStore((state) => state.filters);
  const setQuery = useMasterIndexStore((state) => state.setQuery);
  const setStatus = useMasterIndexStore((state) => state.setStatus);
  const setDatabase = useMasterIndexStore((state) => state.setDatabase);
  const setTier = useMasterIndexStore((state) => state.setTier);
  const setSelectedId = useMasterIndexStore((state) => state.setSelectedId);
  const setPageNumber = useMasterIndexStore((state) => state.setPageNumber);

  // Data hooks
  const {
    items,
    totalCount,
    totalPages,
    isLoading,
    isFetching,
    refetch,
    pageNumber,
    pageSize,
    hasPreviousPage,
    hasNextPage,
  } = useMasterIndexData();

  const { data: statistics, isLoading: isStatisticsLoading } =
    useMasterIndexStatistics();

  // SignalR for real-time updates
  const { isConnected } = useMasterIndexSignalR();
  const { invalidateAll } = useMasterIndexInvalidation();

  // Handlers
  const handleRowClick = useCallback(
    (id: number) => {
      setSelectedId(id);
      navigate(`/catalog/${id}`);
    },
    [navigate, setSelectedId]
  );

  const handleRefresh = useCallback(() => {
    invalidateAll();
    refetch();
  }, [invalidateAll, refetch]);

  const handlePageChange = useCallback(
    (page: number) => {
      setPageNumber(page);
    },
    [setPageNumber]
  );

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="space-y-6 p-6"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-surface-900">
            Document Catalog
          </h1>
          <p className="text-sm text-surface-500 mt-1">
            Browse and search the MasterIndex documentation catalog
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* SignalR Connection Status */}
          <div className="flex items-center gap-2 text-sm">
            <span
              className={cn(
                'w-2 h-2 rounded-full',
                isConnected ? 'bg-green-500' : 'bg-red-500'
              )}
            />
            <span className="text-surface-500">
              {isConnected ? 'Live' : 'Offline'}
            </span>
          </div>
        </div>
      </div>

      {/* Statistics Cards */}
      <StatisticsCards
        statistics={statistics}
        isLoading={isStatisticsLoading}
      />

      {/* Filter Bar */}
      <FilterBar
        onSearch={setQuery}
        onStatusChange={setStatus}
        onDatabaseChange={setDatabase}
        onTierChange={setTier}
        onRefresh={handleRefresh}
        isLoading={isFetching}
        statistics={statistics}
        currentFilters={{
          query: filters.query,
          status: filters.status,
          database: filters.database,
          tier: filters.tier,
        }}
      />

      {/* Results Summary */}
      <div className="flex items-center justify-between text-sm text-surface-500">
        <span>
          Showing {items.length} of {totalCount.toLocaleString()} documents
        </span>
        <span>
          Page {pageNumber} of {totalPages || 1}
        </span>
      </div>

      {/* Data Table */}
      <MasterIndexTable
        items={items}
        isLoading={isLoading}
        onRowClick={handleRowClick}
        currentPage={pageNumber}
        totalPages={totalPages}
        onPageChange={handlePageChange}
        hasPreviousPage={hasPreviousPage}
        hasNextPage={hasNextPage}
      />
    </motion.div>
  );
}

export default MasterIndexDashboard;
