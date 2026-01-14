import { motion } from 'framer-motion';
import {
  Clock,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  MessageSquare,
  ChevronRight,
} from 'lucide-react';
import { formatRelativeTime, cn } from '@/lib/utils';
import { Card, CardHeader, CardTitle, Button, Badge, Avatar } from '@/components/ui';
import type { ApprovalRequest } from '@/types';

interface ApprovalQueueProps {
  approvals: ApprovalRequest[];
  onApprove: (id: string) => void;
  onReject: (id: string) => void;
  onViewDetails?: (id: string) => void;
  isLoading?: boolean;
}

const priorityStyles = {
  low: 'bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400',
  medium: 'bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400',
  high: 'bg-orange-100 dark:bg-orange-900/30 text-orange-600 dark:text-orange-400',
  urgent: 'bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 animate-pulse',
};

export function ApprovalQueue({
  approvals,
  onApprove,
  onReject,
  onViewDetails,
  isLoading,
}: ApprovalQueueProps) {
  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Pending Approvals</CardTitle>
        </CardHeader>
        <div className="space-y-4 mt-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="animate-pulse p-4 rounded-xl bg-stone-50 dark:bg-stone-800">
              <div className="h-4 bg-stone-200 dark:bg-stone-700 rounded w-3/4 mb-2" />
              <div className="h-3 bg-stone-100 dark:bg-stone-700 rounded w-1/2" />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  if (approvals.length === 0) {
    return (
      <Card className="text-center py-10">
        <motion.div
          initial={{ scale: 0.8, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
        >
          <CheckCircle2 className="w-16 h-16 mx-auto text-emerald-400 mb-4" />
          <h3 className="text-lg font-semibold text-stone-800 dark:text-stone-200 mb-1">
            All caught up!
          </h3>
          <p className="text-sm text-stone-500 dark:text-stone-400">
            No pending approvals at the moment
          </p>
        </motion.div>
      </Card>
    );
  }

  return (
    <div className="divide-y divide-stone-100 dark:divide-stone-800">
      {approvals.map((approval, index) => (
        <ApprovalCard
          key={approval.id}
          approval={approval}
          index={index}
          onApprove={onApprove}
          onReject={onReject}
          onViewDetails={onViewDetails}
        />
      ))}
    </div>
  );
}

// Individual approval card
interface ApprovalCardProps {
  approval: ApprovalRequest;
  index: number;
  onApprove: (id: string) => void;
  onReject: (id: string) => void;
  onViewDetails?: (id: string) => void;
}

function ApprovalCard({
  approval,
  index,
  onApprove,
  onReject,
  onViewDetails,
}: ApprovalCardProps) {
  const isUrgent = approval.priority === 'urgent';
  const isOverdue = approval.dueDate && new Date(approval.dueDate) < new Date();

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'p-4 transition-colors',
        isUrgent && 'bg-red-50/50 dark:bg-red-900/10'
      )}
    >
      <div className="flex items-start gap-4">
        {/* Priority indicator */}
        <div
          className={cn(
            'flex items-center justify-center w-10 h-10 rounded-xl flex-shrink-0',
            priorityStyles[approval.priority]
          )}
        >
          {isUrgent ? (
            <AlertTriangle className="w-5 h-5" />
          ) : (
            <Clock className="w-5 h-5" />
          )}
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h3 className="font-medium text-stone-900 dark:text-stone-100 mb-1">
                {approval.documentTitle.replace(/_/g, ' ')}
              </h3>
              <div className="flex items-center flex-wrap gap-3 text-xs text-stone-500 dark:text-stone-400">
                <span className="flex items-center gap-1.5">
                  <Avatar name={approval.requester.name} size="xs" />
                  {approval.requester.name}
                </span>
                <span className="flex items-center gap-1">
                  <Clock className="w-3 h-3" />
                  {formatRelativeTime(approval.requestedAt)}
                </span>
                {approval.dueDate && (
                  <span
                    className={cn(
                      'flex items-center gap-1',
                      isOverdue && 'text-red-600 dark:text-red-400 font-medium'
                    )}
                  >
                    {isOverdue ? (
                      <AlertTriangle className="w-3 h-3" />
                    ) : (
                      <Clock className="w-3 h-3" />
                    )}
                    Due {formatRelativeTime(approval.dueDate)}
                  </span>
                )}
              </div>
            </div>

            <Badge
              variant={
                approval.priority === 'urgent'
                  ? 'danger'
                  : approval.priority === 'high'
                  ? 'warning'
                  : 'default'
              }
              size="sm"
            >
              {approval.priority}
            </Badge>
          </div>

          {/* Actions */}
          <div className="flex items-center gap-2 mt-3">
            <Button
              variant="success"
              size="sm"
              onClick={() => onApprove(approval.id)}
              leftIcon={<CheckCircle2 className="w-4 h-4" />}
            >
              Approve
            </Button>
            <Button
              variant="danger"
              size="sm"
              onClick={() => onReject(approval.id)}
              leftIcon={<XCircle className="w-4 h-4" />}
            >
              Reject
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onViewDetails?.(approval.id)}
              leftIcon={<MessageSquare className="w-4 h-4" />}
            >
              Comment
            </Button>
            <div className="flex-1" />
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onViewDetails?.(approval.id)}
              rightIcon={<ChevronRight className="w-4 h-4" />}
            >
              Details
            </Button>
          </div>
        </div>
      </div>
    </motion.div>
  );
}

export default ApprovalQueue;
