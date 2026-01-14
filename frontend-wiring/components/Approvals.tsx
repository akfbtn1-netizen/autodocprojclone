// =============================================
// APPROVALS PAGE - WIRED VERSION
// File: frontend/src/pages/Approvals.tsx
// Full wiring example with real data
// =============================================

import { useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  FileText, 
  Clock, 
  Check, 
  X, 
  RefreshCw, 
  Edit3,
  Filter,
  Search,
  ChevronDown,
  AlertTriangle,
  Loader2,
} from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';

// Hooks
import { 
  usePendingApprovals, 
  useApproval,
  useApprovalDocument,
  useApproveDocument,
  useRejectDocument,
  useRepromptDocument,
  useEditDocument,
} from '@/hooks/useApprovals';
import { useSignalRContext } from '@/providers/SignalRProvider';

// Types
import type { Approval, Priority } from '@/types/api';

// Utils
import { cn } from '@/lib/utils';

// =============================================
// PRIORITY COLORS
// =============================================

const priorityColors: Record<Priority, string> = {
  Low: 'bg-stone-100 text-stone-700',
  Normal: 'bg-blue-100 text-blue-700',
  High: 'bg-amber-100 text-amber-700',
  Urgent: 'bg-red-100 text-red-700 animate-pulse',
};

const statusColors: Record<string, string> = {
  Pending: 'border-amber-300 bg-amber-50',
  InReview: 'border-blue-300 bg-blue-50',
  Approved: 'border-green-300 bg-green-50',
  Rejected: 'border-red-300 bg-red-50',
  ChangesRequested: 'border-purple-300 bg-purple-50',
};

// =============================================
// MAIN APPROVALS PAGE
// =============================================

