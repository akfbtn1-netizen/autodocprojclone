// ═══════════════════════════════════════════════════════════════════════════
// Approval Kanban Component
// Kanban board view for approval workflow visualization
// ═══════════════════════════════════════════════════════════════════════════

import { useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  Clock,
  CheckCircle2,
  XCircle,
  Edit3,
  RefreshCw,
  Eye,
  AlertTriangle,
} from 'lucide-react';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { Approval, ApprovalStatus } from '@/types/approval';
import { getStatusDisplayName, priorityColors } from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface ApprovalKanbanProps {
  approvals: Approval[];
  onViewDetails: (approval: Approval) => void;
  onApprove: (approval: Approval) => void;
  onReject: (approval: Approval) => void;
  onReprompt: (approval: Approval) => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Column Configuration
// ─────────────────────────────────────────────────────────────────────────────

const columns: {
  id: ApprovalStatus;
  title: string;
  icon: React.ReactNode;
  color: string;
  bgColor: string;
}[] = [
  {
    id: 'PendingApproval',
    title: 'Pending',
    icon: <Clock className="w-4 h-4" />,
    color: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-50 dark:bg-amber-900/20',
  },
  {
    id: 'Editing',
    title: 'In Editing',
    icon: <Edit3 className="w-4 h-4" />,
    color: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-50 dark:bg-purple-900/20',
  },
  {
    id: 'RePromptRequested',
    title: 'Re-prompt',
    icon: <RefreshCw className="w-4 h-4" />,
    color: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-50 dark:bg-blue-900/20',
  },
  {
    id: 'Approved',
    title: 'Approved',
    icon: <CheckCircle2 className="w-4 h-4" />,
    color: 'text-emerald-600 dark:text-emerald-400',
    bgColor: 'bg-emerald-50 dark:bg-emerald-900/20',
  },
  {
    id: 'Rejected',
    title: 'Rejected',
    icon: <XCircle className="w-4 h-4" />,
    color: 'text-red-600 dark:text-red-400',
    bgColor: 'bg-red-50 dark:bg-red-900/20',
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// Kanban Card Component
// ─────────────────────────────────────────────────────────────────────────────

interface KanbanCardProps {
  approval: Approval;
  index: number;
  onViewDetails: () => void;
  onApprove?: () => void;
  onReject?: () => void;
  onReprompt?: () => void;
}

function KanbanCard({
  approval,
  index,
  onViewDetails,
  onApprove,
  onReject,
  onReprompt,
}: KanbanCardProps) {
  const isOverdue = approval.dueDate && new Date(approval.dueDate) < new Date();
  const isPending = approval.status === 'PendingApproval';

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.03 }}
      className={cn(
        'bg-white dark:bg-stone-800 rounded-lg border p-3 cursor-pointer',
        'transition-all hover:shadow-md',
        isOverdue && isPending
          ? 'border-red-300 dark:border-red-700'
          : 'border-stone-200 dark:border-stone-700'
      )}
      onClick={onViewDetails}
    >
      {/* Header */}
      <div className="flex items-start justify-between gap-2 mb-2">
        <h4 className="font-medium text-sm text-stone-900 dark:text-stone-100 line-clamp-2">
          {approval.objectName.replace(/_/g, ' ')}
        </h4>
        <span
          className="flex-shrink-0 w-2 h-2 rounded-full mt-1.5"
          style={{ backgroundColor: priorityColors[approval.priority] }}
          title={approval.priority}
        />
      </div>

      {/* Meta */}
      <div className="space-y-1 text-xs text-stone-500 dark:text-stone-400">
        <p className="truncate">{approval.schemaName}.{approval.databaseName}</p>

        {approval.dueDate && (
          <p className={cn(
            'flex items-center gap-1',
            isOverdue && 'text-red-600 dark:text-red-400 font-medium'
          )}>
            {isOverdue && <AlertTriangle className="w-3 h-3" />}
            Due {formatRelativeTime(approval.dueDate)}
          </p>
        )}
      </div>

      {/* Actions for pending */}
      {isPending && (
        <div className="flex items-center gap-1 mt-3 pt-2 border-t border-stone-100 dark:border-stone-700">
          <button
            onClick={(e) => { e.stopPropagation(); onApprove?.(); }}
            className="flex-1 flex items-center justify-center gap-1 px-2 py-1 text-xs bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400 rounded hover:bg-emerald-200 dark:hover:bg-emerald-900/50 transition-colors"
          >
            <CheckCircle2 className="w-3 h-3" />
            Approve
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); onReject?.(); }}
            className="flex-1 flex items-center justify-center gap-1 px-2 py-1 text-xs bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 rounded hover:bg-red-200 dark:hover:bg-red-900/50 transition-colors"
          >
            <XCircle className="w-3 h-3" />
            Reject
          </button>
          <button
            onClick={(e) => { e.stopPropagation(); onReprompt?.(); }}
            className="p-1 text-blue-600 dark:text-blue-400 hover:bg-blue-100 dark:hover:bg-blue-900/30 rounded transition-colors"
            title="Re-prompt"
          >
            <RefreshCw className="w-3 h-3" />
          </button>
        </div>
      )}
    </motion.div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Kanban Column Component
