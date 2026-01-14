// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex Table Component
// Data table for displaying MasterIndex entries
// ═══════════════════════════════════════════════════════════════════════════

import { memo, useCallback } from 'react';
import { motion } from 'framer-motion';
import {
  ChevronLeft,
  ChevronRight,
  Database,
  Table2,
  Columns3,
  FileText,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge, Button, Card } from '@/components/ui';
import { getStatusVariant } from '@/components/ui/Badge';
import type { MasterIndexSummary } from '@/types/masterIndex';
import { TierDescriptions } from '@/types/masterIndex';

// ─────────────────────────────────────────────────────────────────────────────
// Loading Skeleton
// ─────────────────────────────────────────────────────────────────────────────

function TableSkeleton() {
  return (
    <div className="animate-pulse">
      <div className="bg-surface-50 rounded-t-lg border-b border-surface-200 p-4">
        <div className="grid grid-cols-7 gap-4">
          {[...Array(7)].map((_, i) => (
            <div key={i} className="h-4 bg-surface-200 rounded" />
          ))}
        </div>
      </div>
      {[...Array(5)].map((_, i) => (
        <div key={i} className="border-b border-surface-100 p-4">
          <div className="grid grid-cols-7 gap-4">
            {[...Array(7)].map((_, j) => (
              <div key={j} className="h-4 bg-surface-100 rounded" />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Table Row
// ─────────────────────────────────────────────────────────────────────────────

interface TableRowProps {
  item: MasterIndexSummary;
  onClick: (id: number) => void;
  index: number;
}

const TableRow = memo(function TableRow({ item, onClick, index }: TableRowProps) {
  const handleClick = useCallback(() => {
    onClick(item.indexId);
  }, [onClick, item.indexId]);

  // Get icon based on object type
  const getObjectIcon = (type: string | null) => {
    switch (type?.toLowerCase()) {
      case 'table':
        return Table2;
      case 'column':
        return Columns3;
      case 'view':
      case 'storedprocedure':
        return FileText;
      default:
        return Database;
    }
  };

  const ObjectIcon = getObjectIcon(item.objectType);

  return (
    <motion.tr
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.2, delay: index * 0.02 }}
      onClick={handleClick}
      className="group cursor-pointer hover:bg-surface-50 transition-colors"
    >
      {/* Object Path */}
      <td className="px-4 py-3">
        <div className="flex items-center gap-2">
          <ObjectIcon className="w-4 h-4 text-surface-400 group-hover:text-brand-500 transition-colors" />
          <div className="flex flex-col">
            <span className="text-sm font-medium text-surface-900 group-hover:text-brand-600 transition-colors">
              {item.tableName || item.objectPath || '-'}
            </span>
            {item.columnName && (
              <span className="text-xs text-surface-500">
                .{item.columnName}
              </span>
            )}
          </div>
        </div>
      </td>

      {/* Database */}
      <td className="px-4 py-3">
        <span className="text-sm text-surface-700">
          {item.databaseName || '-'}
        </span>
      </td>

      {/* Schema */}
      <td className="px-4 py-3">
        <span className="text-sm text-surface-700">
          {item.schemaName || '-'}
        </span>
      </td>

      {/* Object Type */}
      <td className="px-4 py-3">
        <Badge variant="default" size="sm">
          {item.objectType || 'Unknown'}
        </Badge>
      </td>

      {/* Category */}
      <td className="px-4 py-3">
        <span className="text-sm text-surface-700">
          {item.category || '-'}
        </span>
      </td>

      {/* Tier */}
      <td className="px-4 py-3">
        {item.tier ? (
          <span
            className="text-sm text-surface-700"
            title={TierDescriptions[item.tier] || ''}
          >
            Tier {item.tier}
          </span>
        ) : (
          <span className="text-sm text-surface-400">-</span>
        )}
      </td>

      {/* Status */}
      <td className="px-4 py-3">
        {item.approvalStatus ? (
          <Badge
            variant={getStatusVariant(item.approvalStatus)}
            size="sm"
            dot
          >
            {item.approvalStatus}
          </Badge>
        ) : (
          <Badge variant="default" size="sm">
            Unknown
          </Badge>
        )}
      </td>

      {/* Last Modified */}
      <td className="px-4 py-3">
        <span className="text-sm text-surface-500">
          {item.lastModifiedDate
            ? new Date(item.lastModifiedDate).toLocaleDateString()
            : '-'}
        </span>
      </td>
    </motion.tr>
  );
});

// ─────────────────────────────────────────────────────────────────────────────
// Pagination
// ─────────────────────────────────────────────────────────────────────────────

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

function Pagination({
  currentPage,
  totalPages,
  onPageChange,
  hasPreviousPage,
  hasNextPage,
}: PaginationProps) {
  // Generate page numbers to show
  const getPageNumbers = () => {
    const pages: (number | 'ellipsis')[] = [];
    const showPages = 5;

    if (totalPages <= showPages) {
      return Array.from({ length: totalPages }, (_, i) => i + 1);
    }

    // Always show first page
    pages.push(1);

    const start = Math.max(2, currentPage - 1);
    const end = Math.min(totalPages - 1, currentPage + 1);

    if (start > 2) {
      pages.push('ellipsis');
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }

    if (end < totalPages - 1) {
      pages.push('ellipsis');
    }

    // Always show last page
    if (totalPages > 1) {
      pages.push(totalPages);
    }

    return pages;
  };

  return (
    <div className="flex items-center justify-between px-4 py-3 bg-surface-50 rounded-b-lg border-t border-surface-200">
      <Button
        variant="outline"
        size="sm"
        onClick={() => onPageChange(currentPage - 1)}
        disabled={!hasPreviousPage}
        className="gap-1"
      >
        <ChevronLeft className="w-4 h-4" />
        Previous
      </Button>

      <div className="flex items-center gap-1">
        {getPageNumbers().map((page, index) =>
          page === 'ellipsis' ? (
            <span key={`ellipsis-${index}`} className="px-2 text-surface-400">
              ...
            </span>
          ) : (
            <button
              key={page}
              onClick={() => onPageChange(page)}
              className={cn(
                'w-8 h-8 text-sm font-medium rounded-lg transition-colors',
                currentPage === page
                  ? 'bg-brand-500 text-white'
                  : 'text-surface-600 hover:bg-surface-100'
              )}
            >
              {page}
            </button>
          )
        )}
      </div>

      <Button
        variant="outline"
        size="sm"
        onClick={() => onPageChange(currentPage + 1)}
        disabled={!hasNextPage}
        className="gap-1"
      >
        Next
        <ChevronRight className="w-4 h-4" />
      </Button>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Table Component
// ─────────────────────────────────────────────────────────────────────────────

interface MasterIndexTableProps {
  items: MasterIndexSummary[];
  isLoading: boolean;
  onRowClick: (id: number) => void;
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export function MasterIndexTable({
  items,
  isLoading,
  onRowClick,
  currentPage,
  totalPages,
  onPageChange,
  hasPreviousPage,
  hasNextPage,
}: MasterIndexTableProps) {
  if (isLoading) {
    return (
      <Card className="overflow-hidden">
        <TableSkeleton />
      </Card>
    );
  }

  if (items.length === 0) {
    return (
      <Card className="p-12 text-center">
        <Database className="w-12 h-12 mx-auto text-surface-300" />
        <h3 className="mt-4 text-lg font-medium text-surface-900">
          No documents found
        </h3>
        <p className="mt-2 text-sm text-surface-500">
          Try adjusting your search or filter criteria
        </p>
      </Card>
    );
  }

  return (
    <Card className="overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="bg-surface-50 border-b border-surface-200">
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Object
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Database
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Schema
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Type
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Category
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Tier
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Status
              </th>
              <th className="px-4 py-3 text-left text-xs font-semibold text-surface-600 uppercase tracking-wider">
                Modified
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-surface-100">
            {items.map((item, index) => (
              <TableRow
                key={item.indexId}
                item={item}
                onClick={onRowClick}
                index={index}
              />
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <Pagination
          currentPage={currentPage}
          totalPages={totalPages}
          onPageChange={onPageChange}
          hasPreviousPage={hasPreviousPage}
          hasNextPage={hasNextPage}
        />
      )}
    </Card>
  );
}

export default MasterIndexTable;
