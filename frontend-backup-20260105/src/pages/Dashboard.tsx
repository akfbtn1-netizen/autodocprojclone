import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { RefreshCw, TrendingUp, Clock, FileCheck, AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui';
import { KpiGrid, KpiCard } from '@/components/dashboard/KpiCard';
import { WorkflowCanvas } from '@/components/workflow';
import { DocumentList, DocumentListCompact } from '@/components/dashboard/DocumentList';
import { ApprovalQueue } from '@/components/dashboard/ApprovalQueue';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui';
import { useWorkflowStore } from '@/stores';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { Document, ApprovalRequest, KpiData } from '@/types';

// Mock data for demonstration
const mockKpis: KpiData[] = [
  {
    id: 'total-docs',
    label: 'Total Documents',
    value: 1247,
    change: 12.5,
    changeLabel: 'vs last month',
    icon: 'documents',
    variant: 'default',
  },
  {
    id: 'pending-approval',
    label: 'Pending Approval',
    value: 23,
    change: -8.2,
    changeLabel: 'vs last week',
    icon: 'pending',
    variant: 'warning',
  },
  {
    id: 'approved-today',
    label: 'Approved Today',
    value: 47,
    change: 23.1,
    changeLabel: 'vs yesterday',
    icon: 'approved',
    variant: 'success',
  },
  {
    id: 'avg-time',
    label: 'Avg. Processing',
    value: '2.4h',
    change: -15.3,
    changeLabel: 'improvement',
    icon: 'time',
    variant: 'brand',
  },
  {
    id: 'approval-rate',
    label: 'Approval Rate',
    value: '94.2%',
    change: 3.1,
    changeLabel: 'vs last month',
    icon: 'rate',
    variant: 'success',
  },
  {
    id: 'ai-enhanced',
    label: 'AI Enhanced',
    value: 892,
    change: 45.7,
    changeLabel: 'this month',
    icon: 'ai',
    variant: 'brand',
  },
];

const mockDocuments: Document[] = [
  {
    id: 'doc-001',
    docId: 'DF-0001',
    title: 'IRF_Policy_Updates_Q4',
    type: 'policy',
    status: 'pending',
    author: { id: 'user-1', name: 'Sarah Chen', email: 'sarah.chen@company.com' },
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 30 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_GetPolicyData',
      schema: 'gwpc',
      database: 'IRFS1',
      complexity: 'high',
      aiEnhanced: true,
      confidenceScore: 0.92,
    },
  },
  {
    id: 'doc-002',
    docId: 'DF-0002',
    title: 'Claims_Processing_Schema',
    type: 'schema',
    status: 'review',
    author: { id: 'user-2', name: 'Mike Johnson', email: 'mike.johnson@company.com' },
    approvers: [
      { id: 'user-3', name: 'Emily Davis', email: 'emily.davis@company.com' },
    ],
    createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 4 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_ProcessClaim',
      schema: 'DaQa',
      database: 'IRFS1',
      complexity: 'medium',
      aiEnhanced: true,
      confidenceScore: 0.88,
    },
  },
  {
    id: 'doc-003',
    docId: 'DF-0003',
    title: 'Customer_Data_Lineage',
    type: 'lineage',
    status: 'approved',
    author: { id: 'user-4', name: 'Alex Rivera', email: 'alex.rivera@company.com' },
    approvers: [
      { id: 'user-3', name: 'Emily Davis', email: 'emily.davis@company.com' },
      { id: 'user-5', name: 'James Wilson', email: 'james.wilson@company.com' },
    ],
    createdAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_CustomerLineage',
      schema: 'gwpc',
      database: 'IRFS1',
      complexity: 'high',
      aiEnhanced: true,
      confidenceScore: 0.95,
    },
  },
  {
    id: 'doc-004',
    docId: 'DF-0004',
    title: 'Risk_Assessment_Procedures',
    type: 'procedure',
    status: 'draft',
    author: { id: 'user-6', name: 'Lisa Park', email: 'lisa.park@company.com' },
    createdAt: new Date(Date.now() - 1 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_RiskAssessment',
      schema: 'DaQa',
      database: 'IRFS1',
      complexity: 'low',
      aiEnhanced: false,
    },
  },
  {
    id: 'doc-005',
    docId: 'DF-0005',
    title: 'Compliance_Report_Generator',
    type: 'report',
    status: 'completed',
    author: { id: 'user-1', name: 'Sarah Chen', email: 'sarah.chen@company.com' },
    approvers: [
      { id: 'user-3', name: 'Emily Davis', email: 'emily.davis@company.com' },
    ],
    createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_ComplianceReport',
      schema: 'gwpc',
      database: 'IRFS1',
      complexity: 'medium',
      aiEnhanced: true,
      confidenceScore: 0.91,
    },
  },
];

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
    comments: 'Please review the updated policy documentation for Q4 compliance changes.',
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
    comments: 'Schema documentation ready for technical review.',
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
    comments: 'Urgent: Security audit documentation needs immediate approval.',
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
    comments: 'Migration documentation for planned database update.',
  },
];

