// ═══════════════════════════════════════════════════════════════════════════
// Approval Details Modal Component
// Full details view with tabs for preview, history, and actions
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  X,
  FileText,
  History,
  MessageSquare,
  Edit3,
  ExternalLink,
  Download,
  Clock,
  User,
  Database,
  Tag,
  Calendar,
  CheckCircle2,
  XCircle,
  RefreshCw,
  Star,
  AlertTriangle,
  Loader2,
} from 'lucide-react';
import { cn, formatRelativeTime } from '@/lib/utils';
import { useApproval, useWorkflowEvents, useDocumentEdits } from '@/hooks/useApprovals';
import { approvalApi } from '@/services/approvalApi';
import type { Approval, WorkflowEvent, DocumentEdit } from '@/types/approval';
import {
  getStatusDisplayName,
  getStatusVariant,
  DocumentTypeLabels,
  priorityColors,
} from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface ApprovalDetailsModalProps {
  approval: Approval | null;
  isOpen: boolean;
  onClose: () => void;
  onApprove: () => void;
  onReject: () => void;
  onReprompt: () => void;
}

type TabId = 'details' | 'preview' | 'history' | 'edits';

// ─────────────────────────────────────────────────────────────────────────────
// Tab Configuration
// ─────────────────────────────────────────────────────────────────────────────

const tabs: { id: TabId; label: string; icon: React.ReactNode }[] = [
  { id: 'details', label: 'Details', icon: <FileText className="w-4 h-4" /> },
  { id: 'preview', label: 'Preview', icon: <ExternalLink className="w-4 h-4" /> },
  { id: 'history', label: 'History', icon: <History className="w-4 h-4" /> },
  { id: 'edits', label: 'Edits', icon: <Edit3 className="w-4 h-4" /> },
];

// ─────────────────────────────────────────────────────────────────────────────
// Status Badge
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
        'inline-flex items-center px-3 py-1 rounded-full text-sm font-medium',
        variantStyles[variant]
      )}
    >
      {displayName}
    </span>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Details Tab Content
// ─────────────────────────────────────────────────────────────────────────────

