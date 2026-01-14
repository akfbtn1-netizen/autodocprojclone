// ═══════════════════════════════════════════════════════════════════════════
// MasterIndex Detail Component
// Detailed view of a single MasterIndex entry
// ═══════════════════════════════════════════════════════════════════════════

import { useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  ArrowLeft,
  Database,
  Server,
  Table2,
  Columns3,
  Tag,
  Shield,
  Clock,
  User,
  FileText,
  AlertTriangle,
  CheckCircle2,
  ExternalLink,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Card, Badge, Button } from '@/components/ui';
import { getStatusVariant } from '@/components/ui/Badge';
import { useMasterIndexDetail, useMasterIndexSignalR } from '@/hooks/useMasterIndex';
import { useMasterIndexStore } from '@/stores/masterIndexStore';
import type { MasterIndexDetail as MasterIndexDetailType } from '@/types/masterIndex';
import { MasterIndexDisplayNames, TierDescriptions } from '@/types/masterIndex';

// ─────────────────────────────────────────────────────────────────────────────
// Loading Skeleton
// ─────────────────────────────────────────────────────────────────────────────

function DetailSkeleton() {
  return (
    <div className="space-y-6 animate-pulse">
      <div className="h-8 w-48 bg-surface-200 rounded" />
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <Card className="p-6">
            <div className="h-6 w-32 bg-surface-200 rounded mb-4" />
            <div className="space-y-3">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="flex gap-4">
                  <div className="h-4 w-24 bg-surface-100 rounded" />
                  <div className="h-4 w-48 bg-surface-100 rounded" />
                </div>
              ))}
            </div>
          </Card>
        </div>
        <div className="space-y-6">
          <Card className="p-6">
            <div className="h-6 w-32 bg-surface-200 rounded mb-4" />
            <div className="space-y-3">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="h-4 bg-surface-100 rounded" />
              ))}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Detail Section
// ─────────────────────────────────────────────────────────────────────────────

interface DetailSectionProps {
  title: string;
  icon: React.ElementType;
  children: React.ReactNode;
  className?: string;
}

