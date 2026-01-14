import { motion } from 'framer-motion';
import {
  FileText,
  MoreVertical,
  Clock,
  User,
  ExternalLink,
  CheckCircle2,
  XCircle,
  Eye,
} from 'lucide-react';
import { formatRelativeTime, cn } from '@/lib/utils';
import {
  Card,
  Button,
  Badge,
  getStatusVariant,
  Avatar,
  Dropdown,
  DropdownItem,
  DropdownSeparator,
} from '@/components/ui';
import type { Document } from '@/types';

interface DocumentListProps {
  documents: Document[];
  onApprove?: (id: string) => void;
  onReject?: (id: string) => void;
  onView?: (id: string) => void;
  isLoading?: boolean;
}

export function DocumentList({
  documents,
  onApprove,
  onReject,
  onView,
  isLoading,
}: DocumentListProps) {
  if (isLoading) {
    return (
      <Card>
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="flex items-center gap-4 p-4 animate-pulse">
              <div className="w-10 h-10 bg-stone-200 dark:bg-stone-700 rounded-xl" />
              <div className="flex-1 space-y-2">
                <div className="h-4 bg-stone-200 dark:bg-stone-700 rounded w-1/3" />
                <div className="h-3 bg-stone-100 dark:bg-stone-800 rounded w-1/2" />
              </div>
              <div className="h-6 w-16 bg-stone-200 dark:bg-stone-700 rounded-full" />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  if (documents.length === 0) {
    return (
      <Card className="text-center py-12">
        <FileText className="w-12 h-12 mx-auto text-stone-300 dark:text-stone-600 mb-4" />
        <h3 className="text-lg font-medium text-stone-700 dark:text-stone-300 mb-1">
          No documents found
        </h3>
        <p className="text-sm text-stone-500 dark:text-stone-400">
          Documents will appear here when created
        </p>
      </Card>
    );
  }

  return (
    <Card padding="none" className="overflow-hidden">
      <div className="divide-y divide-stone-100 dark:divide-stone-800">
        {documents.map((doc, index) => (
          <DocumentRow
            key={doc.id}
            document={doc}
            index={index}
            onApprove={onApprove}
            onReject={onReject}
            onView={onView}
          />
        ))}
      </div>
    </Card>
  );
}

// Individual document row
interface DocumentRowProps {
  document: Document;
  index: number;
  onApprove?: (id: string) => void;
  onReject?: (id: string) => void;
  onView?: (id: string) => void;
}

function DocumentRow({
  document,
  index,
  onApprove,
  onReject,
  onView,
}: DocumentRowProps) {
  const isPending = document.status === 'pending' || document.status === 'review';

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ duration: 0.3, delay: index * 0.05 }}
      className="flex items-center gap-4 p-4 hover:bg-stone-50 dark:hover:bg-stone-800/50 transition-colors group"
    >
      {/* Icon */}
      <div className="flex items-center justify-center w-10 h-10 rounded-xl bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400 group-hover:bg-teal-100 dark:group-hover:bg-teal-900/30 group-hover:text-teal-600 dark:group-hover:text-teal-400 transition-colors">
        <FileText className="w-5 h-5" />
      </div>

      {/* Document info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-1">
          <h4 className="font-medium text-stone-900 dark:text-stone-100 truncate">
            {document.title.replace(/_/g, ' ')}
          </h4>
          <Badge variant="info" size="sm">
            {document.type}
          </Badge>
        </div>
        <div className="flex items-center gap-3 text-xs text-stone-500 dark:text-stone-400">
          <span className="flex items-center gap-1">
            <Clock className="w-3 h-3" />
            {formatRelativeTime(document.updatedAt)}
          </span>
          <span className="flex items-center gap-1">
            <User className="w-3 h-3" />
            {document.author.name}
          </span>
          {document.jiraTicket && (
            <span className="font-mono">{document.jiraTicket}</span>
          )}
        </div>
      </div>

      {/* Status */}
      <Badge variant={getStatusVariant(document.status)} dot>
        {document.status.charAt(0).toUpperCase() + document.status.slice(1)}
      </Badge>

      {/* Approvers */}
      {document.approvers && document.approvers.length > 0 && (
        <Avatar
          name={document.approvers[0].name}
          size="sm"
        />
      )}

      {/* Quick Actions for pending */}
      {isPending && (
        <div className="flex items-center gap-2">
          <Button
            variant="success"
            size="sm"
            onClick={() => onApprove?.(document.id)}
            leftIcon={<CheckCircle2 className="w-3.5 h-3.5" />}
          >
            Approve
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onReject?.(document.id)}
            leftIcon={<XCircle className="w-3.5 h-3.5" />}
          >
            Reject
          </Button>
        </div>
      )}

      {/* More menu */}
      <Dropdown
        align="right"
        trigger={
          <Button variant="ghost" size="icon-sm">
            <MoreVertical className="w-4 h-4" />
          </Button>
        }
      >
        <DropdownItem icon={<Eye className="w-4 h-4" />} onClick={() => onView?.(document.id)}>
          View Details
        </DropdownItem>
        <DropdownItem icon={<ExternalLink className="w-4 h-4" />}>
          Open Document
        </DropdownItem>
        <DropdownSeparator />
        <DropdownItem icon={<FileText className="w-4 h-4" />}>
          View History
        </DropdownItem>
      </Dropdown>
    </motion.div>
  );
}

// Compact version for sidebars/cards
interface DocumentListCompactProps {
  documents: Document[];
  onView?: (id: string) => void;
  onApprove?: (id: string) => void;
  onReject?: (id: string) => void;
  title?: string;
  limit?: number;
}

export function DocumentListCompact({
  documents,
  onView,
  title = 'Recent Documents',
  limit = 5,
}: DocumentListCompactProps) {
  const displayDocs = documents.slice(0, limit);

  return (
    <div className="divide-y divide-stone-100 dark:divide-stone-800">
      {displayDocs.map((doc) => (
        <button
          key={doc.id}
          onClick={() => onView?.(doc.id)}
          className="flex items-center gap-3 w-full p-4 hover:bg-stone-50 dark:hover:bg-stone-800/50 transition-colors text-left"
        >
          <div className="flex items-center justify-center w-8 h-8 rounded-lg bg-stone-100 dark:bg-stone-800 text-stone-500 dark:text-stone-400">
            <FileText className="w-4 h-4" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-stone-800 dark:text-stone-200 truncate">
              {doc.title.replace(/_/g, ' ')}
            </p>
            <p className="text-xs text-stone-400 dark:text-stone-500">
              {formatRelativeTime(doc.updatedAt)}
            </p>
          </div>
          <Badge variant={getStatusVariant(doc.status)} size="sm">
            {doc.status}
          </Badge>
        </button>
      ))}

      {documents.length > limit && (
        <div className="p-4">
          <Button variant="ghost" size="sm" className="w-full">
            View all {documents.length} documents
          </Button>
        </div>
      )}
    </div>
  );
}
