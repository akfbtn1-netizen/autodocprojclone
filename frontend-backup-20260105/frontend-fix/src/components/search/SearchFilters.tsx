// ═══════════════════════════════════════════════════════════════════════════
// Search & Filter Components
// Faceted search using MasterIndex metadata (119 columns)
// Supports: BusinessDomain, SemanticCategory, Schema, PII, Compliance, etc.
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useCallback, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Search,
  Filter,
  X,
  ChevronDown,
  ChevronUp,
  Building2,
  Database,
  Shield,
  FileText,
  Tag,
  CheckCircle2,
  AlertTriangle,
  Sparkles,
} from 'lucide-react';
import { useDocumentFacets, useDocumentSearch } from '@/hooks';
import { Card, CardContent } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { Input } from '@/components/ui/Input';
import { cn } from '@/lib/utils';
import type {
  SearchFilters,
  SearchFacets,
  FacetCount,
  DocumentType,
  DocumentStatus,
  BusinessDomain,
  SemanticCategory,
  DataClassification,
  ComplianceTag,
} from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Facet Group Component
// ─────────────────────────────────────────────────────────────────────────────

interface FacetGroupProps {
  title: string;
  icon: React.ReactNode;
  facets: FacetCount[];
  selectedValues: string[];
  onToggle: (value: string) => void;
  maxVisible?: number;
  variant?: 'default' | 'brand' | 'warning' | 'danger';
}

