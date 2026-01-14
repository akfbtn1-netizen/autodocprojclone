// ═══════════════════════════════════════════════════════════════════════════
// Enterprise Documentation Platform V2 - Type Definitions
// Matches: MasterIndex (119 columns), Multi-Agent System, Approval Workflow
// ═══════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// DOCUMENT TYPES
// ─────────────────────────────────────────────────────────────────────────────

export type DocumentStatus =
  | 'draft'
  | 'pending'
  | 'review'
  | 'approved'
  | 'completed'
  | 'rejected'
  | 'archived';

export type DocumentType =
  | 'EN'  // Enhancement
  | 'BR'  // Business Request
  | 'DF'  // Defect Fix
  | 'SP'  // Stored Procedure
  | 'QA'; // QA Documentation

export const DocumentTypeLabels: Record<DocumentType, string> = {
  EN: 'Enhancement',
  BR: 'Business Request',
  DF: 'Defect Fix',
  SP: 'Stored Procedure',
  QA: 'QA Documentation',
};

export interface User {
  id: string;
  name: string;
  email: string;
  role?: 'admin' | 'approver' | 'viewer' | 'developer';
  avatar?: string;
  department?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// MASTER INDEX METADATA (119 Columns - Key Fields)
// ─────────────────────────────────────────────────────────────────────────────

export interface MasterIndexMetadata {
  // Identity (Tier 1)
  indexId?: number;
  docId: string;
  sourceSystem?: string;
  sourceDocumentId?: string;
  sourceFilePath?: string;

  // Document Info
  documentTitle?: string;
  documentType: DocumentType;
  description?: string;
  generatedDocPath?: string;
  generatedDocUrl?: string;
  fileSize?: number;
  fileHash?: string;
  versionNumber?: string;
  isLatestVersion?: boolean;

  // Database Object Details
  systemName?: string;        // e.g., "IRFS1"
  databaseName?: string;      // e.g., "IRFS1"
  schemaName?: string;        // e.g., "gwpc", "DaQa", "gwpcDaily"
  tableName?: string;
  columnName?: string;
  dataType?: string;
  maxLength?: number;
  isNullable?: boolean;
  defaultValue?: string;

  // Business Context (HIGH PRIORITY)
  businessDomain?: BusinessDomain;
  businessProcess?: string;
  businessOwner?: string;
  technicalOwner?: string;
  businessDefinition?: string;
  technicalDefinition?: string;
  businessGlossaryTerms?: string[];

  // Classification & Compliance (HIGH PRIORITY)
  dataClassification?: DataClassification;
  sensitivity?: string;
  sensitivityLevel?: SensitivityLevel;
  complianceTags?: ComplianceTag[];
  piiIndicator?: boolean;
  containsPii?: boolean;
  piiTypes?: PIIType[];
  retentionPolicy?: string;
  accessRequirements?: string;

  // AI-Generated Metadata (HIGH PRIORITY)
  semanticCategory?: SemanticCategory;
  aiGeneratedTags?: string[];
  keywords?: string;
  recommendedIndexes?: string;
  optimizationSuggestions?: string;

  // Relationships & Dependencies
  relatedDocuments?: string[];
  relatedTables?: string[];
  relatedColumns?: string[];
  foreignKeys?: string[];
  dependencies?: string[];
  upstreamSystems?: string[];
  downstreamSystems?: string[];
  parentTables?: string[];
  childTables?: string[];
  commonJoins?: string[];
  storedProcedures?: string[];

  // Quality & Validation
  qualityScore?: number;
  completenessScore?: number;
  metadataCompleteness?: number;
  lastValidated?: string;
  validationStatus?: 'valid' | 'invalid' | 'pending';

  // Usage & Examples
  usageExamples?: string;
  commonQueries?: string;
  sampleData?: string;
  accessCount?: number;
  lastAccessedDate?: string;
  knownIssues?: string;
  performanceNotes?: string;

  // Workflow & Status
  status?: 'active' | 'deprecated' | 'archived';
  workflowStatus?: string;
  approvalStatus?: ApprovalStatus;
  approvedBy?: string;
  approvedDate?: string;
  criticalityLevel?: CriticalityLevel;

  // Audit & Lifecycle
  createdDate?: string;
  createdBy?: string;
  modifiedDate?: string;
  modifiedBy?: string;
  isDeleted?: boolean;
  deletedDate?: string;
  deletedBy?: string;
  lastSchemaChange?: string;
  technicalComplexity?: TechnicalComplexity;

