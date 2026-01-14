// ═══════════════════════════════════════════════════════════════════════════
// Lineage Service
// Data lineage tracking and visualization
// Endpoints: /api/lineage/*
// ═══════════════════════════════════════════════════════════════════════════

import { apiClient } from './api';
import type { LineageGraph, LineageNode, LineageEdge, Result, BusinessDomain } from '@/types';

export interface ColumnLineage {
  sourceColumn: {
    schema: string;
    table: string;
    column: string;
    dataType?: string;
  };
  targetColumn: {
    schema: string;
    table: string;
    column: string;
    dataType?: string;
  };
  transformation?: string;
  procedure?: string;
  confidence: number;
}

export interface TableLineage {
  table: {
    schema: string;
    name: string;
  };
  upstreamTables: {
    schema: string;
    name: string;
    joinType?: string;
    relationship?: string;
  }[];
  downstreamTables: {
    schema: string;
    name: string;
    usedIn?: string; // procedure name
  }[];
  procedures: string[];
}

export interface ImpactAnalysis {
  affectedObjects: {
    type: 'table' | 'column' | 'procedure' | 'view';
    schema: string;
    name: string;
    impactLevel: 'direct' | 'indirect';
    distance: number;
  }[];
  riskLevel: 'low' | 'medium' | 'high' | 'critical';
  affectedDocuments: string[];
  affectedBusinessDomains: BusinessDomain[];
  recommendations: string[];
}

export interface LineageSearchResult {
  nodes: LineageNode[];
  totalCount: number;
  suggestions: string[];
}

