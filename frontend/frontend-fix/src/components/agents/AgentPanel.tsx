// ═══════════════════════════════════════════════════════════════════════════
// Agent Monitoring Panel
// Displays status of all 4 agents: SchemaDetector, DocGenerator,
// ExcelChangeIntegrator, MetadataManager
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Activity,
  Database,
  FileText,
  Table2,
  Sparkles,
  Play,
  Pause,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  Clock,
  Loader2,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import { useAgents, useAgentHealth, useAgentActivity, useAgentCommand } from '@/hooks';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { cn, formatRelativeTime } from '@/lib/utils';
import type { Agent, AgentType, AgentStatus, AgentActivity as AgentActivityType } from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Agent Icons
// ─────────────────────────────────────────────────────────────────────────────

const agentIcons: Record<AgentType, React.ReactNode> = {
  SchemaDetector: <Database className="h-5 w-5" />,
  DocGenerator: <FileText className="h-5 w-5" />,
  ExcelChangeIntegrator: <Table2 className="h-5 w-5" />,
  MetadataManager: <Sparkles className="h-5 w-5" />,
};

const statusColors: Record<AgentStatus, string> = {
  idle: 'bg-stone-400',
  processing: 'bg-teal-500',
  error: 'bg-red-500',
  stopped: 'bg-stone-600',
  starting: 'bg-amber-500',
};

const statusBadgeVariants: Record<AgentStatus, 'default' | 'success' | 'warning' | 'danger'> = {
  idle: 'default',
  processing: 'success',
  error: 'danger',
  stopped: 'default',
  starting: 'warning',
};

// ─────────────────────────────────────────────────────────────────────────────
// Agent Card Component
// ─────────────────────────────────────────────────────────────────────────────

interface AgentCardProps {
  agent: Agent;
  onStart?: () => void;
  onStop?: () => void;
  onRestart?: () => void;
  isLoading?: boolean;
}

