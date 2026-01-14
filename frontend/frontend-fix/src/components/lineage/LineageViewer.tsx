// ═══════════════════════════════════════════════════════════════════════════
// Lineage Visualization Component
// Uses React Flow to display data lineage graphs
// ═══════════════════════════════════════════════════════════════════════════

import { useCallback, useMemo, useState } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  Panel,
  useNodesState,
  useEdgesState,
  type Node,
  type Edge,
  type NodeTypes,
  BackgroundVariant,
  MarkerType,
  Handle,
  Position,
} from '@xyflow/react';
import {
  Database,
  Table2,
  Columns3,
  Code2,
  ExternalLink,
  AlertTriangle,
  ZoomIn,
  ZoomOut,
  Maximize,
  RefreshCw,
  Info,
} from 'lucide-react';
import '@xyflow/react/dist/style.css';

import { useTableLineage, useDocumentLineage } from '@/hooks';
import { lineageService } from '@/services';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { cn } from '@/lib/utils';
import type { LineageGraph, LineageNode as LineageNodeType } from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Custom Node Component
// ─────────────────────────────────────────────────────────────────────────────

interface LineageNodeData extends LineageNodeType {
  isRoot?: boolean;
  isHighlighted?: boolean;
}

const nodeIcons: Record<string, React.ReactNode> = {
  table: <Table2 className="h-4 w-4" />,
  column: <Columns3 className="h-4 w-4" />,
  procedure: <Code2 className="h-4 w-4" />,
  view: <Database className="h-4 w-4" />,
  external: <ExternalLink className="h-4 w-4" />,
};

function LineageNodeComponent({ data }: { data: LineageNodeData }) {
  const hasPii = data.metadata?.piiIndicator;
  const isRoot = data.isRoot;
  const isHighlighted = data.isHighlighted;

  return (
    <div
      className={cn(
        'relative rounded-lg border-2 bg-white px-4 py-3 shadow-md transition-all',
        'dark:bg-stone-800',
        isRoot
          ? 'border-teal-500 ring-2 ring-teal-500/20'
          : isHighlighted
          ? 'border-amber-500'
          : 'border-stone-200 dark:border-stone-600',
        'min-w-[180px] max-w-[250px]'
      )}
    >
      <Handle
        type="target"
        position={Position.Left}
        className="!bg-teal-500 !w-3 !h-3 !border-2 !border-white"
      />

      <div className="flex items-center gap-2 mb-1">
        <span
          className={cn(
            'flex h-6 w-6 items-center justify-center rounded',
            data.type === 'table' && 'bg-blue-100 text-blue-600',
            data.type === 'column' && 'bg-purple-100 text-purple-600',
            data.type === 'procedure' && 'bg-emerald-100 text-emerald-600',
            data.type === 'view' && 'bg-amber-100 text-amber-600',
            data.type === 'external' && 'bg-stone-100 text-stone-600'
          )}
        >
          {nodeIcons[data.type]}
        </span>
        <span className="text-xs font-medium uppercase text-stone-400">
          {data.type}
        </span>
        {hasPii && (
          <AlertTriangle className="h-3 w-3 text-amber-500" title="Contains PII" />
        )}
      </div>

      <div className="font-medium text-stone-900 dark:text-stone-100 truncate" title={data.name}>
        {data.name}
      </div>

      {data.schema && (
        <div className="text-xs text-stone-500 dark:text-stone-400 truncate">
          {data.schema}
          {data.database && ` · ${data.database}`}
        </div>
      )}

      {data.metadata && (
        <div className="flex flex-wrap gap-1 mt-2">
          {data.metadata.businessDomain && (
            <Badge variant="brand" size="sm">
              {data.metadata.businessDomain}
            </Badge>
          )}
          {data.metadata.dataType && (
            <Badge variant="default" size="sm">
              {data.metadata.dataType}
            </Badge>
          )}
        </div>
      )}

      <Handle
        type="source"
        position={Position.Right}
        className="!bg-teal-500 !w-3 !h-3 !border-2 !border-white"
      />
    </div>
  );
}

const nodeTypes: NodeTypes = {
  lineageNode: LineageNodeComponent,
};

// ─────────────────────────────────────────────────────────────────────────────
// Lineage Canvas Component
// ─────────────────────────────────────────────────────────────────────────────

interface LineageCanvasProps {
  graph: LineageGraph;
  onNodeClick?: (node: LineageNodeType) => void;
  height?: string | number;
}

