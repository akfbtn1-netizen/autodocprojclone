// Search types matching backend DTOs

export type RoutingPath = 'Keyword' | 'Semantic' | 'Relationship' | 'Metadata' | 'Agentic';

export interface SearchRequest {
  query: string;
  maxResults?: number;
  includeLineage?: boolean;
  includePiiFlows?: boolean;
  enableReranking?: boolean;
  minConfidence?: number;
  filterDatabases?: string[];
  filterObjectTypes?: string[];
  filterCategories?: string[];
}

export interface SearchResponse {
  queryId: string;
  originalQuery: string;
  expandedQuery: string | null;
  routingPath: RoutingPath;
  results: SearchResultItem[];
  followUpSuggestions: FollowUpSuggestion[];
  metadata: SearchMetadata;
}

export interface SearchResultItem {
  documentId: string;
  objectType: string | null;
  objectName: string | null;
  schemaName: string | null;
  databaseName: string | null;
  description: string | null;
  businessPurpose: string | null;
  category: string | null;
  dataClassification: string | null;
  score: RelevanceScore;
  matchedTerms: string[] | null;
  lineage: LineageInfo | null;
  piiInfo: PiiInfo | null;
}

export interface RelevanceScore {
  fusedScore: number;
  semanticScore: number;
  colBertScore: number;
}

export interface LineageInfo {
  upstreamCount: number;
  downstreamCount: number;
  immediateUpstream: string[] | null;
  immediateDownstream: string[] | null;
}

export interface PiiInfo {
  isPii: boolean;
  piiType: string | null;
  flowPathCount: number;
}

export interface SearchMetadata {
  totalCandidates: number;
  filteredResults: number;
  processingTime: string;
  stageTimings: Record<string, string>;
  cacheHit: boolean;
  classificationReason: string | null;
}

export interface FollowUpSuggestion {
  suggestionText: string;
  suggestionType: string;
  confidence: number;
  rationale: string | null;
}

export interface GraphSearchResult {
  nodeId: string;
  nodeType: string;
  objectName: string;
  schemaName: string | null;
  databaseName: string | null;
  depth: number;
  relationshipType: string | null;
  parentNodeId: string | null;
  properties: Record<string, unknown> | null;
}

export interface PiiFlowPath {
  sourceNodeId: string;
  piiType: string;
  destinationNodeId: string;
  pathNodes: string[];
}

export interface GraphStats {
  nodeCount: number;
  edgeCount: number;
  piiFlowCount: number;
  lastRebuilt: string;
}

export interface LearningAnalytics {
  totalQueries: number;
  totalInteractions: number;
  averageClickRank: number;
  clickThroughRate: number;
  queryTypeDistribution: Record<string, number>;
  topSearchTerms: Record<string, number>;
  lastUpdateTime: string;
}

export interface CategorySuggestion {
  documentId: string;
  currentCategory: string;
  suggestedCategory: string;
  confidence: number;
  reasoning: string;
}

export interface UserInteraction {
  queryId: string;
  interactionType: 'click' | 'expand' | 'export' | 'feedback';
  documentId?: string;
  data?: Record<string, unknown>;
}

export type ExportFormat = 'Csv' | 'Excel' | 'Pdf';

export interface ExportRequest {
  format: ExportFormat;
  results?: SearchResultItem[];
  includeLineageGraph?: boolean;
  includeMetadata?: boolean;
  includeChangeHistory?: boolean;
  reportTitle?: string;
  reportDescription?: string;
}

// Filter state
export interface SearchFilters {
  databases: string[];
  objectTypes: string[];
  categories: string[];
  showPiiOnly: boolean;
}

// Search state
export interface SearchState {
  query: string;
  results: SearchResultItem[];
  currentQueryId: string | null;
  routingPath: RoutingPath | null;
  isLoading: boolean;
  error: string | null;
  filters: SearchFilters;
  suggestions: string[];
  followUpSuggestions: FollowUpSuggestion[];
  metadata: SearchMetadata | null;
}
