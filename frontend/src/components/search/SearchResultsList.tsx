import React from 'react';
import type { SearchResultItem } from '../../types/search';

interface SearchResultsListProps {
  results: SearchResultItem[];
  onResultClick: (documentId: string) => void;
  selectedId: string | null;
}

export const SearchResultsList: React.FC<SearchResultsListProps> = ({
  results,
  onResultClick,
  selectedId,
}) => {
  return (
    <div className="space-y-4">
      {results.map((result, index) => (
        <div
          key={result.documentId}
          onClick={() => onResultClick(result.documentId)}
          className={`p-4 bg-white rounded-lg shadow-sm border cursor-pointer transition-all hover:shadow-md ${
            selectedId === result.documentId
              ? 'border-teal-500 ring-2 ring-teal-200'
              : 'border-gray-200 hover:border-teal-300'
          }`}
        >
          {/* Header */}
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-3">
              <span className="text-sm text-gray-400 font-mono">
                #{index + 1}
              </span>
              <ObjectTypeIcon type={result.objectType} />
              <div>
                <h3 className="font-semibold text-gray-900">
                  {result.objectName || 'Unknown'}
                </h3>
                <p className="text-sm text-gray-500">
                  {[result.databaseName, result.schemaName]
                    .filter(Boolean)
                    .join('.')}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <ScoreBadge score={result.score.fusedScore} />
              {result.piiInfo?.isPii && (
                <span className="px-2 py-1 text-xs font-medium bg-red-100 text-red-700 rounded">
                  PII
                </span>
              )}
            </div>
          </div>

          {/* Description */}
          {(result.businessPurpose || result.description) && (
            <p className="mt-3 text-sm text-gray-600 line-clamp-2">
              {result.businessPurpose || result.description}
            </p>
          )}

          {/* Metadata Tags */}
          <div className="mt-3 flex flex-wrap gap-2">
            {result.objectType && (
              <span className="px-2 py-1 text-xs bg-gray-100 text-gray-600 rounded">
                {result.objectType}
              </span>
            )}
            {result.category && (
              <span className="px-2 py-1 text-xs bg-blue-100 text-blue-700 rounded">
                {result.category}
              </span>
            )}
            {result.dataClassification && (
              <span className="px-2 py-1 text-xs bg-purple-100 text-purple-700 rounded">
                {result.dataClassification}
              </span>
            )}
            {result.matchedTerms && result.matchedTerms.length > 0 && (
              <span className="px-2 py-1 text-xs bg-yellow-100 text-yellow-700 rounded">
                Matched: {result.matchedTerms.join(', ')}
              </span>
            )}
          </div>

          {/* Lineage Summary */}
          {result.lineage && (
            <div className="mt-3 pt-3 border-t border-gray-100 flex gap-4 text-xs text-gray-500">
              <span>
                Upstream: {result.lineage.upstreamCount}
              </span>
              <span>
                Downstream: {result.lineage.downstreamCount}
              </span>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

// Helper component for object type icons
const ObjectTypeIcon: React.FC<{ type: string | null }> = ({ type }) => {
  const iconClass = 'w-8 h-8 rounded flex items-center justify-center text-white text-sm font-bold';

  switch (type?.toLowerCase()) {
    case 'table':
      return <div className={`${iconClass} bg-blue-500`}>T</div>;
    case 'column':
      return <div className={`${iconClass} bg-green-500`}>C</div>;
    case 'storedprocedure':
    case 'procedure':
      return <div className={`${iconClass} bg-purple-500`}>P</div>;
    case 'view':
      return <div className={`${iconClass} bg-orange-500`}>V</div>;
    case 'function':
      return <div className={`${iconClass} bg-pink-500`}>F</div>;
    default:
      return <div className={`${iconClass} bg-gray-500`}>?</div>;
  }
};

// Score badge component
const ScoreBadge: React.FC<{ score: number }> = ({ score }) => {
  const percentage = Math.round(score * 100);
  let colorClass = 'bg-gray-100 text-gray-600';

  if (percentage >= 80) {
    colorClass = 'bg-green-100 text-green-700';
  } else if (percentage >= 60) {
    colorClass = 'bg-teal-100 text-teal-700';
  } else if (percentage >= 40) {
    colorClass = 'bg-yellow-100 text-yellow-700';
  }

  return (
    <span className={`px-2 py-1 text-xs font-medium rounded ${colorClass}`}>
      {percentage}%
    </span>
  );
};

export default SearchResultsList;