export function Approvals() {
  // State
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [priorityFilter, setPriorityFilter] = useState<Priority | ''>('');
  const [showFilters, setShowFilters] = useState(false);

  // SignalR connection status
  const { isConnected } = useSignalRContext();

  // Data fetching
  const { data: approvals, isLoading, error, refetch } = usePendingApprovals();
  const { data: selectedApproval, isLoading: loadingDetail } = useApproval(selectedId ?? 0);
  const { data: documentContent, isLoading: loadingContent } = useApprovalDocument(selectedId ?? 0);

  // Mutations
  const approveMutation = useApproveDocument();
  const rejectMutation = useRejectDocument();
  const repromptMutation = useRepromptDocument();
  const editMutation = useEditDocument();

  // =============================================
  // HANDLERS
  // =============================================

  const handleApprove = async (id: number, comments: string) => {
    await approveMutation.mutateAsync({
      id,
      request: {
        comments,
        approvedBy: localStorage.getItem('userEmail') || 'user@example.com',
      },
    });
    setSelectedId(null);
  };

  const handleReject = async (id: number, reason: string) => {
    if (!reason.trim()) return;
    await rejectMutation.mutateAsync({
      id,
      request: {
        rejectionReason: reason,
        rejectedBy: localStorage.getItem('userEmail') || 'user@example.com',
      },
    });
    setSelectedId(null);
  };

  const handleReprompt = async (id: number, feedback: string, section?: string) => {
    if (!feedback.trim()) return;
    await repromptMutation.mutateAsync({
      id,
      request: {
        feedbackText: feedback,
        feedbackSection: section,
      },
    });
    setSelectedId(null);
  };

  const handleEdit = async (
    id: number, 
    sectionName: string, 
    originalText: string, 
    editedText: string, 
    reason: string
  ) => {
    await editMutation.mutateAsync({
      id,
      request: {
        sectionName,
        originalText,
        editedText,
        editReason: reason,
        editCategory: 'Technical',
        shouldTrainAi: true,
      },
    });
  };

  // =============================================
  // FILTERING
  // =============================================

  const filteredApprovals = approvals?.filter((approval) => {
    // Search filter
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      const matchesSearch = 
        approval.objectName?.toLowerCase().includes(query) ||
        approval.documentId?.toLowerCase().includes(query) ||
        approval.docIdString?.toLowerCase().includes(query) ||
        approval.schemaName?.toLowerCase().includes(query);
      if (!matchesSearch) return false;
    }

    // Priority filter
    if (priorityFilter && approval.priority !== priorityFilter) {
      return false;
    }

    return true;
  }) ?? [];

  // =============================================
  // RENDER
  // =============================================

  return (
    <div className="min-h-screen bg-stone-50 p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-semibold text-stone-900">Approvals</h1>
          <p className="text-stone-500">
            {filteredApprovals.length} pending • {isConnected ? (
              <span className="text-green-600">● Live</span>
            ) : (
              <span className="text-amber-600">○ Connecting...</span>
            )}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-3 py-2 text-sm bg-white border border-stone-200 rounded-lg hover:bg-stone-50"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="mb-6 space-y-4">
        <div className="flex items-center gap-4">
          {/* Search */}
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-stone-400" />
            <input
              type="text"
              placeholder="Search by object, document ID..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-stone-200 rounded-lg focus:ring-2 focus:ring-teal-500 focus:border-teal-500"
            />
          </div>

          {/* Priority filter */}
          <select
            value={priorityFilter}
            onChange={(e) => setPriorityFilter(e.target.value as Priority | '')}
            className="px-4 py-2 border border-stone-200 rounded-lg focus:ring-2 focus:ring-teal-500"
          >
            <option value="">All Priorities</option>
            <option value="Urgent">Urgent</option>
            <option value="High">High</option>
            <option value="Normal">Normal</option>
            <option value="Low">Low</option>
          </select>
        </div>
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="flex items-center justify-center h-64">
          <Loader2 className="w-8 h-8 animate-spin text-teal-600" />
        </div>
      ) : error ? (
        <div className="flex flex-col items-center justify-center h-64 text-red-600">
          <AlertTriangle className="w-12 h-12 mb-4" />
          <p>Failed to load approvals</p>
          <button onClick={() => refetch()} className="mt-2 text-sm underline">
            Try again
          </button>
        </div>
      ) : filteredApprovals.length === 0 ? (
        <div className="flex flex-col items-center justify-center h-64 text-stone-500">
          <Check className="w-12 h-12 mb-4 text-green-500" />
          <p className="text-lg font-medium">All caught up!</p>
          <p className="text-sm">No pending approvals</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <AnimatePresence mode="popLayout">
            {filteredApprovals.map((approval, index) => (
              <ApprovalCard
                key={approval.id}
                approval={approval}
                index={index}
                onClick={() => setSelectedId(approval.id)}
                onQuickApprove={() => handleApprove(approval.id, '')}
              />
            ))}
          </AnimatePresence>
        </div>
      )}

      {/* Detail Modal */}
      <ApprovalModal
        isOpen={selectedId !== null}
        onClose={() => setSelectedId(null)}
        approval={selectedApproval}
        documentContent={documentContent}
        isLoading={loadingDetail || loadingContent}
        onApprove={(comments) => handleApprove(selectedId!, comments)}
        onReject={(reason) => handleReject(selectedId!, reason)}
        onReprompt={(feedback, section) => handleReprompt(selectedId!, feedback, section)}
        onEdit={(section, original, edited, reason) => 
          handleEdit(selectedId!, section, original, edited, reason)
        }
        isProcessing={
          approveMutation.isPending || 
          rejectMutation.isPending || 
          repromptMutation.isPending
        }
      />
    </div>
  );
}

// =============================================
// APPROVAL CARD COMPONENT
// =============================================

interface ApprovalCardProps {
  approval: Approval;
  index: number;
  onClick: () => void;
  onQuickApprove: () => void;
}

function ApprovalCard({ approval, index, onClick, onQuickApprove }: ApprovalCardProps) {
  const timeAgo = formatDistanceToNow(new Date(approval.requestedAt), { addSuffix: true });
  const isOverdue = approval.dueDate && new Date(approval.dueDate) < new Date();

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.95 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'border-2 rounded-xl p-4 bg-white cursor-pointer transition-all hover:shadow-lg',
        statusColors[approval.status] || 'border-stone-200'
      )}
      onClick={onClick}
    >
      {/* Header */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          <FileText className="w-5 h-5 text-teal-600" />
          <span className={cn(
            'text-xs font-medium px-2 py-0.5 rounded-full',
            priorityColors[approval.priority]
          )}>
            {approval.priority}
          </span>
        </div>
        {isOverdue && (
          <span className="flex items-center gap-1 text-xs text-red-600">
            <AlertTriangle className="w-3 h-3" />
            Overdue
          </span>
        )}
      </div>

      {/* Content */}
      <div className="space-y-2">
        <h3 className="font-semibold text-stone-900 line-clamp-1">
          {approval.docIdString || approval.documentId}
        </h3>
        <p className="text-sm text-stone-600 line-clamp-1">
          {approval.schemaName}.{approval.objectName}
        </p>
        <div className="flex items-center gap-3 text-xs text-stone-500">
          <span className="flex items-center gap-1">
            <Clock className="w-3 h-3" />
            {timeAgo}
          </span>
          <span>{approval.documentType}</span>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 mt-4 pt-3 border-t border-stone-100">
        <button
          onClick={(e) => {
            e.stopPropagation();
            onClick();
          }}
          className="flex-1 px-3 py-1.5 text-sm font-medium text-teal-700 bg-teal-100 hover:bg-teal-200 rounded-lg transition-colors"
        >
          Review
        </button>
        <button
          onClick={(e) => {
            e.stopPropagation();
            onQuickApprove();
          }}
          className="p-1.5 text-green-700 bg-green-100 hover:bg-green-200 rounded-lg transition-colors"
          title="Quick Approve"
        >
          <Check className="w-4 h-4" />
        </button>
      </div>
    </motion.div>
  );
}

