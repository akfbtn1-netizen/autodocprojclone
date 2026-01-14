// ═══════════════════════════════════════════════════════════════════════════
// Search & Filter Components
// Faceted search for documents using MasterIndex metadata
// ═══════════════════════════════════════════════════════════════════════════

import { useState, useCallback, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Search,
  Filter,
  X,
  ChevronDown,
  ChevronUp,
  Check,
  Building2,
  Database,
  Shield,
  Tags,
  FileType,
  AlertTriangle,
} from 'lucide-react';
import { useDocumentFacets } from '@/hooks';
import { Button } from '@/components/ui/Button';
import { Badge } from '@/components/ui/Badge';
import { cn } from '@/lib/utils';
import type {
  SearchFilters,
  BusinessDomain,
  SemanticCategory,
  DataClassification,
  DocumentType,
  DocumentStatus,
  FacetCount,
} from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  onSearch: () => void;
  placeholder?: string;
  isLoading?: boolean;
}

interface FilterPanelProps {
  filters: SearchFilters;
  onChange: (filters: SearchFilters) => void;
  onClear: () => void;
  className?: string;
}

interface FacetGroupProps {
  title: string;
  icon: React.ReactNode;
  facets: FacetCount[];
  selected: string[];
  onChange: (selected: string[]) => void;
  defaultExpanded?: boolean;
}

interface ActiveFiltersProps {
  filters: SearchFilters;
  onRemove: (key: keyof SearchFilters, value?: string) => void;
  onClearAll: () => void;
}

// ─────────────────────────────────────────────────────────────────────────────
// Search Bar Component
// ─────────────────────────────────────────────────────────────────────────────

