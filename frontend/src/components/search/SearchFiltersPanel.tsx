import React from 'react';
import { useSearchStore } from '../../stores/searchStore';

export const SearchFiltersPanel: React.FC = () => {
  const {
    filters,
    togglePiiFilter,
    addDatabaseFilter,
    removeDatabaseFilter,
    addObjectTypeFilter,
    removeObjectTypeFilter,
    addCategoryFilter,
    removeCategoryFilter,
    resetFilters,
  } = useSearchStore();

  // Sample filter options - in production, fetch from API
  const databases = ['gwpc', 'DaQa', 'PolicyAdmin', 'Claims', 'Billing'];
  const objectTypes = ['Table', 'Column', 'StoredProcedure', 'View', 'Function'];
  const categories = ['Customer', 'Policy', 'Claims', 'Financial', 'Reference', 'Audit'];

  const hasActiveFilters =
    filters.databases.length > 0 ||
    filters.objectTypes.length > 0 ||
    filters.categories.length > 0 ||
    filters.showPiiOnly;

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="font-semibold text-gray-900">Filters</h3>
        {hasActiveFilters && (
          <button
            onClick={resetFilters}
            className="text-sm text-teal-600 hover:text-teal-700"
          >
            Clear all
          </button>
        )}
      </div>

      {/* PII Filter */}
      <div className="mb-6">
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={filters.showPiiOnly}
            onChange={togglePiiFilter}
            className="w-4 h-4 text-teal-600 rounded focus:ring-teal-500"
          />
          <span className="text-sm text-gray-700">Show PII columns only</span>
        </label>
      </div>

      {/* Database Filter */}
      <FilterSection
        title="Database"
        options={databases}
        selected={filters.databases}
        onAdd={addDatabaseFilter}
        onRemove={removeDatabaseFilter}
      />

      {/* Object Type Filter */}
      <FilterSection
        title="Object Type"
        options={objectTypes}
        selected={filters.objectTypes}
        onAdd={addObjectTypeFilter}
        onRemove={removeObjectTypeFilter}
      />

      {/* Category Filter */}
      <FilterSection
        title="Category"
        options={categories}
        selected={filters.categories}
        onAdd={addCategoryFilter}
        onRemove={removeCategoryFilter}
      />

      {/* Active Filters Summary */}
      {hasActiveFilters && (
        <div className="mt-6 pt-4 border-t border-gray-200">
          <h4 className="text-sm font-medium text-gray-700 mb-2">
            Active Filters
          </h4>
          <div className="flex flex-wrap gap-2">
            {filters.showPiiOnly && (
              <FilterTag label="PII Only" onRemove={togglePiiFilter} />
            )}
            {filters.databases.map((db) => (
              <FilterTag
                key={db}
                label={db}
                onRemove={() => removeDatabaseFilter(db)}
              />
            ))}
            {filters.objectTypes.map((type) => (
              <FilterTag
                key={type}
                label={type}
                onRemove={() => removeObjectTypeFilter(type)}
              />
            ))}
            {filters.categories.map((cat) => (
              <FilterTag
                key={cat}
                label={cat}
                onRemove={() => removeCategoryFilter(cat)}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

// Filter section component
interface FilterSectionProps {
  title: string;
  options: string[];
  selected: string[];
  onAdd: (value: string) => void;
  onRemove: (value: string) => void;
}

const FilterSection: React.FC<FilterSectionProps> = ({
  title,
  options,
  selected,
  onAdd,
  onRemove,
}) => {
  const [isExpanded, setIsExpanded] = React.useState(selected.length > 0);

  return (
    <div className="mb-4">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="flex items-center justify-between w-full text-left"
      >
        <span className="text-sm font-medium text-gray-700">{title}</span>
        <svg
          className={`w-4 h-4 text-gray-400 transition-transform ${
            isExpanded ? 'rotate-180' : ''
          }`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M19 9l-7 7-7-7"
          />
        </svg>
      </button>

      {isExpanded && (
        <div className="mt-2 space-y-1">
          {options.map((option) => {
            const isSelected = selected.includes(option);
            return (
              <label
                key={option}
                className="flex items-center gap-2 cursor-pointer py-1"
              >
                <input
                  type="checkbox"
                  checked={isSelected}
                  onChange={() => (isSelected ? onRemove(option) : onAdd(option))}
                  className="w-4 h-4 text-teal-600 rounded focus:ring-teal-500"
                />
                <span className="text-sm text-gray-600">{option}</span>
              </label>
            );
          })}
        </div>
      )}
    </div>
  );
};

// Filter tag component
interface FilterTagProps {
  label: string;
  onRemove: () => void;
}

const FilterTag: React.FC<FilterTagProps> = ({ label, onRemove }) => (
  <span className="inline-flex items-center gap-1 px-2 py-1 bg-teal-100 text-teal-700 text-xs rounded">
    {label}
    <button
      onClick={onRemove}
      className="hover:text-teal-900"
      aria-label={`Remove ${label} filter`}
    >
      <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
        <path
          fillRule="evenodd"
          d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z"
          clipRule="evenodd"
        />
      </svg>
    </button>
  </span>
);

export default SearchFiltersPanel;