// =============================================
// APPROVAL MODAL COMPONENT
// =============================================

interface ApprovalModalProps {
  isOpen: boolean;
  onClose: () => void;
  approval?: Approval;
  documentContent?: string;
  isLoading: boolean;
  onApprove: (comments: string) => Promise<void>;
  onReject: (reason: string) => Promise<void>;
  onReprompt: (feedback: string, section?: string) => Promise<void>;
  onEdit: (section: string, original: string, edited: string, reason: string) => Promise<void>;
  isProcessing: boolean;
}

function ApprovalModal({
  isOpen,
  onClose,
  approval,
  documentContent,
  isLoading,
  onApprove,
  onReject,
  onReprompt,
  onEdit,
  isProcessing,
}: ApprovalModalProps) {
  const [activeTab, setActiveTab] = useState<'preview' | 'approve' | 'reject' | 'reprompt'>('preview');
  const [comments, setComments] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [repromptFeedback, setRepromptFeedback] = useState('');

  const handleApprove = async () => {
    await onApprove(comments);
    setComments('');
    setActiveTab('preview');
  };

  const handleReject = async () => {
    await onReject(rejectionReason);
    setRejectionReason('');
    setActiveTab('preview');
  };

  const handleReprompt = async () => {
    await onReprompt(repromptFeedback);
    setRepromptFeedback('');
    setActiveTab('preview');
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/50 z-40"
            onClick={onClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="fixed inset-4 md:inset-10 bg-white rounded-2xl shadow-2xl z-50 flex flex-col overflow-hidden"
          >
            {/* Header */}
            <div className="flex items-center justify-between p-4 border-b border-stone-200">
              <div>
                <h2 className="text-lg font-semibold text-stone-900">
                  {approval?.docIdString || approval?.documentId || 'Loading...'}
                </h2>
                <p className="text-sm text-stone-500">
                  {approval?.schemaName}.{approval?.objectName} • {approval?.documentType}
                </p>
              </div>
              <button
                onClick={onClose}
                className="p-2 hover:bg-stone-100 rounded-lg transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Tabs */}
            <div className="flex border-b border-stone-200 px-4">
              {(['preview', 'approve', 'reject', 'reprompt'] as const).map((tab) => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={cn(
                    'px-4 py-3 text-sm font-medium border-b-2 transition-colors',
                    activeTab === tab
                      ? 'border-teal-500 text-teal-600'
                      : 'border-transparent text-stone-500 hover:text-stone-700'
                  )}
                >
                  {tab === 'preview' && '📄 Preview'}
                  {tab === 'approve' && '✅ Approve'}
                  {tab === 'reject' && '❌ Reject'}
                  {tab === 'reprompt' && '🔄 Reprompt AI'}
                </button>
              ))}
            </div>

            {/* Content */}
            <div className="flex-1 overflow-auto p-6">
              {isLoading ? (
                <div className="flex items-center justify-center h-full">
                  <Loader2 className="w-8 h-8 animate-spin text-teal-600" />
                </div>
              ) : (
                <>
                  {activeTab === 'preview' && (
                    <div className="prose max-w-none">
                      {documentContent ? (
                        <div dangerouslySetInnerHTML={{ __html: documentContent }} />
                      ) : (
                        <p className="text-stone-500">No preview available</p>
                      )}
                    </div>
                  )}

                  {activeTab === 'approve' && (
                    <div className="max-w-xl space-y-4">
                      <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
                        <h3 className="font-medium text-green-800 mb-2">Approve Document</h3>
                        <p className="text-sm text-green-700">
                          This will mark the document as approved and add it to the MasterIndex.
                        </p>
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-stone-700 mb-1">
                          Comments (optional)
                        </label>
                        <textarea
                          value={comments}
                          onChange={(e) => setComments(e.target.value)}
                          rows={4}
                          className="w-full px-3 py-2 border border-stone-300 rounded-lg focus:ring-2 focus:ring-teal-500"
                          placeholder="Add any comments..."
                        />
                      </div>
                      <button
                        onClick={handleApprove}
                        disabled={isProcessing}
                        className="flex items-center gap-2 px-6 py-3 bg-green-600 text-white font-medium rounded-lg hover:bg-green-700 disabled:opacity-50"
                      >
                        {isProcessing ? <Loader2 className="w-5 h-5 animate-spin" /> : <Check className="w-5 h-5" />}
                        {isProcessing ? 'Approving...' : 'Approve Document'}
                      </button>
                    </div>
                  )}

                  {activeTab === 'reject' && (
                    <div className="max-w-xl space-y-4">
                      <div className="p-4 bg-red-50 border border-red-200 rounded-lg">
                        <h3 className="font-medium text-red-800 mb-2">Reject Document</h3>
                        <p className="text-sm text-red-700">
                          Please provide a reason for rejection.
                        </p>
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-stone-700 mb-1">
                          Rejection Reason *
                        </label>
                        <textarea
                          value={rejectionReason}
                          onChange={(e) => setRejectionReason(e.target.value)}
                          rows={4}
                          className="w-full px-3 py-2 border border-stone-300 rounded-lg focus:ring-2 focus:ring-red-500"
                          placeholder="Explain why this document is being rejected..."
                          required
                        />
                      </div>
                      <button
                        onClick={handleReject}
                        disabled={isProcessing || !rejectionReason.trim()}
                        className="flex items-center gap-2 px-6 py-3 bg-red-600 text-white font-medium rounded-lg hover:bg-red-700 disabled:opacity-50"
                      >
                        {isProcessing ? <Loader2 className="w-5 h-5 animate-spin" /> : <X className="w-5 h-5" />}
                        {isProcessing ? 'Rejecting...' : 'Reject Document'}
                      </button>
                    </div>
                  )}

                  {activeTab === 'reprompt' && (
                    <div className="max-w-xl space-y-4">
                      <div className="p-4 bg-purple-50 border border-purple-200 rounded-lg">
                        <h3 className="font-medium text-purple-800 mb-2">Request AI Regeneration</h3>
                        <p className="text-sm text-purple-700">
                          Provide feedback to improve the AI-generated content. A new version will be created.
                        </p>
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-stone-700 mb-1">
                          Feedback for AI *
                        </label>
                        <textarea
                          value={repromptFeedback}
                          onChange={(e) => setRepromptFeedback(e.target.value)}
                          rows={6}
                          className="w-full px-3 py-2 border border-stone-300 rounded-lg focus:ring-2 focus:ring-purple-500"
                          placeholder="Describe what should be improved..."
                          required
                        />
                      </div>
                      <p className="text-xs text-stone-500">
                        This feedback will be logged to DaQa.RegenerationRequests for AI training.
                      </p>
                      <button
                        onClick={handleReprompt}
                        disabled={isProcessing || !repromptFeedback.trim()}
                        className="flex items-center gap-2 px-6 py-3 bg-purple-600 text-white font-medium rounded-lg hover:bg-purple-700 disabled:opacity-50"
                      >
                        {isProcessing ? <Loader2 className="w-5 h-5 animate-spin" /> : <RefreshCw className="w-5 h-5" />}
                        {isProcessing ? 'Requesting...' : 'Request Regeneration'}
                      </button>
                    </div>
                  )}
                </>
              )}
            </div>

            {/* Footer - Metadata */}
            <div className="border-t border-stone-200 p-4 bg-stone-50">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                <div>
                  <span className="text-stone-500">Requested</span>
                  <p className="font-medium">{approval?.requestedAt ? new Date(approval.requestedAt).toLocaleDateString() : '-'}</p>
                </div>
                <div>
                  <span className="text-stone-500">By</span>
                  <p className="font-medium">{approval?.requestedBy || '-'}</p>
                </div>
                <div>
                  <span className="text-stone-500">Priority</span>
                  <p className="font-medium">{approval?.priority || '-'}</p>
                </div>
                <div>
                  <span className="text-stone-500">Version</span>
                  <p className="font-medium">v{approval?.version || 1}</p>
                </div>
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

export default Approvals;