  // JIRA Integration
  cabNumber?: string;
  jiraTicket?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// ENUM TYPES (Matching Backend)
// ─────────────────────────────────────────────────────────────────────────────

export type BusinessDomain =
  | 'Policy Management'
  | 'Claims Processing'
  | 'Billing & Finance'
  | 'Customer Data'
  | 'Agent & Producer'
  | 'Reference Data'
  | 'Underwriting'
  | 'Reporting & Analytics'
  | 'System & Audit'
  | 'Document Management'
  | 'Data Quality & Analytics'
  | 'Multi-Family Policy'
  | 'Core System'
  | 'ETL & Staging'
  | 'Archive & History'
  | 'General';

export type SemanticCategory =
  | 'Policy Management'
  | 'Claims Processing'
  | 'Billing & Finance'
  | 'Customer Data'
  | 'Agent & Producer'
  | 'Reference Data'
  | 'Underwriting'
  | 'Reporting & Analytics'
  | 'System & Audit'
  | 'Document Management'
  | 'General';

export type DataClassification =
  | 'Public'
  | 'Internal'
  | 'Confidential'
  | 'Restricted';

export type SensitivityLevel =
  | 'None'
  | 'Low'
  | 'Medium'
  | 'High'
  | 'Critical';

export type ComplianceTag =
  | 'SOX'
  | 'HIPAA'
  | 'PCI-DSS'
  | 'GLBA'
  | 'State Insurance'
  | 'GDPR'
  | 'CCPA'
  | 'NAIC';

export type PIIType =
  | 'SSN'
  | 'DOB'
  | 'Email'
  | 'Phone'
  | 'Address'
  | 'Name'
  | 'Account'
  | 'License'
  | 'Medical'
  | 'Financial';

export type CriticalityLevel =
  | 'Low'
  | 'Medium'
  | 'High'
  | 'Critical';

export type TechnicalComplexity =
  | 'Low'
  | 'Medium'
  | 'High';

export type ApprovalStatus =
  | 'pending'
  | 'approved'
  | 'rejected'
  | 'cancelled';

// ─────────────────────────────────────────────────────────────────────────────
// DOCUMENT (Full Entity)
// ─────────────────────────────────────────────────────────────────────────────

export interface Document {
  id: string;
  docId: string;
  title: string;
  description?: string;
  type: DocumentType;
  status: DocumentStatus;
  author: User;
  approvers?: User[];
  jiraTicket?: string;
  cabNumber?: string;
  createdAt: string;
  updatedAt: string;
  approvalHistory?: ApprovalHistoryEntry[];
  metadata: MasterIndexMetadata;
}

export interface ApprovalHistoryEntry {
  id: string;
  action: 'submitted' | 'approved' | 'rejected' | 'commented' | 'escalated';
  actor: string;
  timestamp: string;
  comment?: string;
  tier?: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// AGENT TYPES (Multi-Agent System)
// ─────────────────────────────────────────────────────────────────────────────

export type AgentType =
  | 'SchemaDetector'
  | 'DocGenerator'
  | 'ExcelChangeIntegrator'
  | 'MetadataManager';

export type AgentStatus =
  | 'idle'
  | 'processing'
  | 'error'
  | 'stopped'
  | 'starting';

export interface Agent {
  id: string;
  name: string;
  type: AgentType;
  status: AgentStatus;
  description: string;
  lastActivity?: string;
  lastHeartbeat?: string;
  processedCount: number;
  processedToday: number;
  errorCount: number;
  queueDepth: number;
  avgProcessingTimeMs?: number;
  currentTask?: string;
  version?: string;
  healthCheckUrl?: string;
}

export const AgentDescriptions: Record<AgentType, string> = {
  SchemaDetector: 'Monitors database schema changes and triggers documentation updates',
  DocGenerator: 'Generates Word documents using AI-enhanced templates',
  ExcelChangeIntegrator: 'Watches Excel spreadsheets for change requests and syncs to database',
  MetadataManager: 'Populates MasterIndex with extracted and inferred metadata',
};

export interface AgentActivity {
  id: string;
  agentType: AgentType;
  action: string;
  timestamp: string;
  duration?: number;
  success: boolean;
  details?: string;
  documentId?: string;
  error?: string;
}

// ─────────────────────────────────────────────────────────────────────────────
// WORKFLOW TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface WorkflowStage {
  id: string;
  name: string;
  status: 'waiting' | 'active' | 'completed' | 'skipped' | 'error';
  position: { x: number; y: number };
  documentCount: number;
  avgProcessingTime?: string;
  agent?: AgentType;
}

export interface WorkflowConnection {
  id: string;
  source: string;
  target: string;
  animated?: boolean;
  label?: string;
}

export interface WorkflowStats {
  totalDocuments: number;
  pendingApprovals: number;
  approvedToday: number;
  rejectedToday: number;
  avgProcessingTime: string;
  completionRate: number;
  aiEnhancedCount: number;
  piiDocumentsCount: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// APPROVAL TYPES
// ─────────────────────────────────────────────────────────────────────────────

export type ApprovalPriority = 'low' | 'medium' | 'high' | 'urgent';

export interface ApprovalRequest {
  id: string;
  documentId: string;
  docId: string;
  documentTitle: string;
  documentType: DocumentType;
  requester: User;
  requestedAt: string;
  dueDate?: string;
  priority: ApprovalPriority;
  status: ApprovalStatus;
  comments?: string;
  reviewedAt?: string;
  reviewer?: User;
  rejectionReason?: string;
  tier: number;
  maxTiers: number;
  // Metadata for routing
  businessDomain?: BusinessDomain;
  containsPii?: boolean;
  criticalityLevel?: CriticalityLevel;
}

// ─────────────────────────────────────────────────────────────────────────────
// LINEAGE TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface LineageNode {
  id: string;
  type: 'table' | 'column' | 'procedure' | 'view' | 'external';
  name: string;
  schema?: string;
  database?: string;
  metadata?: {
    dataType?: string;
    businessDomain?: BusinessDomain;
    piiIndicator?: boolean;
    description?: string;
  };
}

export interface LineageEdge {
  id: string;
  source: string;
  target: string;
  type: 'direct' | 'transform' | 'derived' | 'reference';
  transformation?: string;
  confidence?: number;
}

export interface LineageGraph {
  nodes: LineageNode[];
  edges: LineageEdge[];
  rootNodeId?: string;
  depth: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// SEARCH & FILTER TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface SearchFilters {
  query?: string;
  documentTypes?: DocumentType[];
  statuses?: DocumentStatus[];
  businessDomains?: BusinessDomain[];
  semanticCategories?: SemanticCategory[];
  schemas?: string[];
  containsPii?: boolean;
  dataClassifications?: DataClassification[];
  complianceTags?: ComplianceTag[];
  criticalityLevels?: CriticalityLevel[];
  dateRange?: {
    start: string;
    end: string;
  };
  completenessScoreMin?: number;
  qualityScoreMin?: number;
}

export interface SearchResult {
  documents: Document[];
  total: number;
  facets: SearchFacets;
  page: number;
  pageSize: number;
}

export interface SearchFacets {
  documentTypes: FacetCount[];
  businessDomains: FacetCount[];
  semanticCategories: FacetCount[];
  schemas: FacetCount[];
  dataClassifications: FacetCount[];
  complianceTags: FacetCount[];
  statuses: FacetCount[];
}

export interface FacetCount {
  value: string;
  count: number;
}

// ─────────────────────────────────────────────────────────────────────────────
// API TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
  meta?: PaginationMeta;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, string[]>;
  traceId?: string;
}

export type Result<T> =
  | { success: true; data: T }
  | { success: false; error: ApiError };

// ─────────────────────────────────────────────────────────────────────────────
// NOTIFICATION TYPES
// ─────────────────────────────────────────────────────────────────────────────

export type NotificationType =
  | 'approval_requested'
  | 'approval_completed'
  | 'document_generated'
  | 'document_updated'
  | 'agent_error'
  | 'agent_status'
  | 'system';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  timestamp: string;
  read: boolean;
  actionUrl?: string;
  documentId?: string;
  agentType?: AgentType;
  priority?: ApprovalPriority;
}