export const lineageService = {
  // ─────────────────────────────────────────────────────────────────────────
  // Graph Retrieval
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get lineage graph for a table
   */
  getTableLineage: async (
    schema: string,
    table: string,
    options?: {
      depth?: number;
      direction?: 'upstream' | 'downstream' | 'both';
      includeColumns?: boolean;
    }
  ): Promise<Result<LineageGraph>> => {
    const params = new URLSearchParams();
    if (options?.depth) params.set('depth', options.depth.toString());
    if (options?.direction) params.set('direction', options.direction);
    if (options?.includeColumns !== undefined) {
      params.set('includeColumns', options.includeColumns.toString());
    }

    return apiClient.get<LineageGraph>(
      `/lineage/table/${schema}/${table}?${params.toString()}`
    );
  },

  /**
   * Get lineage graph for a column
   */
  getColumnLineage: async (
    schema: string,
    table: string,
    column: string,
    options?: {
      depth?: number;
      direction?: 'upstream' | 'downstream' | 'both';
    }
  ): Promise<Result<LineageGraph>> => {
    const params = new URLSearchParams();
    if (options?.depth) params.set('depth', options.depth.toString());
    if (options?.direction) params.set('direction', options.direction);

    return apiClient.get<LineageGraph>(
      `/lineage/column/${schema}/${table}/${column}?${params.toString()}`
    );
  },

  /**
   * Get lineage for a stored procedure
   */
  getProcedureLineage: async (
    schema: string,
    procedure: string
  ): Promise<Result<LineageGraph>> => {
    return apiClient.get<LineageGraph>(`/lineage/procedure/${schema}/${procedure}`);
  },

  /**
   * Get full lineage for a document
   */
  getDocumentLineage: async (docId: string): Promise<Result<LineageGraph>> => {
    return apiClient.get<LineageGraph>(`/lineage/document/${encodeURIComponent(docId)}`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Column-Level Lineage
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get detailed column lineage mappings
   */
  getColumnMappings: async (
    schema: string,
    table: string
  ): Promise<Result<ColumnLineage[]>> => {
    return apiClient.get<ColumnLineage[]>(`/lineage/mappings/${schema}/${table}`);
  },

  /**
   * Get columns that feed into a specific column
   */
  getColumnSources: async (
    schema: string,
    table: string,
    column: string
  ): Promise<Result<ColumnLineage[]>> => {
    return apiClient.get<ColumnLineage[]>(
      `/lineage/column/${schema}/${table}/${column}/sources`
    );
  },

  /**
   * Get columns that a specific column feeds into
   */
  getColumnTargets: async (
    schema: string,
    table: string,
    column: string
  ): Promise<Result<ColumnLineage[]>> => {
    return apiClient.get<ColumnLineage[]>(
      `/lineage/column/${schema}/${table}/${column}/targets`
    );
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Table-Level Lineage
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Get comprehensive table lineage info
   */
  getTableInfo: async (schema: string, table: string): Promise<Result<TableLineage>> => {
    return apiClient.get<TableLineage>(`/lineage/table-info/${schema}/${table}`);
  },

  /**
   * Get upstream tables (data sources)
   */
  getUpstreamTables: async (
    schema: string,
    table: string,
    depth = 3
  ): Promise<Result<LineageNode[]>> => {
    return apiClient.get<LineageNode[]>(
      `/lineage/table/${schema}/${table}/upstream?depth=${depth}`
    );
  },

  /**
   * Get downstream tables (data consumers)
   */
  getDownstreamTables: async (
    schema: string,
    table: string,
    depth = 3
  ): Promise<Result<LineageNode[]>> => {
    return apiClient.get<LineageNode[]>(
      `/lineage/table/${schema}/${table}/downstream?depth=${depth}`
    );
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Impact Analysis
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Analyze impact of changes to a table
   */
  analyzeTableImpact: async (
    schema: string,
    table: string
  ): Promise<Result<ImpactAnalysis>> => {
    return apiClient.get<ImpactAnalysis>(`/lineage/impact/table/${schema}/${table}`);
  },

  /**
   * Analyze impact of changes to a column
   */
  analyzeColumnImpact: async (
    schema: string,
    table: string,
    column: string
  ): Promise<Result<ImpactAnalysis>> => {
    return apiClient.get<ImpactAnalysis>(
      `/lineage/impact/column/${schema}/${table}/${column}`
    );
  },

  /**
   * Get objects that would be affected by a proposed change
   */
  previewChangeImpact: async (change: {
    type: 'modify' | 'delete' | 'rename';
    objectType: 'table' | 'column';
    schema: string;
    table: string;
    column?: string;
    newName?: string;
  }): Promise<Result<ImpactAnalysis>> => {
    return apiClient.post<ImpactAnalysis>('/lineage/impact/preview', change);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Search & Discovery
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Search lineage nodes
   */
  search: async (
    query: string,
    options?: {
      type?: 'table' | 'column' | 'procedure';
      schema?: string;
      limit?: number;
    }
  ): Promise<Result<LineageSearchResult>> => {
    const params = new URLSearchParams({ q: query });
    if (options?.type) params.set('type', options.type);
    if (options?.schema) params.set('schema', options.schema);
    if (options?.limit) params.set('limit', options.limit.toString());

    return apiClient.get<LineageSearchResult>(`/lineage/search?${params.toString()}`);
  },

  /**
   * Get all schemas with lineage data
   */
  getSchemas: async (): Promise<Result<string[]>> => {
    return apiClient.get<string[]>('/lineage/schemas');
  },

  /**
   * Get all tables in a schema
   */
  getTables: async (schema: string): Promise<Result<string[]>> => {
    return apiClient.get<string[]>(`/lineage/schemas/${schema}/tables`);
  },

  /**
   * Get all columns in a table
   */
  getColumns: async (schema: string, table: string): Promise<Result<{
    name: string;
    dataType: string;
    isNullable: boolean;
    hasLineage: boolean;
  }[]>> => {
    return apiClient.get(`/lineage/schemas/${schema}/tables/${table}/columns`);
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Lineage Extraction (Admin)
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Trigger lineage extraction for a procedure
   */
  extractProcedureLineage: async (
    schema: string,
    procedure: string
  ): Promise<Result<{ extracted: number; updated: number }>> => {
    return apiClient.post(`/lineage/extract/procedure/${schema}/${procedure}`);
  },

  /**
   * Trigger full lineage refresh
   */
  refreshAllLineage: async (): Promise<Result<{
    proceduresProcessed: number;
    edgesCreated: number;
    duration: string;
  }>> => {
    return apiClient.post('/lineage/refresh');
  },

  /**
   * Get lineage extraction status
   */
  getExtractionStatus: async (): Promise<Result<{
    lastRun: string;
    status: 'idle' | 'running' | 'completed' | 'failed';
    progress?: number;
    proceduresTotal?: number;
    proceduresProcessed?: number;
  }>> => {
    return apiClient.get('/lineage/extraction-status');
  },

  // ─────────────────────────────────────────────────────────────────────────
  // Visualization Helpers
  // ─────────────────────────────────────────────────────────────────────────

  /**
   * Convert lineage graph to React Flow format
   */
  toReactFlowFormat: (graph: LineageGraph): {
    nodes: Array<{
      id: string;
      type: string;
      position: { x: number; y: number };
      data: LineageNode;
    }>;
    edges: Array<{
      id: string;
      source: string;
      target: string;
      animated?: boolean;
      label?: string;
      type?: string;
    }>;
  } => {
    // Auto-layout using a simple horizontal layout
    const levelMap = new Map<string, number>();
    const processed = new Set<string>();

    // BFS to assign levels
    const queue = graph.rootNodeId ? [graph.rootNodeId] : [];
    if (queue.length > 0) {
      levelMap.set(queue[0], 0);
    }

    while (queue.length > 0) {
      const nodeId = queue.shift()!;
      if (processed.has(nodeId)) continue;
      processed.add(nodeId);

      const currentLevel = levelMap.get(nodeId) ?? 0;

      // Find connected nodes
      graph.edges.forEach(edge => {
        if (edge.source === nodeId && !levelMap.has(edge.target)) {
          levelMap.set(edge.target, currentLevel + 1);
          queue.push(edge.target);
        }
        if (edge.target === nodeId && !levelMap.has(edge.source)) {
          levelMap.set(edge.source, currentLevel - 1);
          queue.push(edge.source);
        }
      });
    }

    // Assign positions based on levels
    const levelCounts = new Map<number, number>();
    const nodes = graph.nodes.map(node => {
      const level = levelMap.get(node.id) ?? 0;
      const count = levelCounts.get(level) ?? 0;
      levelCounts.set(level, count + 1);

      return {
        id: node.id,
        type: 'lineageNode',
        position: {
          x: level * 300,
          y: count * 120,
        },
        data: node,
      };
    });

    const edges = graph.edges.map(edge => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      animated: edge.type === 'transform',
      label: edge.transformation,
      type: 'smoothstep',
    }));

    return { nodes, edges };
  },
};

export default lineageService;