export function LineageCanvas({ graph, onNodeClick, height = 500 }: LineageCanvasProps) {
  const [selectedNode, setSelectedNode] = useState<string | null>(null);

  const { nodes: flowNodes, edges: flowEdges } = useMemo(() => {
    const result = lineageService.toReactFlowFormat(graph);

    const nodes = result.nodes.map((node) => ({
      ...node,
      type: 'lineageNode',
      data: {
        ...node.data,
        isRoot: node.id === graph.rootNodeId,
        isHighlighted: node.id === selectedNode,
      },
    }));

    const edges = result.edges.map((edge) => ({
      ...edge,
      style: { stroke: '#14b8a6', strokeWidth: 2 },
      markerEnd: { type: MarkerType.ArrowClosed, color: '#14b8a6' },
    }));

    return { nodes, edges };
  }, [graph, selectedNode]);

  const [nodes, setNodes, onNodesChange] = useNodesState(flowNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(flowEdges);

  const handleNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      setSelectedNode(node.id);
      const originalNode = graph.nodes.find((n) => n.id === node.id);
      if (originalNode) onNodeClick?.(originalNode);
    },
    [graph.nodes, onNodeClick]
  );

  return (
    <div style={{ height }} className="w-full rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-800 overflow-hidden">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={handleNodeClick}
        nodeTypes={nodeTypes}
        fitView
        fitViewOptions={{ padding: 0.2, maxZoom: 1.5 }}
        minZoom={0.1}
        maxZoom={2}
        proOptions={{ hideAttribution: true }}
      >
        <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="#e7e5e4" />
        <Controls showZoom={false} showFitView={false} showInteractive={false} />
        <MiniMap maskColor="rgba(250, 250, 249, 0.8)" pannable zoomable />

        <Panel position="top-right" className="!m-4">
          <div className="flex items-center gap-1 p-1 bg-white dark:bg-stone-800 rounded-xl border border-stone-200 dark:border-stone-700 shadow-sm">
            <Button variant="ghost" size="icon-sm"><ZoomIn className="h-4 w-4" /></Button>
            <Button variant="ghost" size="icon-sm"><ZoomOut className="h-4 w-4" /></Button>
            <Button variant="ghost" size="icon-sm"><Maximize className="h-4 w-4" /></Button>
          </div>
        </Panel>
      </ReactFlow>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Table Lineage View
// ─────────────────────────────────────────────────────────────────────────────

interface TableLineageViewProps {
  schema: string;
  table: string;
  onNodeClick?: (node: LineageNodeType) => void;
}

export function TableLineageView({ schema, table, onNodeClick }: TableLineageViewProps) {
  const [direction, setDirection] = useState<'upstream' | 'downstream' | 'both'>('both');
  const [depth, setDepth] = useState(3);

  const { data: graph, isLoading, error, refetch } = useTableLineage(schema, table, {
    direction,
    depth,
    includeColumns: true,
  });

  if (isLoading) {
    return (
      <Card variant="elevated">
        <CardContent className="flex items-center justify-center py-16">
          <RefreshCw className="h-8 w-8 animate-spin text-stone-400" />
        </CardContent>
      </Card>
    );
  }

  if (error || !graph || graph.nodes.length === 0) {
    return (
      <Card variant="elevated">
        <CardContent className="flex flex-col items-center justify-center py-16">
          <Database className="h-8 w-8 text-stone-400" />
          <p className="mt-2 text-sm text-stone-600 dark:text-stone-400">
            No lineage data available
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()} className="mt-4">
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card variant="elevated">
      <CardHeader className="border-b border-stone-200 dark:border-stone-700">
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <Database className="h-5 w-5 text-teal-500" />
            Lineage: {schema}.{table}
          </CardTitle>
          <div className="flex items-center gap-2">
            <select
              value={direction}
              onChange={(e) => setDirection(e.target.value as any)}
              className="text-sm border border-stone-200 rounded-lg px-2 py-1 dark:bg-stone-700"
            >
              <option value="both">Both</option>
              <option value="upstream">Upstream</option>
              <option value="downstream">Downstream</option>
            </select>
            <Button variant="ghost" size="icon-sm" onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent padding="none">
        <LineageCanvas graph={graph} onNodeClick={onNodeClick} height={500} />
      </CardContent>
    </Card>
  );
}

export function DocumentLineageView({ docId, onNodeClick }: { docId: string; onNodeClick?: (node: LineageNodeType) => void }) {
  const { data: graph, isLoading } = useDocumentLineage(docId);

  if (isLoading) {
    return <div className="flex items-center justify-center py-16"><RefreshCw className="h-8 w-8 animate-spin text-stone-400" /></div>;
  }

  if (!graph) {
    return <div className="flex flex-col items-center py-16"><Info className="h-8 w-8 text-stone-400" /><p className="mt-2 text-sm text-stone-500">No lineage data</p></div>;
  }

  return <LineageCanvas graph={graph} onNodeClick={onNodeClick} height={400} />;
}

export default LineageCanvas;
