// ═══════════════════════════════════════════════════════════════════════════
// Re-prompt Modal Component
// Request regeneration with feedback for AI learning
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  X,
  RefreshCw,
  AlertCircle,
  Lightbulb,
  FileText,
  Loader2,
  CheckCircle2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useRequestRegeneration } from '@/hooks/useApprovals';
import { useApprovalStore } from '@/stores/approvalStore';
import type { Approval, RegenerationRequest } from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface RepromptModalProps {
  approval: Approval | null;
  isOpen: boolean;
  onClose: () => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Section Selector
// ─────────────────────────────────────────────────────────────────────────────

const commonSections = [
  { id: 'overview', label: 'Overview' },
  { id: 'purpose', label: 'Business Purpose' },
  { id: 'technical', label: 'Technical Details' },
  { id: 'usage', label: 'Usage Guidelines' },
  { id: 'security', label: 'Security Notes' },
  { id: 'examples', label: 'Examples' },
];

// ─────────────────────────────────────────────────────────────────────────────
// Feedback Templates
// ─────────────────────────────────────────────────────────────────────────────

const feedbackTemplates = [
  {
    label: 'More Detail Needed',
    text: 'Please provide more detailed information about the functionality and usage patterns.',
  },
  {
    label: 'Inaccurate Content',
    text: 'The generated content contains inaccuracies. Please regenerate with correct information.',
  },
  {
    label: 'Missing Information',
    text: 'Key information is missing from the documentation. Please include: ',
  },
  {
    label: 'Wrong Format',
    text: 'The document format doesn\'t match our standards. Please adjust the structure to follow: ',
  },
  {
    label: 'Outdated References',
    text: 'Some references are outdated. Please update with current information.',
  },
];

// ─────────────────────────────────────────────────────────────────────────────
// Main Modal Component
// ─────────────────────────────────────────────────────────────────────────────

export function RepromptModal({ approval, isOpen, onClose }: RepromptModalProps) {
  const { closeRepromptModal } = useApprovalStore();
  const regenerateMutation = useRequestRegeneration();

  const [feedbackText, setFeedbackText] = useState('');
  const [selectedSection, setSelectedSection] = useState<string | null>(null);
  const [additionalContext, setAdditionalContext] = useState('');
  const [showSuccess, setShowSuccess] = useState(false);

  // Reset form when modal opens
  useEffect(() => {
    if (isOpen) {
      setFeedbackText('');
      setSelectedSection(null);
      setAdditionalContext('');
      setShowSuccess(false);
    }
  }, [isOpen]);

  const handleSubmit = async () => {
    if (!approval || !feedbackText.trim()) return;

    const request: RegenerationRequest = {
      approvalId: approval.id,
      documentId: approval.documentId,
      originalVersion: approval.version,
      feedbackText: feedbackText.trim(),
      feedbackSection: selectedSection,
      additionalContext: additionalContext.trim() || null,
      requestedBy: 'current-user', // TODO: Get from auth context
    };

    try {
      await regenerateMutation.mutateAsync(request);
      setShowSuccess(true);
      setTimeout(() => {
        onClose();
        closeRepromptModal();
      }, 1500);
    } catch (error) {
      console.error('Regeneration failed:', error);
    }
  };

  const handleTemplateSelect = (template: string) => {
    setFeedbackText((prev) => (prev ? `${prev}\n\n${template}` : template));
  };

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
            <div className="bg-white dark:bg-stone-900 rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden">
              {/* Header */}
              <div className="flex items-center justify-between px-6 py-4 border-b border-stone-200 dark:border-stone-700">
                <div className="flex items-center gap-3">
                  <div className="flex items-center justify-center w-10 h-10 rounded-lg bg-blue-100 dark:bg-blue-900/30">
                    <RefreshCw className="w-5 h-5 text-blue-600 dark:text-blue-400" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-stone-900 dark:text-stone-100">
                      Request Re-generation
                    </h2>
                    <p className="text-sm text-stone-500 dark:text-stone-400">
                      {approval.objectName}
                    </p>
                  </div>
                </div>
                <button
                  onClick={onClose}
                  className="p-2 text-stone-400 hover:text-stone-600 dark:hover:text-stone-300 rounded-lg hover:bg-stone-100 dark:hover:bg-stone-800 transition-colors"
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
                    <CheckCircle2 className="w-16 h-16 text-emerald-500 mb-4" />
                  </motion.div>
                  <h3 className="text-lg font-semibold text-stone-900 dark:text-stone-100 mb-2">
                    Re-generation Requested
                  </h3>
                  <p className="text-sm text-stone-500 dark:text-stone-400">
                    Your feedback has been submitted for AI processing.
                  </p>
                </div>
              ) : (
                <>
                  {/* Content */}
                  <div className="p-6 space-y-6 max-h-[60vh] overflow-y-auto">
                    {/* Info Banner */}
                    <div className="flex items-start gap-3 p-4 bg-blue-50 dark:bg-blue-900/20 rounded-lg">
                      <Lightbulb className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" />
                      <div className="text-sm">
                        <p className="font-medium text-blue-800 dark:text-blue-200 mb-1">
                          Your feedback improves the AI
                        </p>
                        <p className="text-blue-700 dark:text-blue-300">
                          Specific feedback helps the system learn and generate better
                          documentation in the future.
                        </p>
                      </div>
                    </div>

                    {/* Section Selector */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        Which section needs improvement? (Optional)
                      </label>
                      <div className="flex flex-wrap gap-2">
                        {commonSections.map((section) => (
                          <button
                            key={section.id}
                            onClick={() =>
                              setSelectedSection(
                                selectedSection === section.id ? null : section.id
                              )
                            }
                            className={cn(
                              'px-3 py-1.5 text-sm rounded-lg border transition-colors',
                              selectedSection === section.id
                                ? 'bg-teal-50 dark:bg-teal-900/30 border-teal-300 dark:border-teal-700 text-teal-700 dark:text-teal-400'
                                : 'bg-white dark:bg-stone-800 border-stone-200 dark:border-stone-700 text-stone-600 dark:text-stone-400 hover:border-stone-300'
                            )}
                          >
                            {section.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Feedback Text */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        Describe what needs to be changed{' '}
                        <span className="text-red-500">*</span>
                      </label>
                      <textarea
                        value={feedbackText}
                        onChange={(e) => setFeedbackText(e.target.value)}
                        placeholder="Be specific about what's wrong and what you expect..."
                        rows={4}
                        className="w-full px-4 py-3 bg-white dark:bg-stone-800 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent resize-none"
                      />
                      {!feedbackText.trim() && (
                        <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
                          Required - Tell us what needs to change
                        </p>
                      )}
                    </div>

                    {/* Quick Templates */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        Quick templates
                      </label>
                      <div className="flex flex-wrap gap-2">
                        {feedbackTemplates.map((template, idx) => (
                          <button
                            key={idx}
                            onClick={() => handleTemplateSelect(template.text)}
                            className="px-3 py-1.5 text-sm bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400 rounded-lg hover:bg-stone-200 dark:hover:bg-stone-700 transition-colors"
                          >
                            {template.label}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Additional Context */}
                    <div>
                      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
                        Additional context (Optional)
                      </label>
                      <textarea
                        value={additionalContext}
                        onChange={(e) => setAdditionalContext(e.target.value)}
                        placeholder="Provide any additional information that might help..."
                        rows={2}
                        className="w-full px-4 py-3 bg-white dark:bg-stone-800 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent resize-none"
                      />
                    </div>
                  </div>

                  {/* Footer */}
                  <div className="flex items-center justify-between px-6 py-4 border-t border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-800/50">
                    <div className="flex items-center gap-2 text-sm text-stone-500 dark:text-stone-400">
                      <FileText className="w-4 h-4" />
                      Version {approval.version}
                    </div>
                    <div className="flex items-center gap-3">
                      <button
                        onClick={onClose}
                        className="px-4 py-2 text-sm font-medium text-stone-600 dark:text-stone-400 hover:text-stone-800 dark:hover:text-stone-200 transition-colors"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={handleSubmit}
                        disabled={!feedbackText.trim() || regenerateMutation.isPending}
                        className={cn(
                          'flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg transition-colors',
                          feedbackText.trim() && !regenerateMutation.isPending
                            ? 'bg-blue-600 text-white hover:bg-blue-700'
                            : 'bg-stone-200 dark:bg-stone-700 text-stone-400 cursor-not-allowed'
                        )}
                      >
                        {regenerateMutation.isPending ? (
                          <>
                            <Loader2 className="w-4 h-4 animate-spin" />
                            Submitting...
                          </>
                        ) : (
                          <>
                            <RefreshCw className="w-4 h-4" />
                            Request Re-generation
                          </>
                        )}
                      </button>
                    </div>
                  </div>
                </>
              )}

              {/* Error State */}
              {regenerateMutation.isError && (
                <div className="mx-6 mb-4 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
                  <div className="flex items-center gap-2 text-red-700 dark:text-red-400">
                    <AlertCircle className="w-4 h-4" />
                    <span className="text-sm">
                      Failed to submit request. Please try again.
                    </span>
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

export default RepromptModal;
