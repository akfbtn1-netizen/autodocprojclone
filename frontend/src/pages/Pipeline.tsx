// ═══════════════════════════════════════════════════════════════════════════
// Pipeline Page
// End-to-end pipeline visibility dashboard
// ═══════════════════════════════════════════════════════════════════════════

import React from 'react';
import { PipelineView } from '@/components/pipeline/PipelineView';

interface PipelinePageProps {
  onDocumentClick?: (docId: string) => void;
}

export function PipelinePage({ onDocumentClick }: PipelinePageProps) {
  const handleDocumentClick = (docId: string) => {
    if (onDocumentClick) {
      onDocumentClick(docId);
    } else {
      // Default behavior - could navigate to document detail
      console.log('Document clicked:', docId);
    }
  };

  return (
    <div className="min-h-screen bg-stone-50">
      <div className="max-w-[1400px] mx-auto p-6">
        <PipelineView onDocumentClick={handleDocumentClick} />
      </div>
    </div>
  );
}

export default PipelinePage;