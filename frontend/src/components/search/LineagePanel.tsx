import React, { useState } from 'react';
import { useDependents, useDependencies, usePiiFlow } from '../../hooks/useSearch';
import type { GraphSearchResult, PiiFlowPath } from '../../types/search';

interface LineagePanelProps {
  nodeId: string;
  onNodeClick: (nodeId: string) => void;
}

export const LineagePanel: React.FC<LineagePanelProps> = ({
  nodeId,
  onNodeClick,
}) => {
  const [activeTab, setActiveTab] = useState<'upstream' | 'downstream' | 'pii'>(
    'downstream'
  );
  const [maxDepth, setMaxDepth] = useState(3);

  const { data: dependents, isLoading: loadingDependents } = useDependents(
    nodeId,
    maxDepth
  );
  const { data: dependencies, isLoading: loadingDependencies } = useDependencies(
    nodeId,
    maxDepth
  );
  const { data: piiFlows, isLoading: loadingPii } = usePiiFlow(nodeId);

  const isLoading = loadingDependents || loadingDependencies || loadingPii;

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200">
      <div className="p-4 border-b border-gray-200">
        <h3 className="font-semibold text-gray-900">Lineage Explorer</h3>
        <p className="text-sm text-gray-500 mt-1 truncate" title={nodeId}>
          {nodeId}
        </p>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200">
        <TabButton
          active={activeTab === 'upstream'}
          onClick={() => setActiveTab('upstream')}
          label="Upstream"
          count={dependencies?.length}
        />
        <TabButton
          active={activeTab === 'downstream'}
          onClick={() => setActiveTab('downstream')}
          label="Downstream"
          count={dependents?.length}
        />
        <TabButton
          active={activeTab === 'pii'}
          onClick={() => setActiveTab('pii')}
          label="PII Flows"
          count={piiFlows?.length}
        />
      </div>

      {/* Depth Selector */}
      <div className="px-4 py-2 bg-gray-50 border-b border-gray-200">
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <span>Depth:</span>
          <select
            value={maxDepth}
            onChange={(e) => setMaxDepth(Number(e.target.value))}
            className="text-sm border-gray-300 rounded focus:ring-teal-500 focus:border-teal-500"
          >
            <option value={1}>1 level</option>
            <option value={2}>2 levels</option>
            <option value={3}>3 levels</option>
            <option value={5}>5 levels</option>
          </select>
        </label>
      </div>

      {/* Content */}
      <div className="p-4 max-h-96 overflow-y-auto">
        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <svg
              className="animate-spin h-6 w-6 text-teal-600"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
                fill="none"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
              />
            </svg>
          </div>
        ) : activeTab === 'upstream' ? (
          <LineageList
            items={dependencies || []}
            onNodeClick={onNodeClick}
            emptyMessage="No upstream dependencies found"
          />
        ) : activeTab === 'downstream' ? (
          <LineageList
            items={dependents || []}
            onNodeClick={onNodeClick}
            emptyMessage="No downstream dependents found"
          />
        ) : (
          <PiiFlowList
            items={piiFlows || []}
            onNodeClick={onNodeClick}
            emptyMessage="No PII flows detected"
          />
        )}
      </div>
    </div>
  );
};

// Tab button component
interface TabButtonProps {
  active: boolean;
  onClick: () => void;
  label: string;
  count?: number;
}

const TabButton: React.FC<TabButtonProps> = ({
  active,
  onClick,
  label,
  count,
}) => (
  <button
    onClick={onClick}
    className={`flex-1 px-3 py-2 text-sm font-medium text-center transition-colors ${
      active
        ? 'text-teal-600 border-b-2 border-teal-600'
        : 'text-gray-500 hover:text-gray-700'
    }`}
  >
    {label}
    {count !== undefined && count > 0 && (
      <span className="ml-1 text-xs text-gray-400">({count})</span>
    )}
  </button>
);