function AgentCard({ agent, onStart, onStop, onRestart, isLoading }: AgentCardProps) {
  const [expanded, setExpanded] = useState(false);
  const { data: activity } = useAgentActivity(agent.type, 5);

  const isProcessing = agent.status === 'processing';
  const hasError = agent.status === 'error';
  const isStopped = agent.status === 'stopped';

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className={cn(
        'rounded-xl border p-4 transition-all',
        hasError
          ? 'border-red-200 bg-red-50 dark:border-red-900 dark:bg-red-950/50'
          : 'border-stone-200 bg-white dark:border-stone-700 dark:bg-stone-800'
      )}
    >
      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <div
            className={cn(
              'flex h-10 w-10 items-center justify-center rounded-lg',
              hasError
                ? 'bg-red-100 text-red-600 dark:bg-red-900/50'
                : isProcessing
                ? 'bg-teal-100 text-teal-600 dark:bg-teal-900/50'
                : 'bg-stone-100 text-stone-600 dark:bg-stone-700'
            )}
          >
            {isProcessing ? (
              <Loader2 className="h-5 w-5 animate-spin" />
            ) : (
              agentIcons[agent.type]
            )}
          </div>
          <div>
            <h3 className="font-medium text-stone-900 dark:text-stone-100">
              {agent.name}
            </h3>
            <p className="text-xs text-stone-500 dark:text-stone-400">
              {agent.description}
            </p>
          </div>
        </div>

        <Badge variant={statusBadgeVariants[agent.status]} size="sm" dot pulse={isProcessing}>
          {agent.status}
        </Badge>
      </div>

      {/* Stats */}
      <div className="mt-4 grid grid-cols-3 gap-2 text-center">
        <div className="rounded-lg bg-stone-50 p-2 dark:bg-stone-700/50">
          <p className="text-lg font-semibold text-stone-900 dark:text-stone-100">
            {agent.processedToday}
          </p>
          <p className="text-xs text-stone-500 dark:text-stone-400">Today</p>
        </div>
        <div className="rounded-lg bg-stone-50 p-2 dark:bg-stone-700/50">
          <p className="text-lg font-semibold text-stone-900 dark:text-stone-100">
            {agent.queueDepth}
          </p>
          <p className="text-xs text-stone-500 dark:text-stone-400">Queue</p>
        </div>
        <div className="rounded-lg bg-stone-50 p-2 dark:bg-stone-700/50">
          <p className="text-lg font-semibold text-stone-900 dark:text-stone-100">
            {agent.errorCount}
          </p>
          <p className="text-xs text-stone-500 dark:text-stone-400">Errors</p>
        </div>
      </div>

      {/* Current Task */}
      {agent.currentTask && (
        <div className="mt-3 rounded-lg bg-teal-50 p-2 dark:bg-teal-900/30">
          <p className="text-xs font-medium text-teal-700 dark:text-teal-300">
            <Clock className="mr-1 inline h-3 w-3" />
            {agent.currentTask}
          </p>
        </div>
      )}

      {/* Last Activity */}
      {agent.lastActivity && (
        <p className="mt-3 text-xs text-stone-500 dark:text-stone-400">
          Last activity: {formatRelativeTime(agent.lastActivity)}
        </p>
      )}

      {/* Controls */}
      <div className="mt-4 flex items-center justify-between">
        <div className="flex gap-1">
          {isStopped ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={onStart}
              disabled={isLoading}
              leftIcon={<Play className="h-3 w-3" />}
            >
              Start
            </Button>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              onClick={onStop}
              disabled={isLoading}
              leftIcon={<Pause className="h-3 w-3" />}
            >
              Stop
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={onRestart}
            disabled={isLoading}
            leftIcon={<RefreshCw className="h-3 w-3" />}
          >
            Restart
          </Button>
        </div>

        <Button
          variant="ghost"
          size="sm"
          onClick={() => setExpanded(!expanded)}
          rightIcon={expanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
        >
          Activity
        </Button>
      </div>

      {/* Activity Log (Expandable) */}
      <AnimatePresence>
        {expanded && activity && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="mt-4 space-y-2 border-t border-stone-200 pt-4 dark:border-stone-700">
              {activity.length === 0 ? (
                <p className="text-xs text-stone-500 dark:text-stone-400">
                  No recent activity
                </p>
              ) : (
                activity.map((item: AgentActivityType) => (
                  <div
                    key={item.id}
                    className="flex items-center gap-2 text-xs"
                  >
                    {item.success ? (
                      <CheckCircle2 className="h-3 w-3 text-emerald-500" />
                    ) : (
                      <AlertCircle className="h-3 w-3 text-red-500" />
                    )}
                    <span className="flex-1 truncate text-stone-600 dark:text-stone-300">
                      {item.action}
                    </span>
                    <span className="text-stone-400 dark:text-stone-500">
                      {formatRelativeTime(item.timestamp)}
                    </span>
                  </div>
                ))
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Agent Panel (Main Component)
// ─────────────────────────────────────────────────────────────────────────────

export function AgentPanel() {
  const { data: agents, isLoading, error, refetch } = useAgents();
  const { data: health } = useAgentHealth();
  const { mutate: sendCommand, isPending: isCommandPending } = useAgentCommand();

  const handleAgentAction = (agentType: AgentType, action: 'start' | 'stop' | 'restart') => {
    sendCommand({ agentType, command: action });
  };

  // Summary stats
  const healthyCount = agents?.filter((a) => a.status !== 'error' && a.status !== 'stopped').length ?? 0;
  const processingCount = agents?.filter((a) => a.status === 'processing').length ?? 0;
  const errorCount = agents?.filter((a) => a.status === 'error').length ?? 0;

  if (error) {
    return (
      <Card variant="elevated">
        <CardContent className="flex flex-col items-center justify-center py-8">
          <AlertCircle className="h-8 w-8 text-red-500" />
          <p className="mt-2 text-sm text-stone-600 dark:text-stone-400">
            Failed to load agent status
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
            <Activity className="h-5 w-5 text-teal-500" />
            Agent Status
          </CardTitle>
          <div className="flex items-center gap-3">
            {/* Summary badges */}
            <div className="flex items-center gap-2 text-xs">
              <span className="flex items-center gap-1 text-emerald-600 dark:text-emerald-400">
                <span className="h-2 w-2 rounded-full bg-emerald-500" />
                {healthyCount} healthy
              </span>
              {processingCount > 0 && (
                <span className="flex items-center gap-1 text-teal-600 dark:text-teal-400">
                  <span className="h-2 w-2 rounded-full bg-teal-500 animate-pulse" />
                  {processingCount} active
                </span>
              )}
              {errorCount > 0 && (
                <span className="flex items-center gap-1 text-red-600 dark:text-red-400">
                  <span className="h-2 w-2 rounded-full bg-red-500" />
                  {errorCount} error
                </span>
              )}
            </div>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={() => refetch()}
              disabled={isLoading}
            >
              <RefreshCw className={cn('h-4 w-4', isLoading && 'animate-spin')} />
            </Button>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="grid gap-4 md:grid-cols-2">
            {[1, 2, 3, 4].map((i) => (
              <div
                key={i}
                className="h-48 animate-pulse rounded-xl bg-stone-100 dark:bg-stone-700"
              />
            ))}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2">
            {agents?.map((agent) => (
              <AgentCard
                key={agent.id}
                agent={agent}
                onStart={() => handleAgentAction(agent.type, 'start')}
                onStop={() => handleAgentAction(agent.type, 'stop')}
                onRestart={() => handleAgentAction(agent.type, 'restart')}
                isLoading={isCommandPending}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Compact Agent Status (for header/sidebar)
// ─────────────────────────────────────────────────────────────────────────────

export function AgentStatusIndicator() {
  const { data: agents } = useAgents();

  if (!agents) return null;

  const healthyCount = agents.filter((a) => a.status !== 'error' && a.status !== 'stopped').length;
  const processingCount = agents.filter((a) => a.status === 'processing').length;
  const hasError = agents.some((a) => a.status === 'error');

  return (
    <div className="flex items-center gap-1.5">
      <span
        className={cn(
          'h-2 w-2 rounded-full',
          hasError
            ? 'bg-red-500'
            : processingCount > 0
            ? 'bg-teal-500 animate-pulse'
            : 'bg-emerald-500'
        )}
      />
      <span className="text-xs text-stone-500 dark:text-stone-400">
        {hasError
          ? 'Agent Error'
          : processingCount > 0
          ? `${processingCount} processing`
          : `${healthyCount}/4 agents`}
      </span>
    </div>
  );
}

export default AgentPanel;
