import { memo } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { motion } from 'framer-motion';
import {
  FileText,
  Clock,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Sparkles,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Badge } from '@/components/ui';

export interface WorkflowNodeData {
  label: string;
  status: 'waiting' | 'active' | 'completed' | 'error';
  documentCount?: number;
  avgTime?: string;
  description?: string;
  icon?: 'draft' | 'pending' | 'review' | 'approved' | 'completed' | 'rejected';
}

const iconMap = {
  draft: FileText,
  pending: Clock,
  review: AlertCircle,
  approved: CheckCircle2,
  completed: Sparkles,
  rejected: XCircle,
};

const statusColors = {
  waiting: {
    bg: 'bg-surface-50',
    border: 'border-surface-200',
    icon: 'text-surface-400',
    text: 'text-surface-500',
  },
  active: {
    bg: 'bg-brand-50',
    border: 'border-brand-300',
    icon: 'text-brand-600',
    text: 'text-brand-700',
  },
  completed: {
    bg: 'bg-green-50',
    border: 'border-green-300',
    icon: 'text-green-600',
    text: 'text-green-700',
  },
  error: {
    bg: 'bg-red-50',
    border: 'border-red-300',
    icon: 'text-red-600',
    text: 'text-red-700',
  },
};

function WorkflowNodeComponent({ data, selected }: NodeProps<WorkflowNodeData>) {
  const Icon = data.icon ? iconMap[data.icon] : FileText;
  const colors = statusColors[data.status];

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
      className={cn(
        'relative min-w-[200px] rounded-2xl border-2 p-4 transition-all duration-300',
        colors.bg,
        colors.border,
        selected && 'ring-2 ring-brand-500 ring-offset-2',
        data.status === 'active' && 'shadow-glow'
      )}
    >
      {/* Pulse indicator for active nodes */}
      {data.status === 'active' && (
        <motion.div
          className="absolute -inset-px rounded-2xl border-2 border-brand-400"
          animate={{
            opacity: [0.5, 0, 0.5],
            scale: [1, 1.02, 1],
          }}
          transition={{
            duration: 2,
            repeat: Infinity,
            ease: 'easeInOut',
          }}
        />
      )}

      {/* Input Handle */}
      <Handle
        type="target"
        position={Position.Left}
        className="!w-3 !h-3 !bg-brand-500 !border-2 !border-white !shadow-sm"
      />

      {/* Content */}
      <div className="relative z-10">
        {/* Header with icon and label */}
        <div className="flex items-center gap-3 mb-3">
          <div
            className={cn(
              'flex items-center justify-center w-10 h-10 rounded-xl',
              data.status === 'active' ? 'bg-brand-100' : 'bg-white',
              'shadow-sm'
            )}
          >
            <Icon className={cn('w-5 h-5', colors.icon)} />
          </div>
          <div>
            <h3 className="font-display font-semibold text-surface-900">
              {data.label}
            </h3>
            {data.description && (
              <p className="text-xs text-surface-500">{data.description}</p>
            )}
          </div>
        </div>

        {/* Stats */}
        {(data.documentCount !== undefined || data.avgTime) && (
          <div className="flex items-center gap-3 pt-3 border-t border-surface-200/50">
            {data.documentCount !== undefined && (
              <div className="flex items-center gap-1.5">
                <FileText className="w-3.5 h-3.5 text-surface-400" />
                <span className="text-sm font-medium text-surface-700">
                  {data.documentCount}
                </span>
                <span className="text-xs text-surface-400">docs</span>
              </div>
            )}
            {data.avgTime && (
              <div className="flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5 text-surface-400" />
                <span className="text-xs text-surface-500">{data.avgTime}</span>
              </div>
            )}
          </div>
        )}

        {/* Status badge */}
        <div className="absolute -top-2 -right-2">
          {data.status === 'active' && (
            <Badge variant="brand" size="sm" dot pulse>
              Active
            </Badge>
          )}
          {data.status === 'completed' && (
            <Badge variant="success" size="sm">
              Done
            </Badge>
          )}
          {data.status === 'error' && (
            <Badge variant="danger" size="sm">
              Error
            </Badge>
          )}
        </div>
      </div>

      {/* Output Handle */}
      <Handle
        type="source"
        position={Position.Right}
        className="!w-3 !h-3 !bg-brand-500 !border-2 !border-white !shadow-sm"
      />
    </motion.div>
  );
}

export const WorkflowNode = memo(WorkflowNodeComponent);

// Custom edge for animated flow
export const edgeOptions = {
  style: {
    strokeWidth: 2,
    stroke: '#d6d3d1',
  },
  animated: true,
  markerEnd: {
    type: 'arrowclosed' as const,
    color: '#d6d3d1',
  },
};

// Active edge style
export const activeEdgeOptions = {
  style: {
    strokeWidth: 2,
    stroke: '#14b8a6',
  },
  animated: true,
  markerEnd: {
    type: 'arrowclosed' as const,
    color: '#14b8a6',
  },
};