export function Dashboard() {
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastRefresh, setLastRefresh] = useState(new Date());
  const { setDocuments, setPendingApprovals } = useWorkflowStore();

  // Initialize store with mock data
  useEffect(() => {
    setDocuments(mockDocuments);
    setPendingApprovals(mockApprovals);
  }, [setDocuments, setPendingApprovals]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    // Simulate API refresh
    await new Promise((resolve) => setTimeout(resolve, 1000));
    setLastRefresh(new Date());
    setIsRefreshing(false);
  };

  const handleApprove = (id: string) => {
    console.log('Approve:', id);
    // TODO: Call API
  };

  const handleReject = (id: string) => {
    console.log('Reject:', id);
    // TODO: Call API
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-semibold text-stone-900 dark:text-stone-100">
            Dashboard
          </h1>
          <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
            Monitor your documentation workflow and approval status
          </p>
        </div>

        <div className="flex items-center gap-3">
          <span className="text-xs text-stone-500 dark:text-stone-400">
            Last updated {formatRelativeTime(lastRefresh.toISOString())}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            isLoading={isRefreshing}
            leftIcon={<RefreshCw className="h-4 w-4" />}
          >
            Refresh
          </Button>
        </div>
      </div>

      {/* KPI Cards */}
      <KpiGrid>
        {mockKpis.map((kpi, index) => (
          <KpiCard
            key={kpi.id}
            label={kpi.label}
            value={kpi.value}
            change={kpi.change}
            changeLabel={kpi.changeLabel}
            icon={kpi.icon}
            variant={kpi.variant}
            delay={index}
          />
        ))}
      </KpiGrid>

      {/* Workflow Visualization */}
      <Card variant="elevated" className="overflow-hidden">
        <CardHeader className="border-b border-stone-200 dark:border-stone-700">
          <div className="flex items-center justify-between">
            <CardTitle>Workflow Pipeline</CardTitle>
            <div className="flex items-center gap-2 text-xs text-stone-500 dark:text-stone-400">
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
                Live
              </span>
            </div>
          </div>
        </CardHeader>
        <CardContent padding="none">
          <div className="h-[350px]">
            <WorkflowCanvas />
          </div>
        </CardContent>
      </Card>

      {/* Two Column Layout: Approvals + Recent Documents */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Approval Queue */}
        <Card variant="elevated">
          <CardHeader className="border-b border-stone-200 dark:border-stone-700">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2">
                <AlertCircle className="h-5 w-5 text-amber-500" />
                Pending Approvals
              </CardTitle>
              <span className="rounded-full bg-amber-100 dark:bg-amber-900/50 px-2.5 py-0.5 text-xs font-semibold text-amber-700 dark:text-amber-400">
                {mockApprovals.length} pending
              </span>
            </div>
          </CardHeader>
          <CardContent padding="none">
            <div className="max-h-[400px] overflow-y-auto">
              <ApprovalQueue
                approvals={mockApprovals}
                onApprove={handleApprove}
                onReject={handleReject}
              />
            </div>
          </CardContent>
        </Card>

        {/* Recent Documents */}
        <Card variant="elevated">
          <CardHeader className="border-b border-stone-200 dark:border-stone-700">
            <div className="flex items-center justify-between">
              <CardTitle className="flex items-center gap-2">
                <FileCheck className="h-5 w-5 text-teal-500" />
                Recent Documents
              </CardTitle>
              <Button variant="ghost" size="sm">
                View All
              </Button>
            </div>
          </CardHeader>
          <CardContent padding="none">
            <div className="max-h-[400px] overflow-y-auto">
              <DocumentListCompact
                documents={mockDocuments}
                limit={5}
                onApprove={handleApprove}
                onReject={handleReject}
              />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Activity Timeline (Optional - can add later) */}
      <Card variant="ghost">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-stone-400" />
              Recent Activity
            </CardTitle>
            <span className="text-xs text-stone-500 dark:text-stone-400">
              Last 24 hours
            </span>
          </div>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {[
              { action: 'Document approved', doc: 'Customer_Data_Lineage', user: 'Emily Davis', time: '1 hour ago', type: 'success' },
              { action: 'New document created', doc: 'Risk_Assessment_Procedures', user: 'Lisa Park', time: '2 hours ago', type: 'info' },
              { action: 'Approval requested', doc: 'IRF_Policy_Updates_Q4', user: 'Sarah Chen', time: '3 hours ago', type: 'warning' },
              { action: 'Document generated', doc: 'Compliance_Report_Generator', user: 'AI System', time: '5 hours ago', type: 'brand' },
            ].map((activity, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, x: -10 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: index * 0.1 }}
                className="flex items-center gap-4"
              >
                <div
                  className={cn(
                    'h-2 w-2 rounded-full',
                    activity.type === 'success' && 'bg-emerald-500',
                    activity.type === 'info' && 'bg-blue-500',
                    activity.type === 'warning' && 'bg-amber-500',
                    activity.type === 'brand' && 'bg-teal-500'
                  )}
                />
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-stone-700 dark:text-stone-300">
                    <span className="font-medium">{activity.action}</span>
                    {' · '}
                    <span className="text-stone-500 dark:text-stone-400">{activity.doc}</span>
                  </p>
                  <p className="text-xs text-stone-500 dark:text-stone-400">
                    {activity.user} · {activity.time}
                  </p>
                </div>
              </motion.div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default Dashboard;