// ─────────────────────────────────────────────────────────────────────────────
// DASHBOARD KPI TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface DashboardKpis {
  totalDocuments: number;
  pendingApprovals: number;
  approvedToday: number;
  rejectedToday: number;
  avgProcessingTimeHours: number;
  completionRate: number;
  aiEnhancedCount: number;
  aiEnhancedPercentage: number;
  piiDocumentsCount: number;
  avgCompletenessScore: number;
  avgQualityScore: number;
  documentsByType: Record<DocumentType, number>;
  documentsByDomain: Record<string, number>;
  documentsByStatus: Record<DocumentStatus, number>;
  recentActivity: ActivityItem[];
}

export interface ActivityItem {
  id: string;
  type: 'approval' | 'document' | 'agent' | 'system';
  action: string;
  subject: string;
  actor: string;
  timestamp: string;
  documentId?: string;
}

export interface KpiCardData {
  id: string;
  label: string;
  value: number | string;
  change?: number;
  changeLabel?: string;
  changeType?: 'increase' | 'decrease' | 'neutral';
  icon: 'documents' | 'pending' | 'approved' | 'rejected' | 'time' | 'rate' | 'ai' | 'pii' | 'quality';
  variant?: 'default' | 'brand' | 'success' | 'warning' | 'danger';
}

// ─────────────────────────────────────────────────────────────────────────────
// SETTINGS TYPES
// ─────────────────────────────────────────────────────────────────────────────

export interface UserSettings {
  theme: 'light' | 'dark' | 'system';
  notifications: {
    approvalRequests: boolean;
    documentUpdates: boolean;
    agentAlerts: boolean;
    emailDigest: boolean;
  };
  display: {
    compactMode: boolean;
    showMetadata: boolean;
    defaultView: 'grid' | 'list';
  };
}

export interface SystemSettings {
  aiEnhancementEnabled: boolean;
  autoApprovalEnabled: boolean;
  defaultApprovalTiers: number;
  retentionDays: number;
  maxFileSize: number;
}