// Lineage list component
interface LineageListProps {
  items: GraphSearchResult[];
  onNodeClick: (nodeId: string) => void;
  emptyMessage: string;
}

const LineageList: React.FC<LineageListProps> = ({
  items,
  onNodeClick,
  emptyMessage,
}) => {
  if (items.length === 0) {
    return <p className="text-sm text-gray-500 text-center py-4">{emptyMessage}</p>;
  }

  // Group by depth
  const byDepth = items.reduce((acc, item) => {
    const depth = item.depth;
    if (!acc[depth]) acc[depth] = [];
    acc[depth].push(item);
    return acc;
  }, {} as Record<number, GraphSearchResult[]>);

  return (
    <div className="space-y-4">
      {Object.entries(byDepth).map(([depth, depthItems]) => (
        <div key={depth}>
          <h4 className="text-xs font-medium text-gray-500 uppercase mb-2">
            Level {depth}
          </h4>
          <div className="space-y-1">
            {depthItems.map((item) => (
              <button
                key={item.nodeId}
                onClick={() => onNodeClick(item.nodeId)}
                className="w-full text-left px-3 py-2 rounded hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center gap-2">
                  <NodeTypeIcon type={item.nodeType} />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">
                      {item.objectName}
                    </p>
                    <p className="text-xs text-gray-500 truncate">
                      {[item.databaseName, item.schemaName]
                        .filter(Boolean)
                        .join('.')}
                    </p>
                  </div>
                  {item.relationshipType && (
                    <span className="text-xs text-gray-400">
                      {item.relationshipType}
                    </span>
                  )}
                </div>
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
};

// PII flow list component
interface PiiFlowListProps {
  items: PiiFlowPath[];
  onNodeClick: (nodeId: string) => void;
  emptyMessage: string;
}

const PiiFlowList: React.FC<PiiFlowListProps> = ({
  items,
  onNodeClick,
  emptyMessage,
}) => {
  if (items.length === 0) {
    return <p className="text-sm text-gray-500 text-center py-4">{emptyMessage}</p>;
  }

  return (
    <div className="space-y-3">
      {items.map((flow, idx) => (
        <div
          key={idx}
          className="p-3 bg-red-50 border border-red-200 rounded-lg"
        >
          <div className="flex items-center gap-2 mb-2">
            <span className="px-2 py-0.5 text-xs font-medium bg-red-100 text-red-700 rounded">
              {flow.piiType}
            </span>
            <span className="text-xs text-gray-500">
              {flow.pathNodes.length} steps
            </span>
          </div>
          <div className="flex items-center gap-1 text-xs">
            {flow.pathNodes.map((nodeId, nodeIdx) => (
              <React.Fragment key={nodeId}>
                <button
                  onClick={() => onNodeClick(nodeId)}
                  className="text-red-700 hover:text-red-900 hover:underline truncate max-w-20"
                  title={nodeId}
                >
                  {nodeId.split('.').pop()}
                </button>
                {nodeIdx < flow.pathNodes.length - 1 && (
                  <span className="text-gray-400">→</span>
                )}
              </React.Fragment>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
};

// Node type icon
const NodeTypeIcon: React.FC<{ type: string }> = ({ type }) => {
  const baseClass = 'w-6 h-6 rounded flex items-center justify-center text-white text-xs font-bold';

  switch (type?.toLowerCase()) {
    case 'table':
      return <div className={`${baseClass} bg-blue-500`}>T</div>;
    case 'column':
      return <div className={`${baseClass} bg-green-500`}>C</div>;
    case 'storedprocedure':
    case 'procedure':
      return <div className={`${baseClass} bg-purple-500`}>P</div>;
    case 'view':
      return <div className={`${baseClass} bg-orange-500`}>V</div>;
    default:
      return <div className={`${baseClass} bg-gray-500`}>?</div>;
  }
};

export default LineagePanel;