function DetailSection({ title, icon: Icon, children, className }: DetailSectionProps) {
  return (
    <Card className={cn('p-6', className)}>
      <div className="flex items-center gap-2 mb-4">
        <Icon className="w-5 h-5 text-brand-500" />
        <h3 className="text-lg font-semibold text-surface-900">{title}</h3>
      </div>
      {children}
    </Card>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Detail Row
// ─────────────────────────────────────────────────────────────────────────────

interface DetailRowProps {
  label: string;
  value: React.ReactNode;
  className?: string;
}

function DetailRow({ label, value, className }: DetailRowProps) {
  return (
    <div className={cn('flex flex-col sm:flex-row sm:items-start gap-1 sm:gap-4 py-2', className)}>
      <span className="text-sm font-medium text-surface-500 sm:w-40 shrink-0">
        {label}
      </span>
      <span className="text-sm text-surface-900">{value || '-'}</span>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Detail Component
// ─────────────────────────────────────────────────────────────────────────────

export function MasterIndexDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const numericId = id ? parseInt(id, 10) : null;

  // Data hooks
  const { data: detail, isLoading, error } = useMasterIndexDetail(numericId);

  // SignalR for real-time updates
  useMasterIndexSignalR();

  // Clear selection on unmount
  const clearSelection = useMasterIndexStore((state) => state.clearSelection);

  const handleBack = useCallback(() => {
    clearSelection();
    navigate('/catalog');
  }, [navigate, clearSelection]);

  if (isLoading) {
    return (
      <div className="p-6">
        <DetailSkeleton />
      </div>
    );
  }

  if (error || !detail) {
    return (
      <div className="p-6">
        <Card className="p-12 text-center">
          <AlertTriangle className="w-12 h-12 mx-auto text-red-500" />
          <h3 className="mt-4 text-lg font-medium text-surface-900">
            Document Not Found
          </h3>
          <p className="mt-2 text-sm text-surface-500">
            The requested document could not be found or you don't have permission to view it.
          </p>
          <Button variant="outline" className="mt-4" onClick={handleBack}>
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Catalog
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="p-6 space-y-6"
    >
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-start gap-4">
          <Button variant="ghost" size="sm" onClick={handleBack}>
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-bold text-surface-900">
              {detail.physicalName || detail.logicalName || `Document #${detail.indexId}`}
            </h1>
            <p className="text-sm text-surface-500 mt-1">
              {detail.databaseName}.{detail.schemaName}.{detail.physicalName}
              {detail.columnName && `.${detail.columnName}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {detail.approvalStatus && (
            <Badge
              variant={getStatusVariant(detail.approvalStatus)}
              size="lg"
              dot
              pulse={detail.approvalStatus === 'Pending'}
            >
              {detail.approvalStatus}
            </Badge>
          )}
        </div>
      </div>

      {/* Content Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-6">
          {/* Identity Section */}
          <DetailSection title="Identity" icon={Database}>
            <div className="divide-y divide-surface-100">
              <DetailRow label="Index ID" value={detail.indexId} />
              <DetailRow label="Physical Name" value={detail.physicalName} />
              <DetailRow label="Logical Name" value={detail.logicalName} />
              <DetailRow label="Server" value={detail.serverName} />
              <DetailRow label="Database" value={detail.databaseName} />
              <DetailRow label="Schema" value={detail.schemaName} />
              <DetailRow label="Object Type" value={detail.objectType} />
              {detail.columnName && (
                <DetailRow label="Column Name" value={detail.columnName} />
              )}
            </div>
          </DetailSection>

          {/* Documentation Section */}
          <DetailSection title="Documentation" icon={FileText}>
            <div className="space-y-4">
              {detail.description && (
                <div>
                  <h4 className="text-sm font-medium text-surface-600 mb-2">Description</h4>
                  <p className="text-sm text-surface-900 bg-surface-50 p-3 rounded-lg">
                    {detail.description}
                  </p>
                </div>
              )}
              {detail.technicalSummary && (
                <div>
                  <h4 className="text-sm font-medium text-surface-600 mb-2">Technical Summary</h4>
                  <p className="text-sm text-surface-900 bg-surface-50 p-3 rounded-lg">
                    {detail.technicalSummary}
                  </p>
                </div>
              )}
              {detail.businessPurpose && (
                <div>
                  <h4 className="text-sm font-medium text-surface-600 mb-2">Business Purpose</h4>
                  <p className="text-sm text-surface-900 bg-surface-50 p-3 rounded-lg">
                    {detail.businessPurpose}
                  </p>
                </div>
              )}
              {detail.usageNotes && (
                <div>
                  <h4 className="text-sm font-medium text-surface-600 mb-2">Usage Notes</h4>
                  <p className="text-sm text-surface-900 bg-surface-50 p-3 rounded-lg">
                    {detail.usageNotes}
                  </p>
                </div>
              )}
            </div>
          </DetailSection>

          {/* Data Type Section (for columns) */}
          {detail.dataType && (
            <DetailSection title="Data Type" icon={Columns3}>
              <div className="divide-y divide-surface-100">
                <DetailRow label="Data Type" value={detail.dataType} />
                <DetailRow
                  label="Max Length"
                  value={detail.maxLength?.toString()}
                />
                <DetailRow
                  label="Nullable"
                  value={
                    detail.isNullable !== null ? (
                      detail.isNullable ? (
                        <Badge variant="warning" size="sm">Yes</Badge>
                      ) : (
                        <Badge variant="success" size="sm">No</Badge>
                      )
                    ) : (
                      '-'
                    )
                  }
                />
                <DetailRow label="Default Value" value={detail.defaultValue} />
              </div>
            </DetailSection>
          )}
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Classification Section */}
          <DetailSection title="Classification" icon={Tag}>
            <div className="divide-y divide-surface-100">
              <DetailRow label="Category" value={detail.category} />
              <DetailRow label="Sub-Category" value={detail.subCategory} />
              <DetailRow label="Business Domain" value={detail.businessDomain} />
              <DetailRow
                label="Tier"
                value={
                  detail.tier ? (
                    <span title={TierDescriptions[detail.tier]}>
                      Tier {detail.tier}
                    </span>
                  ) : null
                }
              />
              {detail.tags && detail.tags.length > 0 && (
                <div className="py-2">
                  <span className="text-sm font-medium text-surface-500 block mb-2">
                    Tags
                  </span>
                  <div className="flex flex-wrap gap-1">
                    {detail.tags.map((tag) => (
                      <Badge key={tag} variant="brand" size="sm">
                        {tag}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </DetailSection>

          {/* Compliance Section */}
          <DetailSection title="Compliance" icon={Shield}>
            <div className="divide-y divide-surface-100">
              <DetailRow
                label="Contains PII"
                value={
                  detail.piiIndicator !== null ? (
                    detail.piiIndicator ? (
                      <Badge variant="danger" size="sm" dot>
                        <AlertTriangle className="w-3 h-3 mr-1" />
                        Yes
                      </Badge>
                    ) : (
                      <Badge variant="success" size="sm">
                        <CheckCircle2 className="w-3 h-3 mr-1" />
                        No
                      </Badge>
                    )
                  ) : (
                    '-'
                  )
                }
              />
              <DetailRow
                label="Classification"
                value={detail.dataClassification}
              />
              <DetailRow label="Security Level" value={detail.securityLevel} />
            </div>
          </DetailSection>

          {/* Document Info Section */}
          <DetailSection title="Document Info" icon={FileText}>
            <div className="divide-y divide-surface-100">
              <DetailRow label="Version" value={detail.documentVersion} />
              <DetailRow
                label="Generated"
                value={
                  detail.generatedDate
                    ? new Date(detail.generatedDate).toLocaleString()
                    : null
                }
              />
              {detail.generatedDocPath && (
                <DetailRow
                  label="Document Path"
                  value={
                    <a
                      href={detail.generatedDocPath}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-brand-600 hover:underline flex items-center gap-1"
                    >
                      View Document
                      <ExternalLink className="w-3 h-3" />
                    </a>
                  }
                />
              )}
              <DetailRow label="Approved By" value={detail.approvedBy} />
              <DetailRow
                label="Approved Date"
                value={
                  detail.approvedDate
                    ? new Date(detail.approvedDate).toLocaleString()
                    : null
                }
              />
            </div>
          </DetailSection>

          {/* Audit Section */}
          <DetailSection title="Audit Trail" icon={Clock}>
            <div className="divide-y divide-surface-100">
              <DetailRow
                label="Created"
                value={
                  detail.createdDate
                    ? new Date(detail.createdDate).toLocaleString()
                    : null
                }
              />
              <DetailRow label="Created By" value={detail.createdBy} />
              <DetailRow
                label="Modified"
                value={
                  detail.modifiedDate
                    ? new Date(detail.modifiedDate).toLocaleString()
                    : null
                }
              />
              <DetailRow label="Modified By" value={detail.modifiedBy} />
              <DetailRow
                label="Active"
                value={
                  <Badge
                    variant={detail.isActive ? 'success' : 'danger'}
                    size="sm"
                  >
                    {detail.isActive ? 'Yes' : 'No'}
                  </Badge>
                }
              />
            </div>
          </DetailSection>
        </div>
      </div>
    </motion.div>
  );
}

export default MasterIndexDetail;
