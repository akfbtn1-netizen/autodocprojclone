import React from 'react';
import type { FollowUpSuggestion } from '../../types/search';

interface FollowUpSuggestionsProps {
  suggestions: FollowUpSuggestion[];
  onSuggestionClick: (suggestionText: string) => void;
}

export const FollowUpSuggestions: React.FC<FollowUpSuggestionsProps> = ({
  suggestions,
  onSuggestionClick,
}) => {
  if (suggestions.length === 0) return null;

  return (
    <div className="mt-6 p-4 bg-teal-50 border border-teal-200 rounded-lg">
      <h3 className="text-sm font-medium text-teal-800 mb-3">
        Suggested follow-up searches
      </h3>
      <div className="flex flex-wrap gap-2">
        {suggestions.map((suggestion, idx) => (
          <button
            key={idx}
            onClick={() => onSuggestionClick(suggestion.suggestionText)}
            className="group inline-flex items-center gap-2 px-3 py-2 bg-white border border-teal-300 rounded-lg hover:bg-teal-100 hover:border-teal-400 transition-colors"
            title={suggestion.rationale || undefined}
          >
            <SuggestionIcon type={suggestion.suggestionType} />
            <span className="text-sm text-gray-700 group-hover:text-teal-800">
              {suggestion.suggestionText}
            </span>
            <span className="text-xs text-gray-400">
              {Math.round(suggestion.confidence * 100)}%
            </span>
          </button>
        ))}
      </div>
    </div>
  );
};

// Suggestion type icon
const SuggestionIcon: React.FC<{ type: string }> = ({ type }) => {
  const iconClass = 'w-4 h-4';

  switch (type.toLowerCase()) {
    case 'relationship':
      return (
        <svg
          className={`${iconClass} text-blue-500`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M13 10V3L4 14h7v7l9-11h-7z"
          />
        </svg>
      );
    case 'metadata':
    case 'filter':
      return (
        <svg
          className={`${iconClass} text-purple-500`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"
          />
        </svg>
      );
    case 'compliance':
      return (
        <svg
          className={`${iconClass} text-red-500`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"
          />
        </svg>
      );
    default:
      return (
        <svg
          className={`${iconClass} text-gray-500`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
          />
        </svg>
      );
  }
};

export default FollowUpSuggestions;