function DetailsTab({ approval }: { approval: Approval }) {
  return (
    <div className="space-y-6">
      {/* Identity Section */}
      <div>
        <h4 className="text-sm font-medium text-stone-500 dark:text-stone-400 mb-3">
          Identity
        </h4>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="flex items-center gap-2 text-sm">
            <Database className="w-4 h-4 text-stone-400" />
            <span className="text-stone-500 dark:text-stone-400">Database:</span>
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {approval.databaseName}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <Tag className="w-4 h-4 text-stone-400" />
            <span className="text-stone-500 dark:text-stone-400">Schema:</span>
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {approval.schemaName}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <FileText className="w-4 h-4 text-stone-400" />
            <span className="text-stone-500 dark:text-stone-400">Type:</span>
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {DocumentTypeLabels[approval.documentType] || approval.documentType}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span
              className="w-3 h-3 rounded-full"
              style={{ backgroundColor: priorityColors[approval.priority] }}
            />
            <span className="text-stone-500 dark:text-stone-400">Priority:</span>
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {approval.priority}
            </span>
          </div>
        </div>
      </div>

      {/* References Section */}
      <div>
        <h4 className="text-sm font-medium text-stone-500 dark:text-stone-400 mb-3">
          References
        </h4>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="text-sm">
            <span className="text-stone-500 dark:text-stone-400">CAB Number:</span>
            <span className="ml-2 font-mono text-stone-900 dark:text-stone-100">
              {approval.cabNumber || 'N/A'}
            </span>
          </div>
          {approval.jiraNumber && (
            <div className="text-sm">
              <span className="text-stone-500 dark:text-stone-400">JIRA:</span>
              <a
                href={`https://jira.example.com/browse/${approval.jiraNumber}`}
                target="_blank"
                rel="noopener noreferrer"
                className="ml-2 font-mono text-teal-600 dark:text-teal-400 hover:underline"
              >
                {approval.jiraNumber}
              </a>
            </div>
          )}
          <div className="text-sm">
            <span className="text-stone-500 dark:text-stone-400">Template:</span>
            <span className="ml-2 text-stone-900 dark:text-stone-100">
              {approval.templateUsed}
            </span>
          </div>
          <div className="text-sm">
            <span className="text-stone-500 dark:text-stone-400">Version:</span>
            <span className="ml-2 font-medium text-stone-900 dark:text-stone-100">
              {approval.version}
            </span>
          </div>
        </div>
      </div>

      {/* Workflow Section */}
      <div>
        <h4 className="text-sm font-medium text-stone-500 dark:text-stone-400 mb-3">
          Workflow
        </h4>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="flex items-center gap-2 text-sm">
            <User className="w-4 h-4 text-stone-400" />
            <span className="text-stone-500 dark:text-stone-400">Requested by:</span>
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {approval.requestedBy}
            </span>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <Clock className="w-4 h-4 text-stone-400" />
            <span className="text-stone-500 dark:text-stone-400">Requested:</span>
            <span className="text-stone-900 dark:text-stone-100">
              {formatRelativeTime(approval.requestedAt)}
            </span>
          </div>
          {approval.assignedTo && (
            <div className="flex items-center gap-2 text-sm">
              <User className="w-4 h-4 text-stone-400" />
              <span className="text-stone-500 dark:text-stone-400">Assigned to:</span>
              <span className="font-medium text-stone-900 dark:text-stone-100">
                {approval.assignedTo}
              </span>
            </div>
          )}
          {approval.dueDate && (
            <div className="flex items-center gap-2 text-sm">
              <Calendar className="w-4 h-4 text-stone-400" />
              <span className="text-stone-500 dark:text-stone-400">Due:</span>
              <span
                className={cn(
                  new Date(approval.dueDate) < new Date()
                    ? 'text-red-600 dark:text-red-400 font-medium'
                    : 'text-stone-900 dark:text-stone-100'
                )}
              >
                {formatRelativeTime(approval.dueDate)}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Resolution Section (if resolved) */}
      {approval.resolvedBy && (
        <div>
          <h4 className="text-sm font-medium text-stone-500 dark:text-stone-400 mb-3">
            Resolution
          </h4>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex items-center gap-2 text-sm">
              <User className="w-4 h-4 text-stone-400" />
              <span className="text-stone-500 dark:text-stone-400">Resolved by:</span>
              <span className="font-medium text-stone-900 dark:text-stone-100">
                {approval.resolvedBy}
              </span>
            </div>
            {approval.resolvedAt && (
              <div className="flex items-center gap-2 text-sm">
                <Clock className="w-4 h-4 text-stone-400" />
                <span className="text-stone-500 dark:text-stone-400">Resolved:</span>
                <span className="text-stone-900 dark:text-stone-100">
                  {formatRelativeTime(approval.resolvedAt)}
                </span>
              </div>
            )}
          </div>
          {approval.resolutionNotes && (
            <div className="mt-3 p-3 bg-stone-50 dark:bg-stone-800 rounded-lg">
              <p className="text-sm text-stone-700 dark:text-stone-300">
                {approval.resolutionNotes}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Change Description */}
      {approval.changeDescription && (
        <div>
          <h4 className="text-sm font-medium text-stone-500 dark:text-stone-400 mb-2">
            Change Description
          </h4>
          <p className="text-sm text-stone-700 dark:text-stone-300 bg-stone-50 dark:bg-stone-800 p-3 rounded-lg">
            {approval.changeDescription}
          </p>
        </div>
      )}

      {/* Quality Rating */}
      {approval.qualityRating && (
        <div className="flex items-center gap-2">
          <span className="text-sm text-stone-500 dark:text-stone-400">Quality Rating:</span>
          <div className="flex items-center gap-1">
            {[1, 2, 3, 4, 5].map((star) => (
              <Star
                key={star}
                className={cn(
                  'w-4 h-4',
                  star <= approval.qualityRating!
                    ? 'text-amber-400 fill-amber-400'
                    : 'text-stone-300 dark:text-stone-600'
                )}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Preview Tab Content
// ─────────────────────────────────────────────────────────────────────────────

function PreviewTab({ approval }: { approval: Approval }) {
  const previewUrl = approvalApi.getDocumentPreviewUrl(approval.generatedFilePath);
  const downloadUrl = approvalApi.getDocumentDownloadUrl(approval.generatedFilePath);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-stone-500 dark:text-stone-400">
          {approval.generatedFilePath}
        </p>
        <div className="flex items-center gap-2">
          <a
            href={previewUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1 px-3 py-1.5 text-sm text-teal-600 dark:text-teal-400 hover:bg-teal-50 dark:hover:bg-teal-900/30 rounded-lg transition-colors"
          >
            <ExternalLink className="w-4 h-4" />
            Open
          </a>
          <a
            href={downloadUrl}
            download
            className="flex items-center gap-1 px-3 py-1.5 text-sm text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-800 rounded-lg transition-colors"
          >
            <Download className="w-4 h-4" />
            Download
          </a>
        </div>
      </div>

      <div className="aspect-[4/3] bg-stone-100 dark:bg-stone-800 rounded-lg overflow-hidden">
        <iframe
          src={previewUrl}
          className="w-full h-full border-0"
          title="Document Preview"
        />
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// History Tab Content
// ─────────────────────────────────────────────────────────────────────────────

function HistoryTab({ approval }: { approval: Approval }) {
  const { data: events, isLoading } = useWorkflowEvents(approval.documentId);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="w-6 h-6 text-teal-500 animate-spin" />
      </div>
    );
  }

  if (!events || events.length === 0) {
    return (
      <div className="text-center py-12 text-stone-500 dark:text-stone-400">
        No workflow events recorded
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {events.map((event, idx) => (
        <div
          key={event.eventId}
          className="flex items-start gap-3 p-3 bg-stone-50 dark:bg-stone-800 rounded-lg"
        >
          <div
            className={cn(
              'flex items-center justify-center w-8 h-8 rounded-full flex-shrink-0',
              event.status === 'Success'
                ? 'bg-emerald-100 dark:bg-emerald-900/30'
                : event.status === 'Failed'
                ? 'bg-red-100 dark:bg-red-900/30'
                : 'bg-amber-100 dark:bg-amber-900/30'
            )}
          >
            {event.status === 'Success' ? (
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            ) : event.status === 'Failed' ? (
              <XCircle className="w-4 h-4 text-red-600" />
            ) : (
              <Clock className="w-4 h-4 text-amber-600" />
            )}
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center justify-between gap-2">
              <p className="font-medium text-stone-900 dark:text-stone-100">
                {event.eventType}
              </p>
              <span className="text-xs text-stone-500 dark:text-stone-400">
                {formatRelativeTime(event.timestamp)}
              </span>
            </div>
            {event.message && (
              <p className="text-sm text-stone-600 dark:text-stone-400 mt-1">
                {event.message}
              </p>
            )}
            {event.durationMs && (
              <p className="text-xs text-stone-400 mt-1">
                Duration: {event.durationMs}ms
              </p>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Edits Tab Content
// ─────────────────────────────────────────────────────────────────────────────

function EditsTab({ approval }: { approval: Approval }) {
  const { data: edits, isLoading } = useDocumentEdits(approval.id);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="w-6 h-6 text-teal-500 animate-spin" />
      </div>
    );
  }

  if (!edits || edits.length === 0) {
    return (
      <div className="text-center py-12 text-stone-500 dark:text-stone-400">
        No edits have been made to this document
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {edits.map((edit) => (
        <div
          key={edit.id}
          className="p-4 bg-stone-50 dark:bg-stone-800 rounded-lg space-y-3"
        >
          <div className="flex items-center justify-between">
            <span className="font-medium text-stone-900 dark:text-stone-100">
              {edit.sectionName}
            </span>
            <span className="text-xs px-2 py-1 bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400 rounded">
              {edit.editCategory}
            </span>
          </div>
          <div className="grid gap-2 md:grid-cols-2">
            <div className="p-2 bg-red-50 dark:bg-red-900/20 rounded text-sm">
              <p className="text-xs text-red-600 dark:text-red-400 mb-1">Original</p>
              <p className="text-stone-700 dark:text-stone-300 line-clamp-3">
                {edit.originalText}
              </p>
            </div>
            <div className="p-2 bg-emerald-50 dark:bg-emerald-900/20 rounded text-sm">
              <p className="text-xs text-emerald-600 dark:text-emerald-400 mb-1">Edited</p>
              <p className="text-stone-700 dark:text-stone-300 line-clamp-3">
                {edit.editedText}
              </p>
            </div>
          </div>
          <div className="flex items-center justify-between text-xs text-stone-500 dark:text-stone-400">
            <span>Edited by {edit.editedBy}</span>
            <span>{formatRelativeTime(edit.editedAt)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Modal Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalDetailsModal({
  approval,
  isOpen,
  onClose,
  onApprove,
  onReject,
  onReprompt,
}: ApprovalDetailsModalProps) {
  const [activeTab, setActiveTab] = useState<TabId>('details');

  if (!approval) return null;

  const isPending = approval.status === 'PendingApproval';

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
            initial={{ opacity: 0, scale: 0.95, x: 50 }}
            animate={{ opacity: 1, scale: 1, x: 0 }}
            exit={{ opacity: 0, scale: 0.95, x: 50 }}
            className="fixed inset-y-0 right-0 z-50 w-full max-w-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-full bg-white dark:bg-stone-900 shadow-2xl flex flex-col">
              {/* Header */}
              <div className="flex items-start justify-between px-6 py-4 border-b border-stone-200 dark:border-stone-700">
                <div>
                  <h2 className="text-xl font-semibold text-stone-900 dark:text-stone-100">
                    {approval.objectName.replace(/_/g, ' ')}
                  </h2>
                  <div className="flex items-center gap-3 mt-2">
                    <StatusBadge status={approval.status} />
                    <span className="text-sm text-stone-500 dark:text-stone-400">
                      ID: {approval.id}
                    </span>
                  </div>
                </div>
                <button
                  onClick={onClose}
                  className="p-2 text-stone-400 hover:text-stone-600 dark:hover:text-stone-300 rounded-lg hover:bg-stone-100 dark:hover:bg-stone-800 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Tabs */}
              <div className="flex border-b border-stone-200 dark:border-stone-700 px-6">
                {tabs.map((tab) => (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={cn(
                      'flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 -mb-px transition-colors',
                      activeTab === tab.id
                        ? 'border-teal-500 text-teal-600 dark:text-teal-400'
                        : 'border-transparent text-stone-500 dark:text-stone-400 hover:text-stone-700 dark:hover:text-stone-300'
                    )}
                  >
                    {tab.icon}
                    {tab.label}
                  </button>
                ))}
              </div>

              {/* Content */}
              <div className="flex-1 overflow-y-auto p-6">
                {activeTab === 'details' && <DetailsTab approval={approval} />}
                {activeTab === 'preview' && <PreviewTab approval={approval} />}
                {activeTab === 'history' && <HistoryTab approval={approval} />}
                {activeTab === 'edits' && <EditsTab approval={approval} />}
              </div>

              {/* Footer Actions */}
              {isPending && (
                <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-800/50">
                  <button
                    onClick={onReprompt}
                    className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-blue-600 dark:text-blue-400 border border-blue-300 dark:border-blue-700 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/30 transition-colors"
                  >
                    <RefreshCw className="w-4 h-4" />
                    Re-prompt
                  </button>
                  <button
                    onClick={onReject}
                    className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-lg hover:bg-red-700 transition-colors"
                  >
                    <XCircle className="w-4 h-4" />
                    Reject
                  </button>
                  <button
                    onClick={onApprove}
                    className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 transition-colors"
                  >
                    <CheckCircle2 className="w-4 h-4" />
                    Approve
                  </button>
                </div>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

export default ApprovalDetailsModal;
