// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Dashboard Component
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Add React Flow visualization for impact graph
// TODO [4]: Add diff viewer for before/after comparison
// TODO [4]: Wire SignalR for real-time updates

import { useEffect, useState } from 'react';
import { useSchemaChangeStore, useHighRiskChanges, usePendingChanges } from '../../stores/schemaChangeStore';
import type { SchemaChangeDto, RiskLevel } from '../../types/schemaChange';

const RiskBadge = ({ level }: { level: RiskLevel }) => {
  const colors: Record<RiskLevel, string> = {
    LOW: 'bg-green-100 text-green-800',
    MEDIUM: 'bg-yellow-100 text-yellow-800',
    HIGH: 'bg-orange-100 text-orange-800',
    CRITICAL: 'bg-red-100 text-red-800',
  };

  return (
    <span className={`px-2 py-1 text-xs font-medium rounded-full ${colors[level]}`}>
      {level}
    </span>
  );
};

const StatCard = ({ label, value, color = 'stone' }: { label: string; value: number | string; color?: string }) => (
  <div className={`bg-${color}-50 p-4 rounded-lg border border-${color}-200`}>
    <div className="text-2xl font-bold text-stone-900">{value}</div>
    <div className="text-sm text-stone-600">{label}</div>
  </div>
);

const ChangeRow = ({ change, onSelect }: { change: SchemaChangeDto; onSelect: (id: string) => void }) => (
  <tr
    className="hover:bg-stone-50 cursor-pointer transition-colors"
    onClick={() => onSelect(change.changeId)}
  >
    <td className="px-4 py-3 text-sm font-medium text-stone-900">
      {change.schemaName}.{change.objectName}
    </td>
    <td className="px-4 py-3 text-sm text-stone-600">{change.objectType}</td>
    <td className="px-4 py-3 text-sm">
      <span className={`px-2 py-1 rounded text-xs ${
        change.changeType === 'CREATE' ? 'bg-green-100 text-green-700' :
        change.changeType === 'DROP' ? 'bg-red-100 text-red-700' :
        'bg-blue-100 text-blue-700'
      }`}>
        {change.changeType}
      </span>
    </td>
    <td className="px-4 py-3"><RiskBadge level={change.riskLevel} /></td>
    <td className="px-4 py-3 text-sm text-stone-600">
      {new Date(change.detectedAt).toLocaleString()}
    </td>
    <td className="px-4 py-3 text-sm">
      <span className={`px-2 py-1 rounded text-xs ${
        change.processingStatus === 'Pending' ? 'bg-yellow-100 text-yellow-700' :
        change.processingStatus === 'Acknowledged' ? 'bg-green-100 text-green-700' :
        'bg-stone-100 text-stone-700'
      }`}>
        {change.processingStatus}
      </span>
    </td>
    <td className="px-4 py-3 text-sm text-stone-600">
      {change.affectedProcedures + change.affectedViews + change.affectedFunctions}
    </td>
  </tr>
);

export function SchemaChangeDashboard() {
  const {
    changes,
    statistics,
    isLoading,
    error,
    fetchChanges,
    fetchStatistics,
    fetchChangeDetail,
    startDetection,
    acknowledgeChange,
  } = useSchemaChangeStore();

  const highRiskChanges = useHighRiskChanges();
  const pendingChanges = usePendingChanges();
  const [selectedId, setSelectedId] = useState<string | null>(null);

  useEffect(() => {
    fetchChanges();
    fetchStatistics();
  }, [fetchChanges, fetchStatistics]);

  const handleStartDetection = async () => {
    await startDetection('FULL');
  };

  const handleSelectChange = async (changeId: string) => {
    setSelectedId(changeId);
    await fetchChangeDetail(changeId);
  };

  if (error) {
    return (
      <div className="p-6 bg-red-50 border border-red-200 rounded-lg">
        <h3 className="text-red-800 font-medium">Error</h3>
        <p className="text-red-600 text-sm">{error}</p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-stone-900">Schema Change Detector</h1>
          <p className="text-stone-600">Agent #4 - Real-time database schema monitoring</p>
        </div>
        <button
          onClick={handleStartDetection}
          disabled={isLoading}
          className="px-4 py-2 bg-teal-600 text-white rounded-lg hover:bg-teal-700 disabled:opacity-50 transition-colors"
        >
          {isLoading ? 'Running...' : 'Run Detection'}
        </button>
      </div>

      {/* Stats Grid */}
      {statistics && (
        <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-4">
          <StatCard label="Total Changes" value={statistics.totalChanges} />
          <StatCard label="Pending" value={statistics.pendingChanges} color="yellow" />
          <StatCard label="High Risk" value={statistics.highRiskChanges} color="orange" />
          <StatCard label="Critical" value={statistics.criticalChanges} color="red" />
          <StatCard label="Today" value={statistics.changesToday} color="teal" />
          <StatCard label="Avg Impact" value={statistics.averageImpactScore} />
        </div>
      )}

      {/* High Risk Alert */}
      {highRiskChanges.length > 0 && (
        <div className="bg-orange-50 border-l-4 border-orange-500 p-4 rounded-r-lg">
          <div className="flex items-center">
            <span className="text-orange-600 font-medium">
              {highRiskChanges.length} high-risk change{highRiskChanges.length > 1 ? 's' : ''} requiring attention
            </span>
          </div>
        </div>
      )}

      {/* Changes Table */}
      <div className="bg-white rounded-lg border border-stone-200 overflow-hidden">
        <div className="px-4 py-3 border-b border-stone-200 bg-stone-50">
          <h2 className="font-medium text-stone-900">Recent Schema Changes</h2>
        </div>

        {isLoading ? (
          <div className="p-8 text-center text-stone-500">Loading changes...</div>
        ) : changes.length === 0 ? (
          <div className="p-8 text-center text-stone-500">No schema changes detected</div>
        ) : (
          <table className="w-full">
            <thead className="bg-stone-50 border-b border-stone-200">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Object</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Type</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Change</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Risk</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Detected</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Status</th>
                <th className="px-4 py-3 text-left text-xs font-medium text-stone-500 uppercase">Impacts</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-200">
              {changes.map((change) => (
                <ChangeRow
                  key={change.changeId}
                  change={change}
                  onSelect={handleSelectChange}
                />
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

export default SchemaChangeDashboard;
