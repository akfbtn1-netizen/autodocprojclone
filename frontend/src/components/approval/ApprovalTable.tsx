// ═══════════════════════════════════════════════════════════════════════════
// Approval Table Component
// Table view for approval items with sorting and inline actions
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  ChevronUp,
  ChevronDown,
  CheckCircle2,
  XCircle,
  RefreshCw,
  Eye,
  AlertTriangle,
  MoreHorizontal,
} from 'lucide-react';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { Approval } from '@/types/approval';
import {
  getStatusDisplayName,
  getStatusVariant,
  DocumentTypeLabels,
  priorityColors,
} from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface ApprovalTableProps {
  approvals: Approval[];
  onViewDetails: (approval: Approval) => void;
  onApprove: (approval: Approval) => void;
  onReject: (approval: Approval) => void;
  onReprompt: (approval: Approval) => void;
}

type SortField = 'objectName' | 'status' | 'priority' | 'requestedAt' | 'dueDate';
type SortDirection = 'asc' | 'desc';

// ─────────────────────────────────────────────────────────────────────────────
// Status Badge Component
// ─────────────────────────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: Approval['status'] }) {
  const variant = getStatusVariant(status);
  const displayName = getStatusDisplayName(status);

  const variantStyles = {
    warning: 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400',
    success: 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400',
    danger: 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400',
    brand: 'bg-teal-100 dark:bg-teal-900/30 text-teal-700 dark:text-teal-400',
    default: 'bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400',
  };

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium',
        variantStyles[variant]
      )}
    >
      {displayName}
    </span>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Sort Header Component
// ─────────────────────────────────────────────────────────────────────────────

interface SortHeaderProps {
  label: string;
  field: SortField;
  sortField: SortField;
  sortDirection: SortDirection;
  onSort: (field: SortField) => void;
}

