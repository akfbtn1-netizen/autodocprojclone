// Dashboard Page - Real API Integration
import { useState } from 'react';
import { motion } from 'framer-motion';
import { RefreshCw, FileText, Clock, CheckCircle2, Sparkles, Shield, TrendingUp, AlertCircle, Activity } from 'lucide-react';
import { useDashboardKpis, usePendingApprovals, useRecentDocuments, useRecentActivity, useApprovalHub } from '@/hooks';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { WorkflowCanvas } from '@/components/workflow';
import { AgentPanel, AgentStatusIndicator } from '@/components/agents/AgentPanel';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { KpiCardData, ApprovalRequest, Document, ActivityItem } from '@/types';

const kpiIcons: Record<string, React.ReactNode> = {
  documents: <FileText className="h-5 w-5" />,
  pending: <Clock className="h-5 w-5" />,
  approved: <CheckCircle2 className="h-5 w-5" />,
  time: <Clock className="h-5 w-5" />,
  rate: <TrendingUp className="h-5 w-5" />,
  ai: <Sparkles className="h-5 w-5" />,
  pii: <Shield className="h-5 w-5" />,
  quality: <Activity className="h-5 w-5" />,
};

const variantStyles: Record<string, string> = {
  default: 'bg-stone-100 text-stone-600 dark:bg-stone-700',
  brand: 'bg-teal-100 text-teal-600 dark:bg-teal-900/50',
  success: 'bg-emerald-100 text-emerald-600 dark:bg-emerald-900/50',
  warning: 'bg-amber-100 text-amber-600 dark:bg-amber-900/50',
  danger: 'bg-red-100 text-red-600 dark:bg-red-900/50',
};

function KpiCard({ label, value, change, changeLabel, icon, variant = 'default', delay = 0 }: KpiCardData & { delay?: number }) {
  return (
    <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: delay * 0.1 }}>
      <Card variant="elevated">
        <CardContent className="p-4">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-sm text-stone-500">{label}</p>
              <p className="mt-1 text-2xl font-semibold text-stone-900 dark:text-stone-100">{value}</p>
              {change !== undefined && (
                <p className="mt-1 text-xs text-stone-500">{change > 0 && '+'}{change}% {changeLabel}</p>
              )}
            </div>
            <div className={cn('rounded-xl p-3', variantStyles[variant])}>{kpiIcons[icon]}</div>
          </div>
        </CardContent>
      </Card>
    </motion.div>
  );
}

function ApprovalQueueItem({ approval, onApprove, onReject }: { approval: ApprovalRequest; onApprove: (id: string) => void; onReject: (id: string) => void }) {
  const priorityColors: Record<string, string> = { urgent: 'border-l-red-500', high: 'border-l-amber-500', medium: 'border-l-teal-500', low: 'border-l-stone-300' };
  return (
    <div className={cn('border-l-4 px-4 py-3 hover:bg-stone-50 dark:hover:bg-stone-800/50', priorityColors[approval.priority])}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex-1 min-w-0">
          <p className="font-medium text-stone-900 dark:text-stone-100 truncate">{approval.documentTitle}</p>
          <p className="text-sm text-stone-500">{approval.requester.name} · {formatRelativeTime(approval.requestedAt)}</p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant={approval.priority === 'urgent' ? 'danger' : approval.priority === 'high' ? 'warning' : 'default'} size="sm">{approval.priority}</Badge>
          <Button variant="ghost" size="sm" onClick={() => onApprove(approval.id)}>✓</Button>
          <Button variant="ghost" size="sm" onClick={() => onReject(approval.id)}>✗</Button>
        </div>
      </div>
    </div>
  );
}

function RecentDocumentItem({ document }: { document: Document }) {
  const statusColors: Record<string, string> = { draft: 'bg-stone-100 text-stone-600', pending: 'bg-amber-100 text-amber-600', approved: 'bg-emerald-100 text-emerald-600', completed: 'bg-teal-100 text-teal-600' };
  return (
    <div className="flex items-center gap-3 px-4 py-3 hover:bg-stone-50 dark:hover:bg-stone-800/50">
      <div className={cn('flex h-10 w-10 items-center justify-center rounded-lg', statusColors[document.status] || statusColors.draft)}>
        <FileText className="h-5 w-5" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="font-medium text-stone-900 dark:text-stone-100 truncate">{document.title}</p>
        <p className="text-xs text-stone-500">{document.docId} · {formatRelativeTime(document.updatedAt)}</p>
      </div>
      <div className="flex items-center gap-2">
        {document.metadata?.piiIndicator && <Shield className="h-4 w-4 text-amber-500" />}
        {document.metadata?.aiEnhanced && <Sparkles className="h-4 w-4 text-teal-500" />}
        <Badge variant="default" size="sm">{document.status}</Badge>
      </div>
    </div>
  );
}

