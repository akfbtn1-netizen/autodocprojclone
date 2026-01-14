import { useState, useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  Search,
  Filter,
  Plus,
  Grid3X3,
  List,
  SortAsc,
  SortDesc,
  FileText,
  Calendar,
  User,
  Tag,
} from 'lucide-react';
import { Button, Input, Badge, Card, CardContent } from '@/components/ui';
import { Select } from '@/components/ui/Dropdown';
import { DocumentList } from '@/components/dashboard/DocumentList';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { Document, DocumentStatus } from '@/types';

// Extended mock data
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
  {
    id: 'doc-006',
    docId: 'DF-0006',
    title: 'Security_Audit_Procedures',
    type: 'procedure',
    status: 'pending',
    author: { id: 'user-4', name: 'Alex Rivera', email: 'alex.rivera@company.com' },
    createdAt: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_SecurityAudit',
      schema: 'gwpc',
      database: 'IRFS1',
      complexity: 'high',
      aiEnhanced: true,
      confidenceScore: 0.89,
    },
  },
  {
    id: 'doc-007',
    docId: 'DF-0007',
    title: 'Data_Migration_Guide',
    type: 'guide',
    status: 'draft',
    author: { id: 'user-6', name: 'Lisa Park', email: 'lisa.park@company.com' },
    createdAt: new Date(Date.now() - 12 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 8 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_DataMigration',
      schema: 'DaQa',
      database: 'IRFS1',
      complexity: 'high',
      aiEnhanced: false,
    },
  },
  {
    id: 'doc-008',
    docId: 'DF-0008',
    title: 'API_Integration_Specs',
    type: 'specification',
    status: 'completed',
    author: { id: 'user-2', name: 'Mike Johnson', email: 'mike.johnson@company.com' },
    approvers: [
      { id: 'user-5', name: 'James Wilson', email: 'james.wilson@company.com' },
    ],
    createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
    metadata: {
      storedProcedure: 'sp_APIIntegration',
      schema: 'gwpc',
      database: 'IRFS1',
      complexity: 'medium',
      aiEnhanced: true,
      confidenceScore: 0.94,
    },
  },
];

const statusOptions = [
  { value: 'all', label: 'All Statuses' },
  { value: 'draft', label: 'Draft' },
  { value: 'pending', label: 'Pending' },
  { value: 'review', label: 'In Review' },
  { value: 'approved', label: 'Approved' },
  { value: 'completed', label: 'Completed' },
  { value: 'rejected', label: 'Rejected' },
];

const typeOptions = [
  { value: 'all', label: 'All Types' },
  { value: 'policy', label: 'Policy' },
  { value: 'schema', label: 'Schema' },
  { value: 'lineage', label: 'Lineage' },
  { value: 'procedure', label: 'Procedure' },
  { value: 'report', label: 'Report' },
  { value: 'guide', label: 'Guide' },
  { value: 'specification', label: 'Specification' },
];

const sortOptions = [
  { value: 'updated-desc', label: 'Recently Updated' },
  { value: 'updated-asc', label: 'Oldest Updated' },
  { value: 'created-desc', label: 'Recently Created' },
  { value: 'created-asc', label: 'Oldest Created' },
  { value: 'title-asc', label: 'Title (A-Z)' },
  { value: 'title-desc', label: 'Title (Z-A)' },
];

