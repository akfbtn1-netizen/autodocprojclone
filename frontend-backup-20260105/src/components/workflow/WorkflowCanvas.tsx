import { useCallback, useMemo } from 'react';
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
} from '@xyflow/react';
import { motion } from 'framer-motion';
import { RefreshCw, ZoomIn, ZoomOut, Maximize } from 'lucide-react';
import '@xyflow/react/dist/style.css';

import { WorkflowNode, type WorkflowNodeData, edgeOptions, activeEdgeOptions } from './WorkflowNode';
import { Button, Badge } from '@/components/ui';
import { cn } from '@/lib/utils';

interface WorkflowCanvasProps {
  onNodeClick?: (nodeId: string) => void;
  isConnected?: boolean;
}

// Node types registration
const nodeTypes: NodeTypes = {
  workflow: WorkflowNode,
};

// Initial workflow configuration
const initialNodes: Node<WorkflowNodeData>[] = [
  {
    id: 'excel-change',
    type: 'workflow',
    position: { x: 0, y: 100 },
    data: {
      label: 'Excel Change',
      description: 'Detected',
      status: 'completed',
      icon: 'draft',
      documentCount: 3,
      avgTime: '~instant',
    },
  },
  {
    id: 'draft-created',
    type: 'workflow',
    position: { x: 280, y: 100 },
    data: {
      label: 'Draft Created',
      description: 'AI Enhanced',
      status: 'completed',
      icon: 'pending',
      documentCount: 3,
      avgTime: '~2m',
    },
  },
  {
    id: 'approval-request',
    type: 'workflow',
    position: { x: 560, y: 100 },
    data: {
      label: 'Approval',
      description: 'Requested',
      status: 'active',
      icon: 'review',
      documentCount: 2,
      avgTime: '~4h avg',
    },
  },
  {
    id: 'approved',
    type: 'workflow',
    position: { x: 840, y: 100 },
    data: {
      label: 'Approved',
      description: 'Ready',
      status: 'waiting',
      icon: 'approved',
      documentCount: 0,
    },
  },
  {
    id: 'generated',
    type: 'workflow',
    position: { x: 1120, y: 100 },
    data: {
      label: 'Generated',
      description: 'Complete',
      status: 'waiting',
      icon: 'completed',
      documentCount: 0,
    },
  },
];

const initialEdges: Edge[] = [
  {
    id: 'e-excel-draft',
    source: 'excel-change',
    target: 'draft-created',
    ...edgeOptions,
  },
  {
    id: 'e-draft-approval',
    source: 'draft-created',
    target: 'approval-request',
    ...activeEdgeOptions,
  },
  {
    id: 'e-approval-approved',
    source: 'approval-request',
    target: 'approved',
    ...edgeOptions,
  },
  {
    id: 'e-approved-generated',
    source: 'approved',
    target: 'generated',
    ...edgeOptions,
  },
];

export function WorkflowCanvas({ onNodeClick, isConnected = false }: WorkflowCanvasProps) {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  // Handle node click
  const handleNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      onNodeClick?.(node.id);
    },
    [onNodeClick]
  );

  // Minimap node color based on status
  const minimapNodeColor = useCallback((node: Node<WorkflowNodeData>) => {
    const colors = {
      waiting: '#d6d3d1',
      active: '#14b8a6',
      completed: '#22c55e',
      error: '#ef4444',
    };
    return colors[node.data.status] ?? colors.waiting;
  }, []);

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      className="h-[400px] w-full rounded-2xl border border-surface-200 bg-white overflow-hidden shadow-card"
    >
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onNodeClick={handleNodeClick}
        nodeTypes={nodeTypes}
        fitView
        fitViewOptions={{
          padding: 0.3,
          maxZoom: 1.2,
        }}
        minZoom={0.5}
        maxZoom={2}
        attributionPosition="bottom-left"
        proOptions={{ hideAttribution: true }}
      >
        <Background
          variant={BackgroundVariant.Dots}
          gap={20}
          size={1}
          color="#e7e5e4"
        />

        <Controls
          showZoom={false}
          showFitView={false}
          showInteractive={false}
        />

        <MiniMap
          nodeColor={minimapNodeColor}
          maskColor="rgba(250, 250, 249, 0.8)"
          pannable
          zoomable
        />

        {/* Custom top panel */}
        <Panel position="top-left" className="!m-4">
          <div className="flex items-center gap-3">
            <h3 className="font-display text-lg font-semibold text-surface-800">
              Approval Pipeline
            </h3>
            <Badge
              variant={isConnected ? 'success' : 'warning'}
              size="sm"
              dot
              pulse={isConnected}
            >
              {isConnected ? 'Live' : 'Offline'}
            </Badge>
          </div>
        </Panel>

        {/* Custom controls panel */}
        <Panel position="top-right" className="!m-4">
          <div className="flex items-center gap-1 p-1 bg-white rounded-xl border border-surface-200 shadow-card">
            <Button variant="ghost" size="icon-sm" aria-label="Zoom in">
              <ZoomIn className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" aria-label="Zoom out">
              <ZoomOut className="h-4 w-4" />
            </Button>
            <Button variant="ghost" size="icon-sm" aria-label="Fit view">
              <Maximize className="h-4 w-4" />
            </Button>
            <div className="w-px h-5 bg-surface-200 mx-1" />
            <Button variant="ghost" size="icon-sm" aria-label="Refresh">
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </Panel>
      </ReactFlow>
    </motion.div>
  );
}

export default WorkflowCanvas;