// ─────────────────────────────────────────────────────────────────────────────

interface KanbanColumnProps {
  title: string;
  icon: React.ReactNode;
  color: string;
  bgColor: string;
  approvals: Approval[];
  onViewDetails: (approval: Approval) => void;
  onApprove: (approval: Approval) => void;
  onReject: (approval: Approval) => void;
  onReprompt: (approval: Approval) => void;
}

function KanbanColumn({
  title,
  icon,
  color,
  bgColor,
  approvals,
  onViewDetails,
  onApprove,
  onReject,
  onReprompt,
}: KanbanColumnProps) {
  return (
    <div className="flex flex-col min-w-[280px] max-w-[320px]">
      {/* Column Header */}
      <div className={cn('flex items-center gap-2 px-3 py-2 rounded-t-lg', bgColor)}>
        <span className={color}>{icon}</span>
        <h3 className={cn('font-medium text-sm', color)}>{title}</h3>
        <span className={cn(
          'ml-auto text-xs font-medium px-2 py-0.5 rounded-full',
          bgColor,
          color
        )}>
          {approvals.length}
        </span>
      </div>

      {/* Column Content */}
      <div className={cn(
        'flex-1 p-2 space-y-2 rounded-b-lg min-h-[400px] max-h-[600px] overflow-y-auto',
        'bg-stone-50 dark:bg-stone-900/50 border border-t-0 border-stone-200 dark:border-stone-700'
      )}>
        {approvals.length === 0 ? (
          <div className="flex items-center justify-center h-20 text-sm text-stone-400 dark:text-stone-500">
            No items
          </div>
        ) : (
          approvals.map((approval, index) => (
            <KanbanCard
              key={approval.id}
              approval={approval}
              index={index}
              onViewDetails={() => onViewDetails(approval)}
              onApprove={() => onApprove(approval)}
              onReject={() => onReject(approval)}
              onReprompt={() => onReprompt(approval)}
            />
          ))
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Kanban Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalKanban({
  approvals,
  onViewDetails,
  onApprove,
  onReject,
  onReprompt,
}: ApprovalKanbanProps) {
  // Group approvals by status
  const groupedApprovals = useMemo(() => {
    const groups: Record<ApprovalStatus, Approval[]> = {
      PendingApproval: [],
      Editing: [],
      RePromptRequested: [],
      Approved: [],
      Rejected: [],
    };

    approvals.forEach((approval) => {
      if (groups[approval.status]) {
        groups[approval.status].push(approval);
      }
    });

    // Sort each group by priority and date
    Object.keys(groups).forEach((status) => {
      groups[status as ApprovalStatus].sort((a, b) => {
        const priorityOrder = { High: 0, Medium: 1, Low: 2 };
        const priorityDiff = priorityOrder[a.priority] - priorityOrder[b.priority];
        if (priorityDiff !== 0) return priorityDiff;
        return new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime();
      });
    });

    return groups;
  }, [approvals]);

  return (
    <div className="flex gap-4 overflow-x-auto pb-4">
      {columns.map((column) => (
        <KanbanColumn
          key={column.id}
          title={column.title}
          icon={column.icon}
          color={column.color}
          bgColor={column.bgColor}
          approvals={groupedApprovals[column.id]}
          onViewDetails={onViewDetails}
          onApprove={onApprove}
          onReject={onReject}
          onReprompt={onReprompt}
        />
      ))}
    </div>
  );
}

export default ApprovalKanban;
