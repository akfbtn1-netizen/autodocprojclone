// ═══════════════════════════════════════════════════════════════════════════
// Approve/Reject Modal Component
// Unified modal for approval and rejection with feedback capture
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  X,
  CheckCircle2,
  XCircle,
  Star,
  MessageSquare,
  AlertCircle,
  Loader2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useApproveDocument, useRejectDocument } from '@/hooks/useApprovals';
import { useApprovalStore } from '@/stores/approvalStore';
import type { Approval, ApproveRequest, RejectRequest } from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface ApproveRejectModalProps {
  approval: Approval | null;
  mode: 'approve' | 'reject';
  isOpen: boolean;
  onClose: () => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Star Rating Component
// ─────────────────────────────────────────────────────────────────────────────

interface StarRatingProps {
  value: number;
  onChange: (rating: number) => void;
}

function StarRating({ value, onChange }: StarRatingProps) {
  const [hoverValue, setHoverValue] = useState(0);

  return (
    <div className="flex items-center gap-1">
      {[1, 2, 3, 4, 5].map((star) => (
        <button
          key={star}
          type="button"
          onClick={() => onChange(star)}
          onMouseEnter={() => setHoverValue(star)}
          onMouseLeave={() => setHoverValue(0)}
          className="p-1 transition-transform hover:scale-110"
        >
          <Star
            className={cn(
              'w-6 h-6 transition-colors',
              (hoverValue || value) >= star
                ? 'text-amber-400 fill-amber-400'
                : 'text-stone-300 dark:text-stone-600'
            )}
          />
        </button>
      ))}
      {value > 0 && (
        <span className="ml-2 text-sm text-stone-500 dark:text-stone-400">
          {value}/5
        </span>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Rejection Reasons
// ─────────────────────────────────────────────────────────────────────────────

const rejectionReasons = [
  { label: 'Inaccurate Content', value: 'inaccurate' },
  { label: 'Missing Information', value: 'missing' },
  { label: 'Wrong Format', value: 'format' },
  { label: 'Not Relevant', value: 'irrelevant' },
  { label: 'Quality Issues', value: 'quality' },
  { label: 'Other', value: 'other' },
];

// ─────────────────────────────────────────────────────────────────────────────
// Main Modal Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApproveRejectModal({
  approval,
  mode,
  isOpen,
  onClose,
}: ApproveRejectModalProps) {
  const { closeApproveModal, closeRejectModal, closeDetailsModal } = useApprovalStore();
  const approveMutation = useApproveDocument();
  const rejectMutation = useRejectDocument();

  const [qualityRating, setQualityRating] = useState(0);
  const [comments, setComments] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [feedbackText, setFeedbackText] = useState('');
  const [showSuccess, setShowSuccess] = useState(false);

  // Reset form when modal opens
  useEffect(() => {
    if (isOpen) {
      setQualityRating(0);
      setComments('');
      setRejectionReason('');
      setFeedbackText('');
      setShowSuccess(false);
    }
  }, [isOpen]);

  const handleSubmit = async () => {
    if (!approval) return;

    try {
      if (mode === 'approve') {
        const request: ApproveRequest = {
          comments: comments.trim() || undefined,
          qualityRating: qualityRating > 0 ? qualityRating : undefined,
          feedbackText: feedbackText.trim() || undefined,
        };
        await approveMutation.mutateAsync({ id: approval.id, request });
      } else {
        if (!rejectionReason) return;
        const request: RejectRequest = {
          rejectionReason,
          comments: comments.trim() || undefined,
          qualityRating: qualityRating > 0 ? qualityRating : undefined,
          feedbackText: feedbackText.trim() || undefined,
        };
        await rejectMutation.mutateAsync({ id: approval.id, request });
      }

      setShowSuccess(true);
      setTimeout(() => {
        onClose();
        closeApproveModal();
        closeRejectModal();
        closeDetailsModal();
      }, 1500);
    } catch (error) {
      console.error('Action failed:', error);
    }
  };

  const isApprove = mode === 'approve';
  const isPending = approveMutation.isPending || rejectMutation.isPending;
  const isError = approveMutation.isError || rejectMutation.isError;
  const canSubmit = isApprove ? true : !!rejectionReason;

  if (!approval) return null;

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50"
            onClick={onClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="fixed inset-0 z-50 flex items-center justify-center p-4"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="bg-white dark:bg-stone-900 rounded-xl shadow-2xl w-full max-w-lg overflow-hidden">
              {/* Header */}
              <div
                className={cn(
                  'flex items-center justify-between px-6 py-4',
                  isApprove
                    ? 'bg-emerald-50 dark:bg-emerald-900/20'
                    : 'bg-red-50 dark:bg-red-900/20'
                )}
              >
                <div className="flex items-center gap-3">
                  <div
                    className={cn(
                      'flex items-center justify-center w-10 h-10 rounded-lg',
                      isApprove
                        ? 'bg-emerald-100 dark:bg-emerald-900/30'
                        : 'bg-red-100 dark:bg-red-900/30'
                    )}
                  >
                    {isApprove ? (
                      <CheckCircle2 className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
                    ) : (
                      <XCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
                    )}
                  </div>
                  <div>
                    <h2
                      className={cn(
                        'text-lg font-semibold',
                        isApprove
                          ? 'text-emerald-900 dark:text-emerald-100'
                          : 'text-red-900 dark:text-red-100'
                      )}
                    >
                      {isApprove ? 'Approve Document' : 'Reject Document'}
                    </h2>
                    <p
                      className={cn(
                        'text-sm',
                        isApprove
                          ? 'text-emerald-700 dark:text-emerald-300'
                          : 'text-red-700 dark:text-red-300'
                      )}
                    >
                      {approval.objectName}
                    </p>
                  </div>
                </div>
                <button
                  onClick={onClose}
                  className="p-2 text-stone-400 hover:text-stone-600 dark:hover:text-stone-300 rounded-lg hover:bg-white/50 dark:hover:bg-stone-800/50 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Success State */}
              {showSuccess ? (
                <div className="flex flex-col items-center justify-center py-16">
                  <motion.div
                    initial={{ scale: 0 }}
                    animate={{ scale: 1 }}
                    transition={{ type: 'spring', duration: 0.5 }}
                  >
                    {isApprove ? (
                      <CheckCircle2 className="w-16 h-16 text-emerald-500 mb-4" />
                    ) : (
                      <XCircle className="w-16 h-16 text-red-500 mb-4" />
                    )}
                  </motion.div>
                  <h3 className="text-lg font-semibold text-stone-900 dark:text-stone-100 mb-2">
                    {isApprove ? 'Document Approved' : 'Document Rejected'}
                  </h3>
                  <p className="text-sm text-stone-500 dark:text-stone-400">
                    Your feedback has been recorded.
                  </p>
                </div>
              ) : (
                <>
                  {/* Content */}
                  <div className="p-6 space-y-5">
                    {/* Rejection Reason (only for reject mode) */}
                    {!isApprove && (
                      <div>
                        <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                          Reason for Rejection{' '}
                          <span className="text-red-500">*</span>
                        </label>
                        <div className="flex flex-wrap gap-2">
                          {rejectionReasons.map((reason) => (
                            <button
                              key={reason.value}
                              onClick={() => setRejectionReason(reason.value)}
                              className={cn(
                                'px-3 py-1.5 text-sm rounded-lg border transition-colors',
                                rejectionReason === reason.value
                                  ? 'bg-red-50 dark:bg-red-900/30 border-red-300 dark:border-red-700 text-red-700 dark:text-red-400'
                                  : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700 text-stone-600 dark:text-stone-400 hover:border-stone-300'
                              )}
                            >
                              {reason.label}
                            </button>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Quality Rating */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        Quality Rating (Optional)
                      </label>
                      <StarRating value={qualityRating} onChange={setQualityRating} />
                      <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
                        Rate the overall quality of the generated documentation
                      </p>
                    </div>

                    {/* Comments */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        <MessageSquare className="w-4 h-4 inline mr-1" />
                        Comments (Optional)
                      </label>
                      <textarea
                        value={comments}
                        onChange={(e) => setComments(e.target.value)}
                        placeholder={
                          isApprove
                            ? 'Add any notes for the record...'
                            : 'Explain what needs to be fixed...'
                        }
                        rows={3}
                        className="w-full px-4 py-3 bg-white dark:bg-stone-800 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent resize-none"
                      />
                    </div>

                    {/* Additional Feedback */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        AI Feedback (Optional)
                      </label>
                      <textarea
                        value={feedbackText}
                        onChange={(e) => setFeedbackText(e.target.value)}
                        placeholder="Provide feedback to improve future AI-generated content..."
                        rows={2}
                        className="w-full px-4 py-3 bg-white dark:bg-stone-800 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent resize-none"
                      />
                      <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
                        This feedback helps improve the AI documentation generation
                      </p>
                    </div>
                  </div>

                  {/* Error State */}
                  {isError && (
                    <div className="mx-6 mb-4 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
                      <div className="flex items-center gap-2 text-red-700 dark:text-red-400">
                        <AlertCircle className="w-4 h-4" />
                        <span className="text-sm">
                          Failed to {isApprove ? 'approve' : 'reject'}. Please try
                          again.
                        </span>
                      </div>
                    </div>
                  )}

                  {/* Footer */}
                  <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-800/50">
                    <button
                      onClick={onClose}
                      className="px-4 py-2 text-sm font-medium text-stone-600 dark:text-stone-400 hover:text-stone-800 dark:hover:text-stone-200 transition-colors"
                    >
                      Cancel
                    </button>
                    <button
                      onClick={handleSubmit}
                      disabled={!canSubmit || isPending}
                      className={cn(
                        'flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors',
                        canSubmit && !isPending
                          ? isApprove
                            ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                            : 'bg-red-600 text-white hover:bg-red-700'
                          : 'bg-stone-200 dark:bg-stone-700 text-stone-400 cursor-not-allowed'
                      )}
                    >
                      {isPending ? (
                        <>
                          <Loader2 className="w-4 h-4 animate-spin" />
                          Processing...
                        </>
                      ) : isApprove ? (
                        <>
                          <CheckCircle2 className="w-4 h-4" />
                          Approve
                        </>
                      ) : (
                        <>
                          <XCircle className="w-4 h-4" />
                          Reject
                        </>
                      )}
                    </button>
                  </div>
                </>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

export default ApproveRejectModal;
