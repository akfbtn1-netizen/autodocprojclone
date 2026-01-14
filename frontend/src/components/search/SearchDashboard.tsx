import React, { useState, useCallback, useEffect } from 'react';
import { useSearchStore } from '../../stores/searchStore';
import {
  useSearchMutation,
  useSearchSuggestions,
  useRecordInteraction,
  useGraphStats,
} from '../../hooks/useSearch';
import { SearchResultsList } from './SearchResultsList';
import { SearchFiltersPanel } from './SearchFiltersPanel';
import { LineagePanel } from './LineagePanel';
import { FollowUpSuggestions } from './FollowUpSuggestions';
import type { SearchRequest } from '../../types/search';

export const SearchDashboard: React.FC = () => {
  const [inputValue, setInputValue] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [selectedResultId, setSelectedResultId] = useState<string | null>(null);

  const {
    query,
    results,
    currentQueryId,
    routingPath,
    isLoading,
    error,
    filters,
    followUpSuggestions,
    metadata,
    setQuery,
  } = useSearchStore();

  const searchMutation = useSearchMutation();
  const { data: suggestions } = useSearchSuggestions(inputValue, showSuggestions);
  const recordInteraction = useRecordInteraction();
  const { data: graphStats } = useGraphStats();

  // Handle search execution
  const handleSearch = useCallback(
    (searchQuery: string) => {
      if (!searchQuery.trim()) return;

      setQuery(searchQuery);
      setShowSuggestions(false);

      const request: SearchRequest = {
        query: searchQuery,
        maxResults: 20,
        includeLineage: true,
        includePiiFlows: filters.showPiiOnly,
        enableReranking: true,
        filterDatabases: filters.databases.length > 0 ? filters.databases : undefined,
        filterObjectTypes: filters.objectTypes.length > 0 ? filters.objectTypes : undefined,
        filterCategories: filters.categories.length > 0 ? filters.categories : undefined,
      };

      searchMutation.mutate(request);
    },
    [filters, setQuery, searchMutation]
  );

  // Handle input change with debounce for suggestions
  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setInputValue(value);
    setShowSuggestions(value.length >= 2);
  };

  // Handle suggestion click
  const handleSuggestionClick = (suggestion: string) => {
    setInputValue(suggestion);
    handleSearch(suggestion);
  };

  // Handle result click
  const handleResultClick = (documentId: string) => {
    setSelectedResultId(documentId);

    if (currentQueryId) {
      recordInteraction.mutate({
        queryId: currentQueryId,
        interactionType: 'click',
        documentId,
        data: { rank: results.findIndex((r) => r.documentId === documentId) + 1 },
      });
    }
  };

  // Handle follow-up suggestion click
  const handleFollowUpClick = (suggestionText: string) => {
    setInputValue(suggestionText);
    handleSearch(suggestionText);
  };

  // Handle keyboard navigation
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSearch(inputValue);
    } else if (e.key === 'Escape') {
      setShowSuggestions(false);
    }
  };

  return (
    <div className="min-h-screen bg-stone-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-teal-600 to-teal-700 text-white py-8 px-6">
        <div className="max-w-7xl mx-auto">
          <h1 className="text-3xl font-bold mb-2">Smart Search</h1>
          <p className="text-teal-100">
            AI-powered search across database documentation with lineage tracking
          </p>

          {/* Search Bar */}
          <div className="mt-6 relative">
            <div className="flex items-center bg-white rounded-lg shadow-lg">
              <input
                type="text"
                value={inputValue}
                onChange={handleInputChange}
                onKeyDown={handleKeyDown}
                onFocus={() => setShowSuggestions(inputValue.length >= 2)}
                onBlur={() => setTimeout(() => setShowSuggestions(false), 200)}
                placeholder="Search for tables, columns, procedures... Try: 'customer data' or 'dbo.Orders'"
                className="flex-1 px-6 py-4 text-gray-800 rounded-l-lg focus:outline-none text-lg"
              />
              <button
                onClick={() => handleSearch(inputValue)}
                disabled={isLoading}
                className="px-8 py-4 bg-teal-600 text-white rounded-r-lg hover:bg-teal-700 transition-colors disabled:opacity-50"
              >
                {isLoading ? (
                  <span className="flex items-center">
                    <svg
                      className="animate-spin h-5 w-5 mr-2"
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
                    Searching...
                  </span>
                ) : (
                  'Search'
                )}
              </button>
            </div>

            {/* Suggestions Dropdown */}
            {showSuggestions && suggestions && suggestions.length > 0 && (
              <div className="absolute top-full left-0 right-0 mt-2 bg-white rounded-lg shadow-lg z-50">
                {suggestions.map((suggestion, idx) => (
                  <button
                    key={idx}
                    onClick={() => handleSuggestionClick(suggestion)}
                    className="w-full px-6 py-3 text-left text-gray-700 hover:bg-teal-50 first:rounded-t-lg last:rounded-b-lg"
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Graph Stats */}
          {graphStats && (
            <div className="mt-4 flex gap-6 text-sm text-teal-100">
              <span>{graphStats.nodeCount.toLocaleString()} objects indexed</span>
              <span>{graphStats.edgeCount.toLocaleString()} relationships</span>
              <span>{graphStats.piiFlowCount.toLocaleString()} PII flows tracked</span>
            </div>
          )}
        </div>
      </div>

      {/* Main Content */}
      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Error Display */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg text-red-700">
            {error}
          </div>
        )}

        {/* Search Metadata */}
        {metadata && results.length > 0 && (
          <div className="mb-6 flex items-center justify-between text-sm text-gray-600">
            <div className="flex items-center gap-4">
              <span className="font-medium">
                {metadata.filteredResults} results
              </span>
              <span className="px-2 py-1 bg-teal-100 text-teal-700 rounded">
                {routingPath} search
              </span>
              <span>in {metadata.processingTime}</span>
              {metadata.cacheHit && (
                <span className="text-green-600">Cached</span>
              )}
            </div>
            {query && (
              <span className="text-gray-500">
                Searching for: "{query}"
              </span>
            )}
          </div>
        )}

        <div className="grid grid-cols-12 gap-6">
          {/* Filters Panel */}
          <div className="col-span-3">
            <SearchFiltersPanel />
          </div>

          {/* Results */}
          <div className="col-span-6">
            {results.length > 0 ? (
              <SearchResultsList
                results={results}
                onResultClick={handleResultClick}
                selectedId={selectedResultId}
              />
            ) : query && !isLoading ? (
              <div className="text-center py-12 text-gray-500">
                <p className="text-lg">No results found for "{query}"</p>
                <p className="mt-2">Try different keywords or adjust filters</p>
              </div>
            ) : !isLoading ? (
              <div className="text-center py-12 text-gray-500">
                <p className="text-lg">Enter a search query to get started</p>
                <p className="mt-2">
                  Try natural language like "customer data in claims" or exact names like "dbo.Orders"
                </p>
              </div>
            ) : null}

            {/* Follow-up Suggestions */}
            {followUpSuggestions.length > 0 && (
              <FollowUpSuggestions
                suggestions={followUpSuggestions}
                onSuggestionClick={handleFollowUpClick}
              />
            )}
          </div>

          {/* Lineage Panel */}
          <div className="col-span-3">
            {selectedResultId && (
              <LineagePanel
                nodeId={selectedResultId}
                onNodeClick={setSelectedResultId}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default SearchDashboard;
