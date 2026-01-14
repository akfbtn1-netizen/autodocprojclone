// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - TypeScript Types
// ═══════════════════════════════════════════════════════════════════════════

export type RiskLevel = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type ProcessingStatus = 'Pending' | 'Analyzing' | 'Assessed' | 'Acknowledged' | 'AutoProcessed' | 'Failed';
export type DetectionRunState = 'Pending' | 'Snapshotting' | 'Comparing' | 'Analyzing' | 'Notifying' | 'Complete' | 'Failed' | 'Cancelled';
export type ObjectType = 'TABLE' | 'VIEW' | 'PROCEDURE' | 'FUNCTION' | 'INDEX' | 'CONSTRAINT';
export type ChangeType = 'CREATE' | 'ALTER' | 'DROP' | 'RENAME';

export interface SchemaChangeDto {
  changeId: string;
  databaseName: string;
  schemaName: string;
  objectName: string;
  objectType: ObjectType;
  changeType: ChangeType;
  changeDescription?: string;
  detectedAt: string;
  detectedBy: string;
  loginName?: string;
  impactScore: number;
  riskLevel: RiskLevel;
  processingStatus: ProcessingStatus;
  affectedProcedures: number;
  affectedViews: number;
  affectedFunctions: number;
  hasPiiColumns: boolean;
  hasLineageDownstream: boolean;
  approvalRequired: boolean;
  documentationTriggered: boolean;
}

export interface SchemaChangeDetailDto extends SchemaChangeDto {
  oldDefinition?: string;
  newDefinition?: string;
  ddlStatement?: string;
  hostName?: string;
  applicationName?: string;
  acknowledgedBy?: string;
  acknowledgedAt?: string;
  acknowledgementNotes?: string;
  approvalWorkflowId?: string;
  documentationTriggeredAt?: string;
  impacts: ChangeImpactDto[];
  columnChanges: ColumnChangeDto[];
}

export interface ChangeImpactDto {
  impactId: string;
  affectedSchema: string;
  affectedObject: string;
  affectedObjectType: ObjectType;
  impactType: 'BREAKS' | 'INVALIDATES' | 'MODIFIES' | 'PERFORMANCE';
  impactSeverity: number;
  impactDescription?: string;
  operationType?: string;
  affectedColumn?: string;
  lineNumber?: number;
  sqlFragment?: string;
  suggestedAction?: string;
  requiresManualReview: boolean;
}

export interface ColumnChangeDto {
  columnChangeId: string;
  schemaName: string;
  tableName: string;
  columnName: string;
  changeType: 'ADD' | 'DROP' | 'MODIFY' | 'RENAME';
  oldDataType?: string;
  newDataType?: string;
  oldIsNullable?: boolean;
  newIsNullable?: boolean;
  oldIsPii?: boolean;
  newIsPii?: boolean;
  totalUsageCount: number;
}

export interface DetectionRunDto {
  runId: string;
  runType: string;
  scanScope: string;
  schemaFilter?: string;
  currentState: DetectionRunState;
  totalObjects: number;
  processedObjects: number;
  progressPercent: number;
  changesDetected: number;
  highRiskChanges: number;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  errorMessage?: string;
  triggeredBy: string;
}

export interface SchemaSnapshotDto {
  snapshotId: string;
  snapshotName: string;
  snapshotType: string;
  schemaFilter?: string;
  objectCount: number;
  tableCount: number;
  viewCount: number;
  procedureCount: number;
  functionCount: number;
  takenAt: string;
  takenBy: string;
  isBaseline: boolean;
}

export interface SchemaChangeStatsDto {
  totalChanges: number;
  pendingChanges: number;
  highRiskChanges: number;
  criticalChanges: number;
  changesToday: number;
  changesThisWeek: number;
  averageImpactScore: number;
  piiRelatedChanges: number;
  awaitingApproval: number;
  lastDetectionRun?: string;
  lastDetectionStatus?: string;
}

export interface SchemaChangeFilter {
  schemaName?: string;
  objectName?: string;
  objectType?: ObjectType;
  changeType?: ChangeType;
  riskLevel?: RiskLevel;
  processingStatus?: ProcessingStatus;
  fromDate?: string;
  toDate?: string;
  hasPiiColumns?: boolean;
  approvalRequired?: boolean;
  page?: number;
  pageSize?: number;
}

// SignalR notification types
export interface SchemaChangeDetectedNotification {
  changeId: string;
  schemaName: string;
  objectName: string;
  objectType: string;
  changeType: string;
  riskLevel: RiskLevel;
  detectedAt: string;
}

export interface DetectionProgressNotification {
  runId: string;
  currentState: DetectionRunState;
  processedObjects: number;
  totalObjects: number;
  progressPercent: number;
  changesDetected: number;
}
