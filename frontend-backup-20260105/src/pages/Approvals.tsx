import { useState, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  CheckCircle2,
  XCircle,
  Clock,
  AlertTriangle,
  MessageSquare,
  ChevronRight,
  Filter,
  Search,
  Calendar,
  User,
  FileText,
  ExternalLink,
} from 'lucide-react';
import {
  Button,
  Input,
  Badge,
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  Modal,
  Textarea,
} from '@/components/ui';
import { Select } from '@/components/ui/Dropdown';
import { Avatar } from '@/components/ui/Avatar';
import { cn, formatRelativeTime, formatDateTime } from '@/lib/utils';
import type { ApprovalRequest } from '@/types';

// Extended mock data
const mockApprovals: ApprovalRequest[] = [
  {
    id: 'apr-001',
    documentId: 'doc-001',
    documentTitle: 'IRF_Policy_Updates_Q4',
    requester: { id: 'user-1', name: 'Sarah Chen', email: 'sarah.chen@company.com' },
    requestedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
    priority: 'high',
    status: 'pending',
    comments: 'Please review the updated policy documentation for Q4 compliance changes. This includes updates to sections 3.2, 4.1, and 5.6 based on new regulatory requirements.',
  },
  {
    id: 'apr-002',
    documentId: 'doc-002',
    documentTitle: 'Claims_Processing_Schema',
    requester: { id: 'user-2', name: 'Mike Johnson', email: 'mike.johnson@company.com' },
    requestedAt: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString(),
    priority: 'medium',
    status: 'pending',
    comments: 'Schema documentation ready for technical review. Major changes include new field definitions for claim categorization.',
  },
  {
    id: 'apr-003',
    documentId: 'doc-006',
    documentTitle: 'Security_Audit_Procedures',
    requester: { id: 'user-4', name: 'Alex Rivera', email: 'alex.rivera@company.com' },
    requestedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() + 4 * 60 * 60 * 1000).toISOString(),
    priority: 'urgent',
    status: 'pending',
    comments: 'Urgent: Security audit documentation needs immediate approval before SOC2 certification deadline.',
  },
  {
    id: 'apr-004',
    documentId: 'doc-007',
    documentTitle: 'Data_Migration_Guide',
    requester: { id: 'user-6', name: 'Lisa Park', email: 'lisa.park@company.com' },
    requestedAt: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() + 72 * 60 * 60 * 1000).toISOString(),
    priority: 'low',
    status: 'pending',
    comments: 'Migration documentation for planned database update scheduled for next quarter.',
  },
  {
    id: 'apr-005',
    documentId: 'doc-009',
    documentTitle: 'Customer_Analytics_Report',
    requester: { id: 'user-1', name: 'Sarah Chen', email: 'sarah.chen@company.com' },
    requestedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString(),
    priority: 'high',
    status: 'pending',
    comments: 'Quarterly analytics report ready for stakeholder review.',
  },
];

const completedApprovals: ApprovalRequest[] = [
  {
    id: 'apr-101',
    documentId: 'doc-003',
    documentTitle: 'Customer_Data_Lineage',
    requester: { id: 'user-4', name: 'Alex Rivera', email: 'alex.rivera@company.com' },
    requestedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    priority: 'medium',
    status: 'approved',
    comments: 'Lineage documentation approved after technical review.',
    reviewedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    reviewer: { id: 'user-3', name: 'Emily Davis', email: 'emily.davis@company.com' },
  },
  {
    id: 'apr-102',
    documentId: 'doc-005',
    documentTitle: 'Compliance_Report_Generator',
    requester: { id: 'user-1', name: 'Sarah Chen', email: 'sarah.chen@company.com' },
    requestedAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    priority: 'high',
    status: 'approved',
    comments: 'Report generator documentation meets all compliance standards.',
    reviewedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    reviewer: { id: 'user-5', name: 'James Wilson', email: 'james.wilson@company.com' },
  },
  {
    id: 'apr-103',
    documentId: 'doc-010',
    documentTitle: 'API_Rate_Limiting_Spec',
    requester: { id: 'user-2', name: 'Mike Johnson', email: 'mike.johnson@company.com' },
    requestedAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
    dueDate: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    priority: 'low',
    status: 'rejected',
    comments: 'Needs additional details on edge cases and error handling.',
    reviewedAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    reviewer: { id: 'user-3', name: 'Emily Davis', email: 'emily.davis@company.com' },
    rejectionReason: 'Missing documentation for rate limit bypass scenarios and webhook integration.',
  },
];