export function SearchBar({
  value,
  onChange,
  onSearch,
  placeholder = 'Search documents by title, description, keywords...',
  isLoading,
}: SearchBarProps) {
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      onSearch();
    }
  };

  return (
    <div className="relative">
      <Search className="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-stone-400" />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder={placeholder}
        className={cn(
          'w-full rounded-xl border border-stone-200 bg-white py-3 pl-10 pr-4',
          'text-sm text-stone-900 placeholder:text-stone-400',
          'focus:border-teal-500 focus:outline-none focus:ring-2 focus:ring-teal-500/20',
          'dark:border-stone-700 dark:bg-stone-800 dark:text-stone-100',
          'transition-all'
        )}
      />
      {value && (
        <button
          onClick={() => onChange('')}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-stone-400 hover:text-stone-600"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Facet Group Component
// ─────────────────────────────────────────────────────────────────────────────

function FacetGroup({
  title,
  icon,
  facets,
  selected,
  onChange,
  defaultExpanded = true,
}: FacetGroupProps) {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);
  const [showAll, setShowAll] = useState(false);

  const visibleFacets = showAll ? facets : facets.slice(0, 5);
  const hasMore = facets.length > 5;

  const toggleValue = (value: string) => {
    if (selected.includes(value)) {
      onChange(selected.filter((v) => v !== value));
    } else {
      onChange([...selected, value]);
    }
  };

  return (
    <div className="border-b border-stone-200 dark:border-stone-700 last:border-b-0">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-stone-50 dark:hover:bg-stone-800/50"
      >
        <div className="flex items-center gap-2">
          <span className="text-stone-500 dark:text-stone-400">{icon}</span>
          <span className="text-sm font-medium text-stone-900 dark:text-stone-100">
            {title}
          </span>
          {selected.length > 0 && (
            <Badge variant="brand" size="sm">
              {selected.length}
            </Badge>
          )}
        </div>
        {isExpanded ? (
          <ChevronUp className="h-4 w-4 text-stone-400" />
        ) : (
          <ChevronDown className="h-4 w-4 text-stone-400" />
        )}
      </button>

      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="px-4 pb-3 space-y-1">
              {visibleFacets.map((facet) => {
                const isSelected = selected.includes(facet.value);
                return (
                  <button
                    key={facet.value}
                    onClick={() => toggleValue(facet.value)}
                    className={cn(
                      'flex w-full items-center justify-between rounded-lg px-2 py-1.5 text-left text-sm',
                      'hover:bg-stone-100 dark:hover:bg-stone-700/50',
                      isSelected && 'bg-teal-50 text-teal-700 dark:bg-teal-900/30 dark:text-teal-300'
                    )}
                  >
                    <div className="flex items-center gap-2">
                      <div
                        className={cn(
                          'flex h-4 w-4 items-center justify-center rounded border',
                          isSelected
                            ? 'border-teal-500 bg-teal-500 text-white'
                            : 'border-stone-300 dark:border-stone-600'
                        )}
                      >
                        {isSelected && <Check className="h-3 w-3" />}
                      </div>
                      <span className="truncate">{facet.value}</span>
                    </div>
                    <span className="text-xs text-stone-400">{facet.count}</span>
                  </button>
                );
              })}
              {hasMore && (
                <button
                  onClick={() => setShowAll(!showAll)}
                  className="text-xs text-teal-600 hover:text-teal-700 dark:text-teal-400 mt-1"
                >
                  {showAll ? 'Show less' : `Show ${facets.length - 5} more`}
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
// Filter Panel Component
// ─────────────────────────────────────────────────────────────────────────────

export function FilterPanel({ filters, onChange, onClear, className }: FilterPanelProps) {
  const { data: facets, isLoading } = useDocumentFacets();

  const updateFilter = useCallback(
    <K extends keyof SearchFilters>(key: K, value: SearchFilters[K]) => {
      onChange({ ...filters, [key]: value });
    },
    [filters, onChange]
  );

  const activeFilterCount = useMemo(() => {
    let count = 0;
    if (filters.documentTypes?.length) count += filters.documentTypes.length;
    if (filters.businessDomains?.length) count += filters.businessDomains.length;
    if (filters.semanticCategories?.length) count += filters.semanticCategories.length;
    if (filters.schemas?.length) count += filters.schemas.length;
    if (filters.dataClassifications?.length) count += filters.dataClassifications.length;
    if (filters.complianceTags?.length) count += filters.complianceTags.length;
    if (filters.containsPii !== undefined) count += 1;
    return count;
  }, [filters]);

  if (isLoading) {
    return (
      <div className={cn('rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-800', className)}>
        <div className="p-4 space-y-3">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-10 animate-pulse rounded bg-stone-100 dark:bg-stone-700" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className={cn('rounded-xl border border-stone-200 dark:border-stone-700 bg-white dark:bg-stone-800 overflow-hidden', className)}>
      {/* Header */}
      <div className="flex items-center justify-between border-b border-stone-200 dark:border-stone-700 px-4 py-3">
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
          <Button variant="ghost" size="sm" onClick={onClear}>
            Clear all
          </Button>
        )}
      </div>

      {/* Document Type */}
      {facets?.documentTypes && (
        <FacetGroup
          title="Document Type"
          icon={<FileType className="h-4 w-4" />}
          facets={facets.documentTypes}
          selected={filters.documentTypes || []}
          onChange={(selected) => updateFilter('documentTypes', selected as DocumentType[])}
        />
      )}

      {/* Business Domain */}
      {facets?.businessDomains && (
        <FacetGroup
          title="Business Domain"
          icon={<Building2 className="h-4 w-4" />}
          facets={facets.businessDomains}
          selected={filters.businessDomains || []}
          onChange={(selected) => updateFilter('businessDomains', selected as BusinessDomain[])}
        />
      )}

      {/* Semantic Category */}
      {facets?.semanticCategories && (
        <FacetGroup
          title="Semantic Category"
          icon={<Tags className="h-4 w-4" />}
          facets={facets.semanticCategories}
          selected={filters.semanticCategories || []}
          onChange={(selected) => updateFilter('semanticCategories', selected as SemanticCategory[])}
        />
      )}

      {/* Schema */}
      {facets?.schemas && (
        <FacetGroup
          title="Schema"
          icon={<Database className="h-4 w-4" />}
          facets={facets.schemas}
          selected={filters.schemas || []}
          onChange={(selected) => updateFilter('schemas', selected)}
          defaultExpanded={false}
        />
      )}

      {/* Data Classification */}
      {facets?.dataClassifications && (
        <FacetGroup
          title="Data Classification"
          icon={<Shield className="h-4 w-4" />}
          facets={facets.dataClassifications}
          selected={filters.dataClassifications || []}
          onChange={(selected) => updateFilter('dataClassifications', selected as DataClassification[])}
          defaultExpanded={false}
        />
      )}

      {/* PII Toggle */}
      <div className="border-b border-stone-200 dark:border-stone-700 px-4 py-3">
        <button
          onClick={() => {
            if (filters.containsPii === true) {
              updateFilter('containsPii', undefined);
            } else {
              updateFilter('containsPii', true);
            }
          }}
          className={cn(
            'flex w-full items-center justify-between rounded-lg px-2 py-2 text-left text-sm',
            'hover:bg-stone-100 dark:hover:bg-stone-700/50',
            filters.containsPii === true && 'bg-amber-50 text-amber-700 dark:bg-amber-900/30'
          )}
        >
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-4 w-4 text-amber-500" />
            <span>Contains PII Only</span>
          </div>
          {filters.containsPii === true && <Check className="h-4 w-4 text-amber-600" />}
        </button>
      </div>

      {/* Compliance Tags */}
      {facets?.complianceTags && (
        <FacetGroup
          title="Compliance"
          icon={<Shield className="h-4 w-4" />}
          facets={facets.complianceTags}
          selected={filters.complianceTags || []}
          onChange={(selected) => updateFilter('complianceTags', selected as any[])}
          defaultExpanded={false}
        />
      )}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Active Filters Component
// ─────────────────────────────────────────────────────────────────────────────

export function ActiveFilters({ filters, onRemove, onClearAll }: ActiveFiltersProps) {
  const filterTags: { key: keyof SearchFilters; value: string; label: string }[] = [];

  // Build filter tags
  filters.documentTypes?.forEach((v) => filterTags.push({ key: 'documentTypes', value: v, label: v }));
  filters.businessDomains?.forEach((v) => filterTags.push({ key: 'businessDomains', value: v, label: v }));
  filters.semanticCategories?.forEach((v) => filterTags.push({ key: 'semanticCategories', value: v, label: v }));
  filters.schemas?.forEach((v) => filterTags.push({ key: 'schemas', value: v, label: v }));
  filters.dataClassifications?.forEach((v) => filterTags.push({ key: 'dataClassifications', value: v, label: v }));
  filters.complianceTags?.forEach((v) => filterTags.push({ key: 'complianceTags', value: v, label: v }));
  if (filters.containsPii === true) {
    filterTags.push({ key: 'containsPii', value: 'true', label: 'Contains PII' });
  }

  if (filterTags.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-sm text-stone-500">Active filters:</span>
      {filterTags.map((tag, index) => (
        <Badge
          key={`${tag.key}-${tag.value}-${index}`}
          variant="default"
          size="sm"
          className="flex items-center gap-1"
        >
          {tag.label}
          <button
            onClick={() => onRemove(tag.key, tag.value)}
            className="ml-1 hover:text-red-500"
          >
            <X className="h-3 w-3" />
          </button>
        </Badge>
      ))}
      <Button variant="ghost" size="sm" onClick={onClearAll}>
        Clear all
      </Button>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Combined Search Component
// ─────────────────────────────────────────────────────────────────────────────

interface DocumentSearchProps {
  onSearch: (filters: SearchFilters) => void;
  initialFilters?: SearchFilters;
  showFilterPanel?: boolean;
}

export function DocumentSearch({
  onSearch,
  initialFilters = {},
  showFilterPanel = true,
}: DocumentSearchProps) {
  const [query, setQuery] = useState(initialFilters.query || '');
  const [filters, setFilters] = useState<SearchFilters>(initialFilters);
  const [isFilterOpen, setIsFilterOpen] = useState(false);

  const handleSearch = useCallback(() => {
    onSearch({ ...filters, query: query || undefined });
  }, [filters, query, onSearch]);

  const handleFilterChange = useCallback((newFilters: SearchFilters) => {
    setFilters(newFilters);
    onSearch({ ...newFilters, query: query || undefined });
  }, [query, onSearch]);

  const clearFilters = useCallback(() => {
    setFilters({});
    setQuery('');
    onSearch({});
  }, [onSearch]);

  const removeFilter = useCallback((key: keyof SearchFilters, value?: string) => {
    const newFilters = { ...filters };
    
    if (key === 'containsPii') {
      delete newFilters.containsPii;
    } else if (value && Array.isArray(newFilters[key])) {
      (newFilters[key] as string[]) = (newFilters[key] as string[]).filter((v) => v !== value);
      if ((newFilters[key] as string[]).length === 0) {
        delete newFilters[key];
      }
    }

    setFilters(newFilters);
    onSearch({ ...newFilters, query: query || undefined });
  }, [filters, query, onSearch]);

  return (
    <div className="space-y-4">
      {/* Search Bar */}
      <div className="flex gap-2">
        <div className="flex-1">
          <SearchBar
            value={query}
            onChange={setQuery}
            onSearch={handleSearch}
          />
        </div>
        {showFilterPanel && (
          <Button
            variant="outline"
            onClick={() => setIsFilterOpen(!isFilterOpen)}
            leftIcon={<Filter className="h-4 w-4" />}
          >
            Filters
            {Object.keys(filters).length > 0 && (
              <Badge variant="brand" size="sm" className="ml-2">
                {Object.keys(filters).length}
              </Badge>
            )}
          </Button>
        )}
        <Button variant="brand" onClick={handleSearch}>
          Search
        </Button>
      </div>

      {/* Active Filters */}
      <ActiveFilters
        filters={filters}
        onRemove={removeFilter}
        onClearAll={clearFilters}
      />

      {/* Filter Panel (collapsible) */}
      <AnimatePresence>
        {isFilterOpen && showFilterPanel && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
          >
            <FilterPanel
              filters={filters}
              onChange={handleFilterChange}
              onClear={clearFilters}
            />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default DocumentSearch;