function FacetGroup({
  title,
  icon,
  facets,
  selectedValues,
  onToggle,
  maxVisible = 5,
  variant = 'default',
}: FacetGroupProps) {
  const [expanded, setExpanded] = useState(false);
  const [showAll, setShowAll] = useState(false);

  const visibleFacets = showAll ? facets : facets.slice(0, maxVisible);
  const hasMore = facets.length > maxVisible;

  if (facets.length === 0) return null;

  return (
    <div className="border-b border-stone-200 dark:border-stone-700 last:border-b-0">
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-stone-50 dark:hover:bg-stone-800/50"
      >
        <div className="flex items-center gap-2">
          <span className="text-stone-500 dark:text-stone-400">{icon}</span>
          <span className="font-medium text-stone-900 dark:text-stone-100">{title}</span>
          {selectedValues.length > 0 && (
            <Badge variant="brand" size="sm">
              {selectedValues.length}
            </Badge>
          )}
        </div>
        {expanded ? (
          <ChevronUp className="h-4 w-4 text-stone-400" />
        ) : (
          <ChevronDown className="h-4 w-4 text-stone-400" />
        )}
      </button>

      <AnimatePresence>
        {expanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="px-4 pb-3 space-y-1">
              {visibleFacets.map((facet) => {
                const isSelected = selectedValues.includes(facet.value);
                return (
                  <button
                    key={facet.value}
                    onClick={() => onToggle(facet.value)}
                    className={cn(
                      'flex w-full items-center justify-between px-2 py-1.5 rounded-lg text-sm transition-colors',
                      isSelected
                        ? 'bg-teal-100 text-teal-700 dark:bg-teal-900/50 dark:text-teal-300'
                        : 'hover:bg-stone-100 dark:hover:bg-stone-700/50 text-stone-700 dark:text-stone-300'
                    )}
                  >
                    <div className="flex items-center gap-2">
                      <div
                        className={cn(
                          'w-4 h-4 rounded border flex items-center justify-center',
                          isSelected
                            ? 'bg-teal-500 border-teal-500'
                            : 'border-stone-300 dark:border-stone-600'
                        )}
                      >
                        {isSelected && <CheckCircle2 className="h-3 w-3 text-white" />}
                      </div>
                      <span className="truncate">{facet.value}</span>
                    </div>
                    <span className="text-xs text-stone-500 dark:text-stone-400">
                      {facet.count}
                    </span>
                  </button>
                );
              })}

              {hasMore && (
                <button
                  onClick={() => setShowAll(!showAll)}
                  className="text-xs text-teal-600 dark:text-teal-400 hover:underline mt-2"
                >
                  {showAll ? 'Show less' : `Show ${facets.length - maxVisible} more`}
                </button>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Toggle Filter (for boolean filters like PII)
// ─────────────────────────────────────────────────────────────────────────────

interface ToggleFilterProps {
  label: string;
  icon: React.ReactNode;
  value: boolean | undefined;
  onChange: (value: boolean | undefined) => void;
  trueLabel?: string;
  falseLabel?: string;
}

function ToggleFilter({
  label,
  icon,
  value,
  onChange,
  trueLabel = 'Yes',
  falseLabel = 'No',
}: ToggleFilterProps) {
  return (
    <div className="px-4 py-3 border-b border-stone-200 dark:border-stone-700">
      <div className="flex items-center gap-2 mb-2">
        <span className="text-stone-500 dark:text-stone-400">{icon}</span>
        <span className="font-medium text-stone-900 dark:text-stone-100">{label}</span>
      </div>
      <div className="flex gap-2">
        <button
          onClick={() => onChange(value === true ? undefined : true)}
          className={cn(
            'px-3 py-1 rounded-lg text-sm font-medium transition-colors',
            value === true
              ? 'bg-teal-100 text-teal-700 dark:bg-teal-900/50 dark:text-teal-300'
              : 'bg-stone-100 text-stone-600 dark:bg-stone-700 dark:text-stone-400 hover:bg-stone-200 dark:hover:bg-stone-600'
          )}
        >
          {trueLabel}
        </button>
        <button
          onClick={() => onChange(value === false ? undefined : false)}
          className={cn(
            'px-3 py-1 rounded-lg text-sm font-medium transition-colors',
            value === false
              ? 'bg-teal-100 text-teal-700 dark:bg-teal-900/50 dark:text-teal-300'
              : 'bg-stone-100 text-stone-600 dark:bg-stone-700 dark:text-stone-400 hover:bg-stone-200 dark:hover:bg-stone-600'
          )}
        >
          {falseLabel}
        </button>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Range Filter (for scores)
// ─────────────────────────────────────────────────────────────────────────────

interface RangeFilterProps {
  label: string;
  icon: React.ReactNode;
  value: number | undefined;
  onChange: (value: number | undefined) => void;
  min?: number;
  max?: number;
  step?: number;
}

function RangeFilter({
  label,
  icon,
  value,
  onChange,
  min = 0,
  max = 100,
  step = 10,
}: RangeFilterProps) {
  return (
    <div className="px-4 py-3 border-b border-stone-200 dark:border-stone-700">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <span className="text-stone-500 dark:text-stone-400">{icon}</span>
          <span className="font-medium text-stone-900 dark:text-stone-100">{label}</span>
        </div>
        <span className="text-sm text-stone-500">
          {value !== undefined ? `≥ ${value}%` : 'Any'}
        </span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value ?? min}
        onChange={(e) => {
          const val = Number(e.target.value);
          onChange(val === min ? undefined : val);
        }}
        className="w-full h-2 bg-stone-200 dark:bg-stone-700 rounded-lg appearance-none cursor-pointer accent-teal-500"
      />
      <div className="flex justify-between text-xs text-stone-400 mt-1">
        <span>{min}%</span>
        <span>{max}%</span>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Search Filters Sidebar
// ─────────────────────────────────────────────────────────────────────────────

interface SearchFiltersSidebarProps {
  filters: SearchFilters;
  onFiltersChange: (filters: SearchFilters) => void;
  facets?: SearchFacets;
  isLoading?: boolean;
}

export function SearchFiltersSidebar({
  filters,
  onFiltersChange,
  facets,
  isLoading,
}: SearchFiltersSidebarProps) {
  const { data: defaultFacets } = useDocumentFacets();
  const activeFacets = facets || defaultFacets;

  const toggleArrayFilter = useCallback(
    <K extends keyof SearchFilters>(key: K, value: string) => {
      const current = (filters[key] as string[] | undefined) || [];
      const newValues = current.includes(value)
        ? current.filter((v) => v !== value)
        : [...current, value];
      onFiltersChange({
        ...filters,
        [key]: newValues.length > 0 ? newValues : undefined,
      });
    },
    [filters, onFiltersChange]
  );

  const activeFilterCount = useMemo(() => {
    let count = 0;
    if (filters.documentTypes?.length) count += filters.documentTypes.length;
    if (filters.statuses?.length) count += filters.statuses.length;
    if (filters.businessDomains?.length) count += filters.businessDomains.length;
    if (filters.semanticCategories?.length) count += filters.semanticCategories.length;
    if (filters.schemas?.length) count += filters.schemas.length;
    if (filters.complianceTags?.length) count += filters.complianceTags.length;
    if (filters.containsPii !== undefined) count++;
    if (filters.completenessScoreMin !== undefined) count++;
    if (filters.qualityScoreMin !== undefined) count++;
    return count;
  }, [filters]);

  const clearAllFilters = useCallback(() => {
    onFiltersChange({ query: filters.query });
  }, [filters.query, onFiltersChange]);

  return (
    <Card variant="elevated" className="overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-stone-200 dark:border-stone-700 bg-stone-50 dark:bg-stone-800/50">
        <div className="flex items-center gap-2">
          <Filter className="h-4 w-4 text-stone-500" />
          <span className="font-medium text-stone-900 dark:text-stone-100">Filters</span>
          {activeFilterCount > 0 && (
            <Badge variant="brand" size="sm">
              {activeFilterCount}
            </Badge>
          )}
        </div>
        {activeFilterCount > 0 && (
          <button
            onClick={clearAllFilters}
            className="text-xs text-teal-600 dark:text-teal-400 hover:underline"
          >
            Clear all
          </button>
        )}
      </div>

      {/* Facet Groups */}
      <div className="divide-y divide-stone-200 dark:divide-stone-700">
        {/* Document Type */}
        <FacetGroup
          title="Document Type"
          icon={<FileText className="h-4 w-4" />}
          facets={activeFacets?.documentTypes || []}
          selectedValues={filters.documentTypes || []}
          onToggle={(value) => toggleArrayFilter('documentTypes', value as DocumentType)}
        />

        {/* Business Domain */}
        <FacetGroup
          title="Business Domain"
          icon={<Building2 className="h-4 w-4" />}
          facets={activeFacets?.businessDomains || []}
          selectedValues={filters.businessDomains || []}
          onToggle={(value) => toggleArrayFilter('businessDomains', value as BusinessDomain)}
          variant="brand"
        />

        {/* Semantic Category */}
        <FacetGroup
          title="Semantic Category"
          icon={<Tag className="h-4 w-4" />}
          facets={activeFacets?.semanticCategories || []}
          selectedValues={filters.semanticCategories || []}
          onToggle={(value) => toggleArrayFilter('semanticCategories', value as SemanticCategory)}
        />

        {/* Schema */}
        <FacetGroup
          title="Database Schema"
          icon={<Database className="h-4 w-4" />}
          facets={activeFacets?.schemas || []}
          selectedValues={filters.schemas || []}
          onToggle={(value) => toggleArrayFilter('schemas', value)}
        />

        {/* Data Classification */}
        <FacetGroup
          title="Data Classification"
          icon={<Shield className="h-4 w-4" />}
          facets={activeFacets?.dataClassifications || []}
          selectedValues={filters.dataClassifications || []}
          onToggle={(value) => toggleArrayFilter('dataClassifications', value as DataClassification)}
          variant="warning"
        />

        {/* Compliance Tags */}
        <FacetGroup
          title="Compliance"
          icon={<AlertTriangle className="h-4 w-4" />}
          facets={activeFacets?.complianceTags || []}
          selectedValues={filters.complianceTags || []}
          onToggle={(value) => toggleArrayFilter('complianceTags', value as ComplianceTag)}
          variant="danger"
        />

        {/* Status */}
        <FacetGroup
          title="Status"
          icon={<CheckCircle2 className="h-4 w-4" />}
          facets={activeFacets?.statuses || []}
          selectedValues={filters.statuses || []}
          onToggle={(value) => toggleArrayFilter('statuses', value as DocumentStatus)}
        />

        {/* PII Toggle */}
        <ToggleFilter
          label="Contains PII"
          icon={<Shield className="h-4 w-4" />}
          value={filters.containsPii}
          onChange={(value) => onFiltersChange({ ...filters, containsPii: value })}
          trueLabel="With PII"
          falseLabel="No PII"
        />

        {/* Completeness Score */}
        <RangeFilter
          label="Min Completeness"
          icon={<Sparkles className="h-4 w-4" />}
          value={filters.completenessScoreMin}
          onChange={(value) => onFiltersChange({ ...filters, completenessScoreMin: value })}
        />

        {/* Quality Score */}
        <RangeFilter
          label="Min Quality"
          icon={<CheckCircle2 className="h-4 w-4" />}
          value={filters.qualityScoreMin}
          onChange={(value) => onFiltersChange({ ...filters, qualityScoreMin: value })}
        />
      </div>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Search Bar Component
// ─────────────────────────────────────────────────────────────────────────────

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSearch?: () => void;
  placeholder?: string;
  isLoading?: boolean;
}

export function SearchBar({
  value,
  onChange,
  onSearch,
  placeholder = 'Search documents, tables, columns...',
  isLoading,
}: SearchBarProps) {
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && onSearch) {
      onSearch();
    }
  };

  return (
    <div className="relative">
      <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-stone-400" />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        className={cn(
          'w-full pl-10 pr-10 py-3 rounded-xl border border-stone-300 dark:border-stone-600',
          'bg-white dark:bg-stone-800 text-stone-900 dark:text-stone-100',
          'placeholder:text-stone-400 dark:placeholder:text-stone-500',
          'focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent',
          'transition-shadow'
        )}
      />
      {value && (
        <button
          onClick={() => onChange('')}
          className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-stone-400 hover:text-stone-600"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Active Filters Pills
// ─────────────────────────────────────────────────────────────────────────────

interface ActiveFiltersPillsProps {
  filters: SearchFilters;
  onRemove: (key: keyof SearchFilters, value?: string) => void;
  onClearAll: () => void;
}

export function ActiveFiltersPills({ filters, onRemove, onClearAll }: ActiveFiltersPillsProps) {
  const pills: { key: keyof SearchFilters; value: string; label: string }[] = [];

  // Collect all active filter values
  if (filters.documentTypes) {
    filters.documentTypes.forEach((v) =>
      pills.push({ key: 'documentTypes', value: v, label: `Type: ${v}` })
    );
  }
  if (filters.businessDomains) {
    filters.businessDomains.forEach((v) =>
      pills.push({ key: 'businessDomains', value: v, label: v })
    );
  }
  if (filters.semanticCategories) {
    filters.semanticCategories.forEach((v) =>
      pills.push({ key: 'semanticCategories', value: v, label: v })
    );
  }
  if (filters.schemas) {
    filters.schemas.forEach((v) =>
      pills.push({ key: 'schemas', value: v, label: `Schema: ${v}` })
    );
  }
  if (filters.complianceTags) {
    filters.complianceTags.forEach((v) =>
      pills.push({ key: 'complianceTags', value: v, label: v })
    );
  }
  if (filters.containsPii !== undefined) {
    pills.push({
      key: 'containsPii',
      value: String(filters.containsPii),
      label: filters.containsPii ? 'Contains PII' : 'No PII',
    });
  }
  if (filters.completenessScoreMin !== undefined) {
    pills.push({
      key: 'completenessScoreMin',
      value: String(filters.completenessScoreMin),
      label: `Completeness ≥ ${filters.completenessScoreMin}%`,
    });
  }

  if (pills.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-sm text-stone-500 dark:text-stone-400">Active filters:</span>
      {pills.map((pill, index) => (
        <motion.span
          key={`${pill.key}-${pill.value}-${index}`}
          initial={{ scale: 0.8, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.8, opacity: 0 }}
          className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-teal-100 dark:bg-teal-900/50 text-teal-700 dark:text-teal-300 text-sm"
        >
          {pill.label}
          <button
            onClick={() => onRemove(pill.key, pill.value)}
            className="hover:bg-teal-200 dark:hover:bg-teal-800 rounded-full p-0.5"
          >
            <X className="h-3 w-3" />
          </button>
        </motion.span>
      ))}
      <button
        onClick={onClearAll}
        className="text-sm text-teal-600 dark:text-teal-400 hover:underline"
      >
        Clear all
      </button>
    </div>
  );
}

export default SearchFiltersSidebar;
