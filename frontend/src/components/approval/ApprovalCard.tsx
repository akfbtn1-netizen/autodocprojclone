// ═══════════════════════════════════════════════════════════════════════════
// Approval Card Component
// Individual card for approval items with actions
// ═══════════════════════════════════════════════════════════════════════════

import { motion } from 'framer-motion';
import {
  Clock,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  RefreshCw,
  Eye,
  FileText,
  Database,
  User,
  Calendar,
  Star,
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

interface ApprovalCardProps {
  approval: Approval;
  index: number;
  onViewDetails: () => void;
  onApprove: () => void;
  onReject: () => void;
  onReprompt: () => void;
}

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
        'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium',
        variantStyles[variant]
      )}
    >
      {displayName}
    </span>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Priority Indicator Component
// ─────────────────────────────────────────────────────────────────────────────

function PriorityIndicator({ priority }: { priority: Approval['priority'] }) {
  const color = priorityColors[priority];

  return (
    <span
      className="inline-flex items-center gap-1 text-xs font-medium"
      style={{ color }}
    >
      <span
        className="w-2 h-2 rounded-full"
        style={{ backgroundColor: color }}
      />
      {priority}
    </span>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Quality Rating Component
// ─────────────────────────────────────────────────────────────────────────────

function QualityRating({ rating }: { rating?: number }) {
  if (!rating) return null;

  return (
    <div className="flex items-center gap-1">
      {[1, 2, 3, 4, 5].map((star) => (
        <Star
          key={star}
          className={cn(
            'w-3 h-3',
            star <= rating
              ? 'text-amber-400 fill-amber-400'
              : 'text-stone-300 dark:text-stone-600'
          )}
        />
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Card Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalCard({
  approval,
  index,
  onViewDetails,
  onApprove,
  onReject,
  onReprompt,
}: ApprovalCardProps) {
  const isOverdue = approval.dueDate && new Date(approval.dueDate) < new Date();
  const isPending = approval.status === 'PendingApproval';

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'bg-white dark:bg-stone-900 rounded-xl border overflow-hidden transition-all hover:shadow-lg',
        isOverdue && isPending
          ? 'border-red-300 dark:border-red-800'
          : 'border-stone-200 dark:border-stone-700'
      )}
    >
      {/* Header */}
      <div className="p-4 border-b border-stone-100 dark:border-stone-800">
        <div className="flex items-start justify-between gap-3 mb-3">
          <div className="flex-1 min-w-0">
            <h3 className="font-semibold text-stone-900 dark:text-stone-100 truncate">
              {approval.objectName.replace(/_/g, ' ')}
            </h3>
            <p className="text-xs text-stone-500 dark:text-stone-400 truncate mt-1">
              {approval.schemaName}.{approval.tableName || approval.objectName}
            </p>
          </div>
          <StatusBadge status={approval.status} />
        </div>

        {/* Meta info */}
        <div className="flex flex-wrap items-center gap-3 text-xs text-stone-500 dark:text-stone-400">
          <span className="flex items-center gap-1">
            <Database className="w-3 h-3" />
            {approval.databaseName}
          </span>
          <span className="flex items-center gap-1">
            <FileText className="w-3 h-3" />
            {DocumentTypeLabels[approval.documentType] || approval.documentType}
          </span>
          <PriorityIndicator priority={approval.priority} />
        </div>
      </div>

      {/* Content */}
      <div className="p-4 space-y-3">
        {/* CAB & JIRA */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-stone-500 dark:text-stone-400">CAB#</span>
          <span className="font-mono text-stone-700 dark:text-stone-300">
            {approval.cabNumber || 'N/A'}
          </span>
        </div>

        {approval.jiraNumber && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-stone-500 dark:text-stone-400">JIRA</span>
            <span className="font-mono text-teal-600 dark:text-teal-400">
              {approval.jiraNumber}
            </span>
          </div>
        )}

        {/* Requested by */}
        <div className="flex items-center justify-between text-sm">
          <span className="flex items-center gap-1 text-stone-500 dark:text-stone-400">
            <User className="w-3 h-3" />
            Requested by
          </span>
          <span className="text-stone-700 dark:text-stone-300">
            {approval.requestedBy}
          </span>
        </div>

        {/* Dates */}
        <div className="flex items-center justify-between text-sm">
          <span className="flex items-center gap-1 text-stone-500 dark:text-stone-400">
            <Clock className="w-3 h-3" />
            Requested
          </span>
          <span className="text-stone-700 dark:text-stone-300">
            {formatRelativeTime(approval.requestedAt)}
          </span>
        </div>

        {approval.dueDate && (
          <div className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-1 text-stone-500 dark:text-stone-400">
              <Calendar className="w-3 h-3" />
              Due
            </span>
            <span
              className={cn(
                isOverdue
                  ? 'text-red-600 dark:text-red-400 font-medium'
                  : 'text-stone-700 dark:text-stone-300'
              )}
            >
              {formatRelativeTime(approval.dueDate)}
              {isOverdue && (
                <AlertTriangle className="inline w-3 h-3 ml-1" />
              )}
            </span>
          </div>
        )}

        {/* Quality Rating */}
        {approval.qualityRating && (
          <div className="flex items-center justify-between">
            <span className="text-sm text-stone-500 dark:text-stone-400">Quality</span>
            <QualityRating rating={approval.qualityRating} />
          </div>
        )}

        {/* Change Description */}
        {approval.changeDescription && (
          <p className="text-sm text-stone-600 dark:text-stone-400 line-clamp-2 pt-2 border-t border-stone-100 dark:border-stone-800">
            {approval.changeDescription}
          </p>
        )}
      </div>

      {/* Actions */}
      {isPending && (
        <div className="p-4 bg-stone-50 dark:bg-stone-800/50 border-t border-stone-100 dark:border-stone-800">
          <div className="flex items-center gap-2">
            <button
              onClick={onApprove}
              className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 transition-colors text-sm font-medium"
            >
              <CheckCircle2 className="w-4 h-4" />
              Approve
            </button>
            <button
              onClick={onReject}
              className="flex-1 flex items-center justify-center gap-1.5 px-3 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
            >
              <XCircle className="w-4 h-4" />
              Reject
            </button>
            <button
              onClick={onReprompt}
              className="px-3 py-2 border border-stone-300 dark:border-stone-600 text-stone-700 dark:text-stone-300 rounded-lg hover:bg-stone-100 dark:hover:bg-stone-700 transition-colors"
              title="Request Re-generation"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={onViewDetails}
              className="px-3 py-2 border border-stone-300 dark:border-stone-600 text-stone-700 dark:text-stone-300 rounded-lg hover:bg-stone-100 dark:hover:bg-stone-700 transition-colors"
              title="View Details"
            >
              <Eye className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {/* Non-pending: just view details */}
      {!isPending && (
        <div className="p-4 border-t border-stone-100 dark:border-stone-800">
          <button
            onClick={onViewDetails}
            className="w-full flex items-center justify-center gap-2 px-4 py-2 border border-stone-300 dark:border-stone-600 text-stone-700 dark:text-stone-300 rounded-lg hover:bg-stone-100 dark:hover:bg-stone-800 transition-colors text-sm"
          >
            <Eye className="w-4 h-4" />
            View Details
          </button>
        </div>
      )}
    </motion.div>
  );
}

export default ApprovalCard;
