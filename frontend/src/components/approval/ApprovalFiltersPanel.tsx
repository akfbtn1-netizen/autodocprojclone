// ═══════════════════════════════════════════════════════════════════════════
// Approval Filters Panel Component
// Advanced filtering options for approval dashboard
// ═══════════════════════════════════════════════════════════════════════════

import { X, RotateCcw } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ApprovalFilters, ApprovalStatus, ApprovalPriority, DocumentTypeCode } from '@/types/approval';
import { DocumentTypeLabels, getStatusDisplayName } from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface ApprovalFiltersPanelProps {
  filters: ApprovalFilters;
  onFilterChange: <K extends keyof ApprovalFilters>(key: K, value: ApprovalFilters[K]) => void;
  onReset: () => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Filter Options
// ─────────────────────────────────────────────────────────────────────────────

const statusOptions: { value: ApprovalStatus; label: string }[] = [
  { value: 'PendingApproval', label: 'Pending Approval' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Editing', label: 'In Editing' },
  { value: 'RePromptRequested', label: 'Re-prompt Requested' },
];

const priorityOptions: { value: ApprovalPriority; label: string }[] = [
  { value: 'High', label: 'High' },
  { value: 'Medium', label: 'Medium' },
  { value: 'Low', label: 'Low' },
];

const documentTypeOptions: { value: DocumentTypeCode; label: string }[] = [
  { value: 'DF', label: 'Defect Fix' },
  { value: 'EN', label: 'Enhancement' },
  { value: 'BR', label: 'Business Request' },
  { value: 'AN', label: 'Analysis' },
  { value: 'ER', label: 'Error Report' },
  { value: 'EQ', label: 'Equipment' },
  { value: 'RS', label: 'Research' },
];

// ─────────────────────────────────────────────────────────────────────────────
// Checkbox Group Component
// ─────────────────────────────────────────────────────────────────────────────

interface CheckboxGroupProps<T extends string> {
  label: string;
  options: { value: T; label: string }[];
  selected: T[];
  onChange: (values: T[]) => void;
}

function CheckboxGroup<T extends string>({
  label,
  options,
  selected,
  onChange,
}: CheckboxGroupProps<T>) {
  const handleToggle = (value: T) => {
    if (selected.includes(value)) {
      onChange(selected.filter((v) => v !== value));
    } else {
      onChange([...selected, value]);
    }
  };

  return (
    <div>
      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
        {label}
      </label>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => {
          const isSelected = selected.includes(option.value);
          return (
            <button
              key={option.value}
              onClick={() => handleToggle(option.value)}
              className={cn(
                'px-3 py-1.5 text-sm rounded-lg border transition-colors',
                isSelected
                  ? 'bg-teal-50 dark:bg-teal-900/30 border-teal-300 dark:border-teal-700 text-teal-700 dark:text-teal-400'
                  : 'bg-white dark:bg-stone-900 border-stone-200 dark:border-stone-700 text-stone-600 dark:text-stone-400 hover:border-stone-300'
              )}
            >
              {option.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Date Range Component
// ─────────────────────────────────────────────────────────────────────────────

interface DateRangeProps {
  value?: { start: string; end: string };
  onChange: (value: { start: string; end: string } | undefined) => void;
}

function DateRangeFilter({ value, onChange }: DateRangeProps) {
  return (
    <div>
      <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
        Date Range
      </label>
      <div className="flex items-center gap-2">
        <input
          type="date"
          value={value?.start || ''}
          onChange={(e) =>
            onChange(e.target.value ? { start: e.target.value, end: value?.end || '' } : undefined)
          }
          className="flex-1 px-3 py-2 text-sm bg-white dark:bg-stone-900 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500"
        />
        <span className="text-stone-400">to</span>
        <input
          type="date"
          value={value?.end || ''}
          onChange={(e) =>
            onChange(e.target.value ? { start: value?.start || '', end: e.target.value } : undefined)
          }
          className="flex-1 px-3 py-2 text-sm bg-white dark:bg-stone-900 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500"
        />
        {value && (
          <button
            onClick={() => onChange(undefined)}
            className="p-2 text-stone-400 hover:text-stone-600 dark:hover:text-stone-300"
          >
            <X className="w-4 h-4" />
          </button>
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Active Filters Display
// ─────────────────────────────────────────────────────────────────────────────

interface ActiveFiltersProps {
  filters: ApprovalFilters;
  onRemove: <K extends keyof ApprovalFilters>(key: K, value?: string) => void;
  onReset: () => void;
}

function ActiveFilters({ filters, onRemove, onReset }: ActiveFiltersProps) {
  const hasFilters =
    (filters.status && filters.status.length > 0) ||
    (filters.documentType && filters.documentType.length > 0) ||
    (filters.priority && filters.priority.length > 0) ||
    filters.assignedTo ||
    filters.dateRange;

  if (!hasFilters) return null;

  return (
    <div className="flex flex-wrap items-center gap-2 pt-4 border-t border-stone-200 dark:border-stone-700">
      <span className="text-sm text-stone-500 dark:text-stone-400">Active filters:</span>

      {filters.status?.map((status) => (
        <span
          key={status}
          className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-teal-100 dark:bg-teal-900/30 text-teal-700 dark:text-teal-400 rounded-full"
        >
          {getStatusDisplayName(status)}
          <button
            onClick={() =>
              onRemove('status', status)
            }
            className="hover:text-teal-900 dark:hover:text-teal-200"
          >
            <X className="w-3 h-3" />
          </button>
        </span>
      ))}

      {filters.documentType?.map((type) => (
        <span
          key={type}
          className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400 rounded-full"
        >
          {DocumentTypeLabels[type]}
          <button
            onClick={() => onRemove('documentType', type)}
            className="hover:text-purple-900 dark:hover:text-purple-200"
          >
            <X className="w-3 h-3" />
          </button>
        </span>
      ))}

      {filters.priority?.map((priority) => (
        <span
          key={priority}
          className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400 rounded-full"
        >
          {priority}
          <button
            onClick={() => onRemove('priority', priority)}
            className="hover:text-amber-900 dark:hover:text-amber-200"
          >
            <X className="w-3 h-3" />
          </button>
        </span>
      ))}

      {filters.assignedTo && (
        <span className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400 rounded-full">
          Assigned: {filters.assignedTo}
          <button
            onClick={() => onRemove('assignedTo')}
            className="hover:text-blue-900 dark:hover:text-blue-200"
          >
            <X className="w-3 h-3" />
          </button>
        </span>
      )}

      {filters.dateRange && (
        <span className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-stone-100 dark:bg-stone-800 text-stone-700 dark:text-stone-400 rounded-full">
          {filters.dateRange.start} - {filters.dateRange.end}
          <button
            onClick={() => onRemove('dateRange')}
            className="hover:text-stone-900 dark:hover:text-stone-200"
          >
            <X className="w-3 h-3" />
          </button>
        </span>
      )}

      <button
        onClick={onReset}
        className="flex items-center gap-1 px-2 py-1 text-xs text-red-600 dark:text-red-400 hover:text-red-700 dark:hover:text-red-300"
      >
        <RotateCcw className="w-3 h-3" />
        Clear all
      </button>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Filters Panel Component
// ─────────────────────────────────────────────────────────────────────────────

export function ApprovalFiltersPanel({
  filters,
  onFilterChange,
  onReset,
}: ApprovalFiltersPanelProps) {
  const handleRemoveFilter = <K extends keyof ApprovalFilters>(
    key: K,
    value?: string
  ) => {
    if (key === 'status' && value) {
      onFilterChange('status', filters.status?.filter((s) => s !== value) || []);
    } else if (key === 'documentType' && value) {
      onFilterChange('documentType', filters.documentType?.filter((t) => t !== value) || []);
    } else if (key === 'priority' && value) {
      onFilterChange('priority', filters.priority?.filter((p) => p !== value) || []);
    } else if (key === 'assignedTo') {
      onFilterChange('assignedTo', undefined);
    } else if (key === 'dateRange') {
      onFilterChange('dateRange', undefined);
    }
  };

  return (
    <div className="bg-white dark:bg-stone-900 rounded-xl border border-stone-200 dark:border-stone-700 p-4">
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
        {/* Status Filter */}
        <CheckboxGroup
          label="Status"
          options={statusOptions}
          selected={filters.status || []}
          onChange={(values) => onFilterChange('status', values)}
        />

        {/* Priority Filter */}
        <CheckboxGroup
          label="Priority"
          options={priorityOptions}
          selected={filters.priority || []}
          onChange={(values) => onFilterChange('priority', values)}
        />

        {/* Document Type Filter */}
        <CheckboxGroup
          label="Document Type"
          options={documentTypeOptions}
          selected={filters.documentType || []}
          onChange={(values) => onFilterChange('documentType', values)}
        />

        {/* Date Range */}
        <DateRangeFilter
          value={filters.dateRange}
          onChange={(value) => onFilterChange('dateRange', value)}
        />
      </div>

      {/* Assigned To */}
      <div className="mt-4">
        <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-2">
          Assigned To
        </label>
        <input
          type="text"
          placeholder="Filter by assignee..."
          value={filters.assignedTo || ''}
          onChange={(e) => onFilterChange('assignedTo', e.target.value || undefined)}
          className="w-full max-w-xs px-3 py-2 text-sm bg-white dark:bg-stone-900 border border-stone-200 dark:border-stone-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500"
        />
      </div>

      {/* Active Filters */}
      <ActiveFilters filters={filters} onRemove={handleRemoveFilter} onReset={onReset} />
    </div>
  );
}

export default ApprovalFiltersPanel;