const priorityConfig = {
  low: { label: 'Low', color: 'bg-stone-100 text-stone-600 dark:bg-stone-800 dark:text-stone-400' },
  medium: { label: 'Medium', color: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-400' },
  high: { label: 'High', color: 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-400' },
  urgent: { label: 'Urgent', color: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-400' },
};

const statusOptions = [
  { value: 'all', label: 'All Status' },
  { value: 'pending', label: 'Pending' },
  { value: 'approved', label: 'Approved' },
  { value: 'rejected', label: 'Rejected' },
];

const priorityOptions = [
  { value: 'all', label: 'All Priority' },
  { value: 'urgent', label: 'Urgent' },
  { value: 'high', label: 'High' },
  { value: 'medium', label: 'Medium' },
  { value: 'low', label: 'Low' },
];

export function Approvals() {
  const [tab, setTab] = useState<'pending' | 'completed'>('pending');
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [priorityFilter, setPriorityFilter] = useState('all');
  const [selectedApproval, setSelectedApproval] = useState<ApprovalRequest | null>(null);
  const [actionModal, setActionModal] = useState<{ type: 'approve' | 'reject'; approval: ApprovalRequest } | null>(null);
  const [actionComment, setActionComment] = useState('');

  const allApprovals = [...mockApprovals, ...completedApprovals];

  const filteredApprovals = useMemo(() => {
    let result = tab === 'pending' ? mockApprovals : completedApprovals;

    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      result = result.filter(
        (apr) =>
          apr.documentTitle.toLowerCase().includes(query) ||
          apr.requester.name.toLowerCase().includes(query)
      );
    }

    if (statusFilter !== 'all' && tab === 'completed') {
      result = result.filter((apr) => apr.status === statusFilter);
    }

    if (priorityFilter !== 'all') {
      result = result.filter((apr) => apr.priority === priorityFilter);
    }

    // Sort by priority then date
    const priorityOrder = { urgent: 0, high: 1, medium: 2, low: 3 };
    result.sort((a, b) => {
      const priorityDiff = priorityOrder[a.priority] - priorityOrder[b.priority];
      if (priorityDiff !== 0) return priorityDiff;
      return new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime();
    });

    return result;
  }, [tab, searchQuery, statusFilter, priorityFilter]);

  const handleApprove = () => {
    if (actionModal) {
      console.log('Approved:', actionModal.approval.id, 'Comment:', actionComment);
      setActionModal(null);
      setActionComment('');
    }
  };

  const handleReject = () => {
    if (actionModal) {
      console.log('Rejected:', actionModal.approval.id, 'Reason:', actionComment);
      setActionModal(null);
      setActionComment('');
    }
  };

  const isOverdue = (dueDate: string) => new Date(dueDate) < new Date();

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-semibold text-stone-900 dark:text-stone-100">
            Approvals
          </h1>
          <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
            Review and manage document approval requests
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 text-sm">
            <span className="flex items-center gap-1.5 text-amber-600 dark:text-amber-400">
              <Clock className="h-4 w-4" />
              {mockApprovals.length} pending
            </span>
            <span className="text-stone-300 dark:text-stone-600">|</span>
            <span className="flex items-center gap-1.5 text-red-600 dark:text-red-400">
              <AlertTriangle className="h-4 w-4" />
              {mockApprovals.filter((a) => a.priority === 'urgent').length} urgent
            </span>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1 border-b border-stone-200 dark:border-stone-700">
        <button
          onClick={() => setTab('pending')}
          className={cn(
            'relative px-4 py-2.5 text-sm font-medium transition-colors',
            tab === 'pending'
              ? 'text-teal-600 dark:text-teal-400'
              : 'text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200'
          )}
        >
          Pending
          <span className="ml-2 rounded-full bg-amber-100 dark:bg-amber-900/50 px-2 py-0.5 text-xs text-amber-700 dark:text-amber-400">
            {mockApprovals.length}
          </span>
          {tab === 'pending' && (
            <motion.div
              layoutId="activeTab"
              className="absolute bottom-0 left-0 right-0 h-0.5 bg-teal-500"
            />
          )}
        </button>
        <button
          onClick={() => setTab('completed')}
          className={cn(
            'relative px-4 py-2.5 text-sm font-medium transition-colors',
            tab === 'completed'
              ? 'text-teal-600 dark:text-teal-400'
              : 'text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200'
          )}
        >
          Completed
          <span className="ml-2 rounded-full bg-stone-100 dark:bg-stone-800 px-2 py-0.5 text-xs text-stone-600 dark:text-stone-400">
            {completedApprovals.length}
          </span>
          {tab === 'completed' && (
            <motion.div
              layoutId="activeTab"
              className="absolute bottom-0 left-0 right-0 h-0.5 bg-teal-500"
            />
          )}
        </button>
      </div>

      {/* Filters */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex-1 max-w-md">
          <Input
            placeholder="Search approvals..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            leftIcon={<Search className="h-4 w-4" />}
          />
        </div>

        <div className="flex items-center gap-3">
          <Select
            value={priorityFilter}
            onChange={setPriorityFilter}
            options={priorityOptions}
            placeholder="Priority"
          />
          {tab === 'completed' && (
            <Select
              value={statusFilter}
              onChange={setStatusFilter}
              options={statusOptions}
              placeholder="Status"
            />
          )}
        </div>
      </div>

      {/* Approval List */}
      <div className="space-y-3">
        <AnimatePresence mode="popLayout">
          {filteredApprovals.map((approval, index) => (
            <motion.div
              key={approval.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95 }}
              transition={{ delay: index * 0.05 }}
            >
              <Card
                variant="elevated"
                className={cn(
                  'hover:shadow-lg transition-all cursor-pointer',
                  approval.priority === 'urgent' && 'ring-1 ring-red-200 dark:ring-red-800',
                  isOverdue(approval.dueDate) && approval.status === 'pending' && 'ring-1 ring-amber-200 dark:ring-amber-800'
                )}
                onClick={() => setSelectedApproval(approval)}
              >
                <CardContent className="p-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex items-start gap-4 flex-1 min-w-0">
                      <Avatar
                        name={approval.requester.name}
                        size="md"
                      />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          <h3 className="font-medium text-stone-900 dark:text-stone-100">
                            {approval.documentTitle.replace(/_/g, ' ')}
                          </h3>
                          <span
                            className={cn(
                              'px-2 py-0.5 rounded-full text-xs font-medium',
                              priorityConfig[approval.priority].color
                            )}
                          >
                            {priorityConfig[approval.priority].label}
                          </span>
                          {approval.status !== 'pending' && (
                            <Badge
                              variant={approval.status === 'approved' ? 'approved' : 'rejected'}
                              size="sm"
                            >
                              {approval.status}
                            </Badge>
                          )}
                        </div>
                        <p className="text-sm text-stone-500 dark:text-stone-400 mb-2">
                          Requested by {approval.requester.name}
                        </p>
                        <p className="text-sm text-stone-600 dark:text-stone-300 line-clamp-2">
                          {approval.comments}
                        </p>

                        <div className="flex items-center gap-4 mt-3 text-xs text-stone-500 dark:text-stone-400">
                          <span className="flex items-center gap-1">
                            <Calendar className="h-3.5 w-3.5" />
                            Requested {formatRelativeTime(approval.requestedAt)}
                          </span>
                          {approval.status === 'pending' && (
                            <span
                              className={cn(
                                'flex items-center gap-1',
                                isOverdue(approval.dueDate) && 'text-red-600 dark:text-red-400 font-medium'
                              )}
                            >
                              <Clock className="h-3.5 w-3.5" />
                              {isOverdue(approval.dueDate) ? 'Overdue' : `Due ${formatRelativeTime(approval.dueDate)}`}
                            </span>
                          )}
                          {approval.reviewedAt && (
                            <span className="flex items-center gap-1">
                              <CheckCircle2 className="h-3.5 w-3.5" />
                              Reviewed {formatRelativeTime(approval.reviewedAt)}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>

                    {approval.status === 'pending' && (
                      <div className="flex items-center gap-2">
                        <Button
                          variant="success"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            setActionModal({ type: 'approve', approval });
                          }}
                          leftIcon={<CheckCircle2 className="h-4 w-4" />}
                        >
                          Approve
                        </Button>
                        <Button
                          variant="danger"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            setActionModal({ type: 'reject', approval });
                          }}
                          leftIcon={<XCircle className="h-4 w-4" />}
                        >
                          Reject
                        </Button>
                      </div>
                    )}

                    <ChevronRight className="h-5 w-5 text-stone-400 flex-shrink-0" />
                  </div>
                </CardContent>
              </Card>
            </motion.div>
          ))}
        </AnimatePresence>

        {filteredApprovals.length === 0 && (
          <Card variant="ghost">
            <CardContent className="py-12 text-center">
              <CheckCircle2 className="h-12 w-12 mx-auto text-emerald-300 dark:text-emerald-700 mb-4" />
              <h3 className="font-medium text-stone-900 dark:text-stone-100 mb-2">
                {tab === 'pending' ? 'All caught up!' : 'No completed approvals'}
              </h3>
              <p className="text-sm text-stone-500 dark:text-stone-400">
                {tab === 'pending'
                  ? 'There are no pending approval requests at the moment.'
                  : 'No completed approvals match your filters.'}
              </p>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Detail Modal */}
      <Modal
        isOpen={!!selectedApproval}
        onClose={() => setSelectedApproval(null)}
        title="Approval Details"
        size="lg"
      >
        {selectedApproval && (
          <div className="space-y-6">
            <div className="flex items-start gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-stone-100 dark:bg-stone-800">
                <FileText className="h-6 w-6 text-stone-500" />
              </div>
              <div className="flex-1">
                <h3 className="text-lg font-semibold text-stone-900 dark:text-stone-100">
                  {selectedApproval.documentTitle.replace(/_/g, ' ')}
                </h3>
                <p className="text-sm text-stone-500 dark:text-stone-400">
                  Document ID: {selectedApproval.documentId}
                </p>
              </div>
              <span
                className={cn(
                  'px-3 py-1 rounded-full text-sm font-medium',
                  priorityConfig[selectedApproval.priority].color
                )}
              >
                {priorityConfig[selectedApproval.priority].label}
              </span>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1">
                <p className="text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wide">
                  Requester
                </p>
                <div className="flex items-center gap-2">
                  <Avatar name={selectedApproval.requester.name} size="sm" />
                  <div>
                    <p className="text-sm font-medium text-stone-900 dark:text-stone-100">
                      {selectedApproval.requester.name}
                    </p>
                    <p className="text-xs text-stone-500 dark:text-stone-400">
                      {selectedApproval.requester.email}
                    </p>
                  </div>
                </div>
              </div>

              <div className="space-y-1">
                <p className="text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wide">
                  Timeline
                </p>
                <p className="text-sm text-stone-900 dark:text-stone-100">
                  Requested: {formatDateTime(selectedApproval.requestedAt)}
                </p>
                <p className="text-sm text-stone-900 dark:text-stone-100">
                  Due: {formatDateTime(selectedApproval.dueDate)}
                </p>
              </div>
            </div>

            <div className="space-y-2">
              <p className="text-xs font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wide">
                Comments
              </p>
              <p className="text-sm text-stone-700 dark:text-stone-300 bg-stone-50 dark:bg-stone-800/50 rounded-lg p-3">
                {selectedApproval.comments}
              </p>
            </div>

            {selectedApproval.rejectionReason && (
              <div className="space-y-2">
                <p className="text-xs font-medium text-red-500 uppercase tracking-wide">
                  Rejection Reason
                </p>
                <p className="text-sm text-red-700 dark:text-red-300 bg-red-50 dark:bg-red-900/20 rounded-lg p-3">
                  {selectedApproval.rejectionReason}
                </p>
              </div>
            )}

            <div className="flex items-center justify-between pt-4 border-t border-stone-200 dark:border-stone-700">
              <Button
                variant="outline"
                leftIcon={<ExternalLink className="h-4 w-4" />}
              >
                View Document
              </Button>

              {selectedApproval.status === 'pending' && (
                <div className="flex items-center gap-2">
                  <Button
                    variant="danger"
                    onClick={() => {
                      setSelectedApproval(null);
                      setActionModal({ type: 'reject', approval: selectedApproval });
                    }}
                    leftIcon={<XCircle className="h-4 w-4" />}
                  >
                    Reject
                  </Button>
                  <Button
                    variant="success"
                    onClick={() => {
                      setSelectedApproval(null);
                      setActionModal({ type: 'approve', approval: selectedApproval });
                    }}
                    leftIcon={<CheckCircle2 className="h-4 w-4" />}
                  >
                    Approve
                  </Button>
                </div>
              )}
            </div>
          </div>
        )}
      </Modal>

      {/* Action Modal */}
      <Modal
        isOpen={!!actionModal}
        onClose={() => {
          setActionModal(null);
          setActionComment('');
        }}
        title={actionModal?.type === 'approve' ? 'Approve Document' : 'Reject Document'}
        size="md"
      >
        {actionModal && (
          <div className="space-y-4">
            <p className="text-sm text-stone-600 dark:text-stone-400">
              {actionModal.type === 'approve'
                ? 'Are you sure you want to approve this document?'
                : 'Please provide a reason for rejection.'}
            </p>

            <div className="bg-stone-50 dark:bg-stone-800/50 rounded-lg p-3">
              <p className="font-medium text-stone-900 dark:text-stone-100">
                {actionModal.approval.documentTitle.replace(/_/g, ' ')}
              </p>
              <p className="text-sm text-stone-500 dark:text-stone-400">
                Requested by {actionModal.approval.requester.name}
              </p>
            </div>

            <Textarea
              label={actionModal.type === 'approve' ? 'Comment (optional)' : 'Rejection reason'}
              placeholder={
                actionModal.type === 'approve'
                  ? 'Add any comments for the requester...'
                  : 'Explain why this document is being rejected...'
              }
              value={actionComment}
              onChange={(e) => setActionComment(e.target.value)}
              rows={3}
            />

            <div className="flex items-center justify-end gap-3 pt-4">
              <Button
                variant="outline"
                onClick={() => {
                  setActionModal(null);
                  setActionComment('');
                }}
              >
                Cancel
              </Button>
              {actionModal.type === 'approve' ? (
                <Button
                  variant="success"
                  onClick={handleApprove}
                  leftIcon={<CheckCircle2 className="h-4 w-4" />}
                >
                  Confirm Approval
                </Button>
              ) : (
                <Button
                  variant="danger"
                  onClick={handleReject}
                  disabled={!actionComment.trim()}
                  leftIcon={<XCircle className="h-4 w-4" />}
                >
                  Confirm Rejection
                </Button>
              )}
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}

export default Approvals;