export function Documents() {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [typeFilter, setTypeFilter] = useState('all');
  const [sortBy, setSortBy] = useState('updated-desc');
  const [viewMode, setViewMode] = useState<'list' | 'grid'>('list');

  // Filter and sort documents
  const filteredDocuments = useMemo(() => {
    let result = [...mockDocuments];

    // Search filter
    if (searchQuery) {
      const query = searchQuery.toLowerCase();
      result = result.filter(
        (doc) =>
          doc.title.toLowerCase().includes(query) ||
          doc.docId.toLowerCase().includes(query) ||
          doc.author.name.toLowerCase().includes(query) ||
          doc.metadata?.storedProcedure?.toLowerCase().includes(query)
      );
    }

    // Status filter
    if (statusFilter !== 'all') {
      result = result.filter((doc) => doc.status === statusFilter);
    }

    // Type filter
    if (typeFilter !== 'all') {
      result = result.filter((doc) => doc.type === typeFilter);
    }

    // Sort
    result.sort((a, b) => {
      switch (sortBy) {
        case 'updated-desc':
          return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
        case 'updated-asc':
          return new Date(a.updatedAt).getTime() - new Date(b.updatedAt).getTime();
        case 'created-desc':
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        case 'created-asc':
          return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        case 'title-asc':
          return a.title.localeCompare(b.title);
        case 'title-desc':
          return b.title.localeCompare(a.title);
        default:
          return 0;
      }
    });

    return result;
  }, [searchQuery, statusFilter, typeFilter, sortBy]);

  const handleApprove = (id: string) => {
    console.log('Approve:', id);
  };

  const handleReject = (id: string) => {
    console.log('Reject:', id);
  };

  // Count by status
  const statusCounts = useMemo(() => {
    return mockDocuments.reduce(
      (acc, doc) => {
        acc[doc.status] = (acc[doc.status] || 0) + 1;
        return acc;
      },
      {} as Record<string, number>
    );
  }, []);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-semibold text-stone-900 dark:text-stone-100">
            Documents
          </h1>
          <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
            Manage and track all documentation across your organization
          </p>
        </div>

        <Button
          variant="primary"
          leftIcon={<Plus className="h-4 w-4" />}
        >
          New Document
        </Button>
      </div>

      {/* Quick Stats */}
      <div className="flex flex-wrap gap-2">
        {Object.entries(statusCounts).map(([status, count]) => (
          <button
            key={status}
            onClick={() => setStatusFilter(status)}
            className={cn(
              'flex items-center gap-2 rounded-full px-3 py-1.5 text-sm font-medium transition-all',
              statusFilter === status
                ? 'bg-teal-100 text-teal-700 dark:bg-teal-900/50 dark:text-teal-300'
                : 'bg-stone-100 text-stone-600 hover:bg-stone-200 dark:bg-stone-800 dark:text-stone-400 dark:hover:bg-stone-700'
            )}
          >
            <Badge variant={status as DocumentStatus} size="sm" dot>
              {status.charAt(0).toUpperCase() + status.slice(1)}
            </Badge>
            <span className="text-xs">{count}</span>
          </button>
        ))}
        {statusFilter !== 'all' && (
          <button
            onClick={() => setStatusFilter('all')}
            className="flex items-center gap-1 rounded-full px-3 py-1.5 text-sm font-medium text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200"
          >
            Clear filter
          </button>
        )}
      </div>

      {/* Filters & Search */}
      <Card variant="ghost">
        <CardContent className="p-4">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            {/* Search */}
            <div className="flex-1 max-w-md">
              <Input
                placeholder="Search documents..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                leftIcon={<Search className="h-4 w-4" />}
              />
            </div>

            {/* Filter Controls */}
            <div className="flex flex-wrap items-center gap-3">
              <Select
                value={typeFilter}
                onChange={setTypeFilter}
                options={typeOptions}
                placeholder="Filter by type"
              />

              <Select
                value={sortBy}
                onChange={setSortBy}
                options={sortOptions}
                placeholder="Sort by"
              />

              {/* View Toggle */}
              <div className="flex items-center rounded-lg border border-stone-200 dark:border-stone-700 p-1">
                <button
                  onClick={() => setViewMode('list')}
                  className={cn(
                    'flex items-center justify-center h-8 w-8 rounded-md transition-colors',
                    viewMode === 'list'
                      ? 'bg-stone-100 dark:bg-stone-800 text-stone-900 dark:text-stone-100'
                      : 'text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200'
                  )}
                >
                  <List className="h-4 w-4" />
                </button>
                <button
                  onClick={() => setViewMode('grid')}
                  className={cn(
                    'flex items-center justify-center h-8 w-8 rounded-md transition-colors',
                    viewMode === 'grid'
                      ? 'bg-stone-100 dark:bg-stone-800 text-stone-900 dark:text-stone-100'
                      : 'text-stone-500 hover:text-stone-700 dark:text-stone-400 dark:hover:text-stone-200'
                  )}
                >
                  <Grid3X3 className="h-4 w-4" />
                </button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Results Count */}
      <div className="flex items-center justify-between text-sm text-stone-500 dark:text-stone-400">
        <span>
          Showing {filteredDocuments.length} of {mockDocuments.length} documents
        </span>
      </div>

      {/* Document List */}
      {viewMode === 'list' ? (
        <Card variant="elevated">
          <DocumentList
            documents={filteredDocuments}
            onApprove={handleApprove}
            onReject={handleReject}
          />
        </Card>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filteredDocuments.map((doc, index) => (
            <motion.div
              key={doc.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
            >
              <Card variant="elevated" className="h-full hover:shadow-lg transition-shadow cursor-pointer">
                <CardContent className="p-4">
                  <div className="flex items-start justify-between gap-3 mb-3">
                    <div className="flex items-center gap-2">
                      <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-stone-100 dark:bg-stone-800">
                        <FileText className="h-5 w-5 text-stone-500 dark:text-stone-400" />
                      </div>
                      <div>
                        <p className="font-medium text-stone-900 dark:text-stone-100 text-sm">
                          {doc.docId}
                        </p>
                        <p className="text-xs text-stone-500 dark:text-stone-400">
                          {doc.type}
                        </p>
                      </div>
                    </div>
                    <Badge variant={doc.status} size="sm">
                      {doc.status}
                    </Badge>
                  </div>

                  <h3 className="font-medium text-stone-900 dark:text-stone-100 mb-2 line-clamp-2">
                    {doc.title.replace(/_/g, ' ')}
                  </h3>

                  <div className="space-y-2 text-xs text-stone-500 dark:text-stone-400">
                    <div className="flex items-center gap-2">
                      <User className="h-3.5 w-3.5" />
                      <span>{doc.author.name}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Calendar className="h-3.5 w-3.5" />
                      <span>Updated {formatRelativeTime(doc.updatedAt)}</span>
                    </div>
                    {doc.metadata?.storedProcedure && (
                      <div className="flex items-center gap-2">
                        <Tag className="h-3.5 w-3.5" />
                        <span className="font-mono">{doc.metadata.storedProcedure}</span>
                      </div>
                    )}
                  </div>

                  {doc.metadata?.aiEnhanced && (
                    <div className="mt-3 pt-3 border-t border-stone-100 dark:border-stone-800">
                      <div className="flex items-center justify-between text-xs">
                        <span className="text-teal-600 dark:text-teal-400 font-medium">
                          AI Enhanced
                        </span>
                        {doc.metadata.confidenceScore && (
                          <span className="text-stone-500 dark:text-stone-400">
                            {Math.round(doc.metadata.confidenceScore * 100)}% confidence
                          </span>
                        )}
                      </div>
                    </div>
                  )}
                </CardContent>
              </Card>
            </motion.div>
          ))}
        </div>
      )}

      {/* Empty State */}
      {filteredDocuments.length === 0 && (
        <Card variant="ghost">
          <CardContent className="py-12 text-center">
            <FileText className="h-12 w-12 mx-auto text-stone-300 dark:text-stone-600 mb-4" />
            <h3 className="font-medium text-stone-900 dark:text-stone-100 mb-2">
              No documents found
            </h3>
            <p className="text-sm text-stone-500 dark:text-stone-400 mb-4">
              Try adjusting your search or filter criteria
            </p>
            <Button
              variant="outline"
              onClick={() => {
                setSearchQuery('');
                setStatusFilter('all');
                setTypeFilter('all');
              }}
            >
              Clear all filters
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export default Documents;