function SortHeader({ label, field, sortField, sortDirection, onSort }: SortHeaderProps) {
  const isActive = sortField === field;

  return (
    <button
      onClick={() => onSort(field)}
      className="flex items-center gap-1 text-left font-medium text-stone-700 dark:text-stone-300 hover:text-stone-900 dark:hover:text-stone-100 transition-colors"
    >
      {label}
      {isActive && (
        sortDirection === 'asc' ? (
          <ChevronUp className="w-4 h-4" />
        ) : (
          <ChevronDown className="w-4 h-4" />
        )
      )}
    </button>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Table Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalTable({
  approvals,
  onViewDetails,
  onApprove,
  onReject,
  onReprompt,
}: ApprovalTableProps) {
  const [sortField, setSortField] = useState<SortField>('requestedAt');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');
  const [expandedRow, setExpandedRow] = useState<number | null>(null);

  // Handle sort
  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  // Sorted approvals
  const sortedApprovals = useMemo(() => {
    const sorted = [...approvals].sort((a, b) => {
      let comparison = 0;

      switch (sortField) {
        case 'objectName':
          comparison = a.objectName.localeCompare(b.objectName);
          break;
        case 'status':
          comparison = a.status.localeCompare(b.status);
          break;
        case 'priority': {
          const priorityOrder = { High: 0, Medium: 1, Low: 2 };
          comparison = priorityOrder[a.priority] - priorityOrder[b.priority];
          break;
        }
        case 'requestedAt':
          comparison = new Date(a.requestedAt).getTime() - new Date(b.requestedAt).getTime();
          break;
        case 'dueDate':
          if (!a.dueDate && !b.dueDate) comparison = 0;
          else if (!a.dueDate) comparison = 1;
          else if (!b.dueDate) comparison = -1;
          else comparison = new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
          break;
      }

      return sortDirection === 'asc' ? comparison : -comparison;
    });

    return sorted;
  }, [approvals, sortField, sortDirection]);

  return (
    <div className="bg-white dark:bg-stone-900 rounded-xl border border-stone-200 dark:border-stone-700 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-stone-50 dark:bg-stone-800 border-b border-stone-200 dark:border-stone-700">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                <SortHeader
                  label="Object"
                  field="objectName"
                  sortField={sortField}
                  sortDirection={sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                Type
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                <SortHeader
                  label="Status"
                  field="status"
                  sortField={sortField}
                  sortDirection={sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                <SortHeader
                  label="Priority"
                  field="priority"
                  sortField={sortField}
                  sortDirection={sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                <SortHeader
                  label="Requested"
                  field="requestedAt"
                  sortField={sortField}
                  sortDirection={sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                <SortHeader
                  label="Due"
                  field="dueDate"
                  sortField={sortField}
                  sortDirection={sortDirection}
                  onSort={handleSort}
                />
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider">
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-stone-100 dark:divide-stone-800">
            {sortedApprovals.map((approval, index) => {
              const isOverdue = approval.dueDate && new Date(approval.dueDate) < new Date();
              const isPending = approval.status === 'PendingApproval';
              const isExpanded = expandedRow === approval.id;

              return (
                <motion.tr
                  key={approval.id}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ delay: index * 0.02 }}
                  className={cn(
                    'transition-colors',
                    isOverdue && isPending
                      ? 'bg-red-50/50 dark:bg-red-900/10'
                      : 'hover:bg-stone-50 dark:hover:bg-stone-800/50'
                  )}
                >
                  <td className="px-4 py-3">
                    <div>
                      <p className="font-medium text-stone-900 dark:text-stone-100 truncate max-w-xs">
                        {approval.objectName.replace(/_/g, ' ')}
                      </p>
                      <p className="text-xs text-stone-500 dark:text-stone-400 truncate">
                        {approval.schemaName}.{approval.databaseName}
                      </p>
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <span className="text-sm text-stone-600 dark:text-stone-400">
                      {DocumentTypeLabels[approval.documentType] || approval.documentType}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={approval.status} />
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className="inline-flex items-center gap-1.5 text-sm font-medium"
                      style={{ color: priorityColors[approval.priority] }}
                    >
                      <span
                        className="w-2 h-2 rounded-full"
                        style={{ backgroundColor: priorityColors[approval.priority] }}
                      />
                      {approval.priority}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-sm text-stone-600 dark:text-stone-400">
                    {formatRelativeTime(approval.requestedAt)}
                  </td>
                  <td className="px-4 py-3">
                    {approval.dueDate ? (
                      <span
                        className={cn(
                          'text-sm flex items-center gap-1',
                          isOverdue
                            ? 'text-red-600 dark:text-red-400 font-medium'
                            : 'text-stone-600 dark:text-stone-400'
                        )}
                      >
                        {formatRelativeTime(approval.dueDate)}
                        {isOverdue && <AlertTriangle className="w-3 h-3" />}
                      </span>
                    ) : (
                      <span className="text-sm text-stone-400">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-1">
                      {isPending ? (
                        <>
                          <button
                            onClick={() => onApprove(approval)}
                            className="p-1.5 text-emerald-600 hover:bg-emerald-100 dark:hover:bg-emerald-900/30 rounded transition-colors"
                            title="Approve"
                          >
                            <CheckCircle2 className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => onReject(approval)}
                            className="p-1.5 text-red-600 hover:bg-red-100 dark:hover:bg-red-900/30 rounded transition-colors"
                            title="Reject"
                          >
                            <XCircle className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => onReprompt(approval)}
                            className="p-1.5 text-blue-600 hover:bg-blue-100 dark:hover:bg-blue-900/30 rounded transition-colors"
                            title="Re-prompt"
                          >
                            <RefreshCw className="w-4 h-4" />
                          </button>
                        </>
                      ) : null}
                      <button
                        onClick={() => onViewDetails(approval)}
                        className="p-1.5 text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-700 rounded transition-colors"
                        title="View Details"
                      >
                        <Eye className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </motion.tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {/* Empty state */}
      {sortedApprovals.length === 0 && (
        <div className="text-center py-12">
          <p className="text-stone-500 dark:text-stone-400">No approvals to display</p>
        </div>
      )}
    </div>
  );
}

export default ApprovalTable;