export function Dashboard() {
  const [lastRefresh, setLastRefresh] = useState(new Date());
  const { data: kpis, isLoading: kpisLoading, refetch: refetchKpis } = useDashboardKpis();
  const { data: approvals, isLoading: approvalsLoading, refetch: refetchApprovals } = usePendingApprovals();
  const { data: documents, isLoading: documentsLoading, refetch: refetchDocuments } = useRecentDocuments(5);
  const { data: activity, refetch: refetchActivity } = useRecentActivity(10);
  const { isConnected, approveDocument, rejectDocument } = useApprovalHub();

  const handleRefresh = async () => {
    await Promise.all([refetchKpis(), refetchApprovals(), refetchDocuments(), refetchActivity()]);
    setLastRefresh(new Date());
  };

  const kpiCards: KpiCardData[] = kpis ? [
    { id: 'total-docs', label: 'Total Documents', value: kpis.totalDocuments.toLocaleString(), icon: 'documents', variant: 'default' },
    { id: 'pending', label: 'Pending Approval', value: kpis.pendingApprovals, icon: 'pending', variant: 'warning' },
    { id: 'approved-today', label: 'Approved Today', value: kpis.approvedToday, icon: 'approved', variant: 'success' },
    { id: 'avg-time', label: 'Avg. Processing', value: `${kpis.avgProcessingTimeHours?.toFixed(1) || 0}h`, icon: 'time', variant: 'brand' },
    { id: 'completion-rate', label: 'Completion Rate', value: `${kpis.completionRate?.toFixed(1) || 0}%`, icon: 'rate', variant: 'success' },
    { id: 'ai-enhanced', label: 'AI Enhanced', value: kpis.aiEnhancedCount?.toLocaleString() || 0, change: kpis.aiEnhancedPercentage, changeLabel: 'of total', icon: 'ai', variant: 'brand' },
  ] : [];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-semibold text-stone-900 dark:text-stone-100">Dashboard</h1>
          <p className="mt-1 text-sm text-stone-500">Monitor your documentation workflow</p>
        </div>
        <div className="flex items-center gap-3">
          <span className={cn('h-2 w-2 rounded-full', isConnected ? 'bg-emerald-500 animate-pulse' : 'bg-stone-400')} />
          <AgentStatusIndicator />
          <span className="text-xs text-stone-500">Updated {formatRelativeTime(lastRefresh.toISOString())}</span>
          <Button variant="outline" size="sm" onClick={handleRefresh} isLoading={kpisLoading} leftIcon={<RefreshCw className="h-4 w-4" />}>Refresh</Button>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        {kpisLoading ? [1,2,3,4,5,6].map(i => <div key={i} className="h-28 animate-pulse rounded-xl bg-stone-100 dark:bg-stone-800" />) : kpiCards.map((kpi, i) => <KpiCard key={kpi.id} {...kpi} delay={i} />)}
      </div>

      <Card variant="elevated" className="overflow-hidden">
        <CardHeader className="border-b border-stone-200 dark:border-stone-700">
          <CardTitle>Workflow Pipeline</CardTitle>
        </CardHeader>
        <CardContent padding="none"><div className="h-[350px]"><WorkflowCanvas isConnected={isConnected} /></div></CardContent>
      </Card>

      <AgentPanel />

      <div className="grid gap-6 lg:grid-cols-2">
        <Card variant="elevated">
          <CardHeader className="border-b border-stone-200 dark:border-stone-700">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2"><AlertCircle className="h-5 w-5 text-amber-500" />Pending Approvals</CardTitle>
              <Badge variant="warning" size="sm">{approvals?.approvals?.length ?? 0} pending</Badge>
            </div>
          </CardHeader>
          <CardContent padding="none">
            <div className="max-h-[400px] overflow-y-auto divide-y divide-stone-100 dark:divide-stone-700">
              {approvalsLoading ? <div className="p-4"><div className="h-16 animate-pulse bg-stone-100 rounded" /></div> : 
               approvals?.approvals?.length === 0 ? <div className="text-center py-8 text-stone-500">All caught up!</div> :
               approvals?.approvals?.map(a => <ApprovalQueueItem key={a.id} approval={a} onApprove={(id) => approveDocument(id)} onReject={(id) => rejectDocument(id, 'Rejected')} />)}
            </div>
          </CardContent>
        </Card>

        <Card variant="elevated">
          <CardHeader className="border-b border-stone-200 dark:border-stone-700">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2"><FileText className="h-5 w-5 text-teal-500" />Recent Documents</CardTitle>
              <Button variant="ghost" size="sm">View All</Button>
            </div>
          </CardHeader>
          <CardContent padding="none">
            <div className="max-h-[400px] overflow-y-auto divide-y divide-stone-100 dark:divide-stone-700">
              {documentsLoading ? <div className="p-4"><div className="h-16 animate-pulse bg-stone-100 rounded" /></div> :
               documents?.length === 0 ? <div className="text-center py-8 text-stone-500">No documents yet</div> :
               documents?.map(doc => <RecentDocumentItem key={doc.id} document={doc} />)}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export default Dashboard;
