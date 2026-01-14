// ═══════════════════════════════════════════════════════════════════════════
// Pipeline View - End-to-End Visibility
// Swimlane visualization of document pipeline
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  RefreshCw, 
  FileText, 
  Clock, 
  CheckCircle2, 
  XCircle, 
  Zap, 
  Shield,
  ChevronRight,
  ChevronDown,
  ChevronUp
} from 'lucide-react';
import { Card, CardHeader, CardContent } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { usePipelineStatus, usePipelineStages } from '@/hooks/usePipeline';
import { getStageColor, getStageIcon } from '@/services/pipeline';
import { cn } from '@/lib/utils';
import type { PipelineStageData, PipelineActivity } from '@/services/pipeline';

// ─────────────────────────────────────────────────────────────────────────────
// Stage Card Component
// ─────────────────────────────────────────────────────────────────────────────

interface StageCardProps {
  stage: PipelineStageData;
  onItemClick?: (docId: string) => void;
}

function StageCard({ stage, onItemClick }: StageCardProps) {
  const [isExpanded, setIsExpanded] = useState(stage.count > 0 && stage.count <= 5);

  return (
    <div className="flex-shrink-0 w-72">
      <Card 
        variant="elevated" 
        className="h-full"
        style={{ borderTopColor: stage.color, borderTopWidth: 3 }}
      >
        <CardHeader 
          className="pb-2 cursor-pointer"
          onClick={() => setIsExpanded(!isExpanded)}
        >
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-lg">{getStageIcon(stage.stageName)}</span>
              <div>
                <h3 className="font-medium text-stone-900">{stage.displayName}</h3>
                <p className="text-sm text-stone-500">{stage.count} documents</p>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <Badge variant={stage.count > 0 ? 'warning' : 'default'} size="sm">
                {stage.count}
              </Badge>
              {isExpanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
            </div>
          </div>
        </CardHeader>

        <AnimatePresence>
          {isExpanded && (
            <CardContent className="pt-0">
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                className="space-y-2 max-h-96 overflow-y-auto"
              >
                {stage.items.length === 0 ? (
                  <div className="text-center py-4 text-stone-400">
                    <FileText className="h-8 w-8 mx-auto mb-2 opacity-50" />
                    <p className="text-sm">No documents in this stage</p>
                  </div>
                ) : (
                  stage.items.map((item) => (
                    <motion.div
                      key={item.indexId}
                      initial={{ opacity: 0, x: -10 }}
                      animate={{ opacity: 1, x: 0 }}
                      className={cn(
                        'p-3 rounded-lg border border-stone-200',
                        'hover:bg-stone-50 cursor-pointer transition-colors',
                        'group'
                      )}
                      onClick={() => onItemClick?.(item.docId)}
                    >
                      <div className="flex items-start justify-between">
                        <div className="flex-1 min-w-0">
                          <p className="font-medium text-stone-900 truncate group-hover:text-teal-600">
                            {item.documentTitle || 'Untitled Document'}
                          </p>
                          <p className="text-xs text-stone-500 mt-1">
                            {item.objectName || `${item.documentType || 'Unknown'}`}
                          </p>
                          {item.jiraNumber && (
                            <p className="text-xs text-blue-600 mt-1 font-mono">
                              {item.jiraNumber}
                            </p>
                          )}
                        </div>
                        <div className="flex items-center gap-2 ml-2">
                          {item.piiIndicator && (
                            <Shield className="h-3 w-3 text-amber-500" />
                          )}
                          {item.completenessScore && (
                            <span className="text-xs text-stone-500">
                              {Math.round(item.completenessScore * 100)}%
                            </span>
                          )}
                          <ChevronRight className="h-3 w-3 text-stone-400 group-hover:text-teal-500" />
                        </div>
                      </div>
                    </motion.div>
                  ))
                )}
              </motion.div>
            </CardContent>
          )}
        </AnimatePresence>
      </Card>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Pipeline Metrics Summary
// ─────────────────────────────────────────────────────────────────────────────

interface MetricsSummaryProps {
  metrics: {
    totalDocuments: number;
    inStaging: number;
    inDraft: number;
    pendingApproval: number;
    approved: number;
    published: number;
    rejected: number;
    avgCompletenessScore: number;
    avgQualityScore: number;
    piiDocuments: number;
  };
}

function MetricsSummary({ metrics }: MetricsSummaryProps) {
  const items = [
    { label: 'Total', value: metrics.totalDocuments, icon: FileText, color: 'text-stone-600' },
    { label: 'In Pipeline', value: metrics.inStaging + metrics.inDraft + metrics.pendingApproval, icon: Zap, color: 'text-blue-600' },
    { label: 'Pending', value: metrics.pendingApproval, icon: Clock, color: 'text-amber-600' },
    { label: 'Published', value: metrics.published, icon: CheckCircle2, color: 'text-teal-600' },
    { label: 'Rejected', value: metrics.rejected, icon: XCircle, color: 'text-red-600' },
    { label: 'Contains PII', value: metrics.piiDocuments, icon: Shield, color: 'text-amber-600' },
  ];

  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
      {items.map((item) => (
        <div 
          key={item.label}
          className="flex items-center gap-2 p-3 rounded-lg bg-stone-50 dark:bg-stone-800/50"
        >
          <item.icon className={cn('h-5 w-5', item.color)} />
          <div>
            <div className="font-semibold text-stone-900">{item.value}</div>
            <div className="text-xs text-stone-500">{item.label}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Pipeline View Component
// ─────────────────────────────────────────────────────────────────────────────

interface PipelineViewProps {
  onDocumentClick?: (docId: string) => void;
}

export function PipelineView({ onDocumentClick }: PipelineViewProps) {
  const { data: status, isLoading: statusLoading, refetch: refetchStatus } = usePipelineStatus();
  const { data: stages, isLoading: stagesLoading, refetch: refetchStages } = usePipelineStages();

  const isLoading = statusLoading || stagesLoading;

  const handleRefresh = () => {
    refetchStatus();
    refetchStages();
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center py-12">
          <RefreshCw className="h-8 w-8 animate-spin text-stone-400" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-stone-900">Pipeline Overview</h1>
          <p className="text-stone-600 mt-1">End-to-end document workflow visibility</p>
        </div>
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            className="flex items-center gap-2"
          >
            <RefreshCw className="h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      {/* Metrics Summary */}
      {status && <MetricsSummary metrics={status.metrics} />}

      {/* Swimlane View */}
      <Card variant="ghost">
        <CardHeader>
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Document Stages</h2>
            <Badge variant="info" size="sm">
              Live Updates
            </Badge>
          </div>
        </CardHeader>
        <CardContent>
          {stages && stages.length > 0 ? (
            <div className="flex gap-4 overflow-x-auto pb-4">
              {stages.map((stage: PipelineStageData) => (
                <StageCard
                  key={stage.stageName}
                  stage={stage}
                  onItemClick={onDocumentClick ?? (() => {})}
                />
              ))}
            </div>
          ) : (
            <div className="text-center py-8 text-stone-500">
              <FileText className="h-12 w-12 mx-auto mb-3 opacity-50" />
              <p>No pipeline data available</p>
              <p className="text-sm mt-1">Documents will appear here as they flow through the system</p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Recent Activity */}
      {status && status.recentActivity.length > 0 && (
        <Card variant="elevated">
          <CardHeader>
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Recent Activity</h2>
              <Badge variant="default" size="sm">
                Last 24h
              </Badge>
            </div>
          </CardHeader>
          <CardContent>
            <div className="space-y-3 max-h-64 overflow-y-auto">
              {status.recentActivity.slice(0, 10).map((activity: PipelineActivity) => (
                <div
                  key={activity.indexId}
                  className="flex items-center gap-3 p-2 hover:bg-stone-50 rounded-lg cursor-pointer"
                  onClick={() => onDocumentClick?.(activity.docId)}
                >
                  <div 
                    className="w-2 h-2 rounded-full"
                    style={{ backgroundColor: getStageColor(activity.workflowStatus || '') }}
                  />
                  <div className="flex-1">
                    <p className="text-sm font-medium">{activity.documentTitle || 'Untitled'}</p>
                    <p className="text-xs text-stone-500">
                      {activity.schemaName}.{activity.tableName}
                      {activity.jiraNumber && ` • ${activity.jiraNumber}`}
                    </p>
                  </div>
                  <div className="text-xs text-stone-500">
                    {new Date(activity.modifiedDate).toLocaleDateString()}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Pipeline Status Bar for Dashboard
// ─────────────────────────────────────────────────────────────────────────────

export function PipelineStatusBar() {
  const { data: status, isLoading } = usePipelineStatus();

  if (isLoading || !status) {
    return <div className="h-8 w-48 animate-pulse bg-stone-100 rounded" />;
  }

  const { metrics } = status;
  const inPipeline = metrics.inStaging + metrics.inDraft + metrics.pendingApproval;

  return (
    <div className="flex items-center gap-4 text-sm">
      <div className="flex items-center gap-1.5">
        <div className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
        <span className="text-stone-600">{inPipeline} in pipeline</span>
      </div>
      {metrics.pendingApproval > 0 && (
        <div className="flex items-center gap-1.5">
          <Clock className="h-3.5 w-3.5 text-amber-500" />
          <span className="text-amber-700">{metrics.pendingApproval} pending</span>
        </div>
      )}
      {metrics.rejected > 0 && (
        <div className="flex items-center gap-1.5">
          <XCircle className="h-3.5 w-3.5 text-red-500" />
          <span className="text-red-700">{metrics.rejected} rejected</span>
        </div>
      )}
    </div>
  );
}

export default PipelineView;