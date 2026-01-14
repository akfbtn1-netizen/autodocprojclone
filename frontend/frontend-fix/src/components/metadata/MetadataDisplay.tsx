// ═══════════════════════════════════════════════════════════════════════════
// Metadata Display Component
// Displays MasterIndex metadata (119 columns) in an organized, readable format
// ═══════════════════════════════════════════════════════════════════════════

import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Database,
  Shield,
  Tags,
  Sparkles,
  Building2,
  GitBranch,
  FileCheck,
  AlertTriangle,
  ChevronDown,
  ChevronRight,
  Copy,
  Check,
  ExternalLink,
  Info,
} from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';
import type {
  MasterIndexMetadata,
  BusinessDomain,
  SemanticCategory,
  DataClassification,
  SensitivityLevel,
  PIIType,
  ComplianceTag,
} from '@/types';

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

const classificationColors: Record<DataClassification, string> = {
  Public: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-300',
  Internal: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
  Confidential: 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-300',
  Restricted: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300',
};

const sensitivityColors: Record<SensitivityLevel, string> = {
  None: 'bg-stone-100 text-stone-600',
  Low: 'bg-emerald-100 text-emerald-700',
  Medium: 'bg-amber-100 text-amber-700',
  High: 'bg-orange-100 text-orange-700',
  Critical: 'bg-red-100 text-red-700',
};

// ─────────────────────────────────────────────────────────────────────────────
// Metadata Section Component
// ─────────────────────────────────────────────────────────────────────────────

interface MetadataSectionProps {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  defaultOpen?: boolean;
  badge?: React.ReactNode;
}

function MetadataSection({ title, icon, children, defaultOpen = true, badge }: MetadataSectionProps) {
  const [isOpen, setIsOpen] = useState(defaultOpen);

  return (
    <div className="border-b border-stone-200 dark:border-stone-700 last:border-b-0">
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="flex w-full items-center justify-between px-4 py-3 text-left hover:bg-stone-50 dark:hover:bg-stone-800/50"
      >
        <div className="flex items-center gap-2">
          <span className="text-stone-500 dark:text-stone-400">{icon}</span>
          <span className="font-medium text-stone-900 dark:text-stone-100">{title}</span>
          {badge}
        </div>
        {isOpen ? (
          <ChevronDown className="h-4 w-4 text-stone-400" />
        ) : (
          <ChevronRight className="h-4 w-4 text-stone-400" />
        )}
      </button>
      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="px-4 pb-4">{children}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Field Row Component
// ─────────────────────────────────────────────────────────────────────────────

interface FieldRowProps {
  label: string;
  value: React.ReactNode;
  tooltip?: string;
  copyable?: boolean;
}

function FieldRow({ label, value, tooltip, copyable }: FieldRowProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    if (typeof value === 'string') {
      navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  if (value === null || value === undefined || value === '') return null;

  return (
    <div className="flex items-start justify-between gap-2 py-1.5">
      <div className="flex items-center gap-1">
        <span className="text-sm text-stone-500 dark:text-stone-400">{label}</span>
        {tooltip && (
          <Info className="h-3 w-3 text-stone-400 cursor-help" title={tooltip} />
        )}
      </div>
      <div className="flex items-center gap-1">
        <span className="text-sm font-medium text-stone-900 dark:text-stone-100 text-right">
          {value}
        </span>
        {copyable && typeof value === 'string' && (
          <button
            onClick={handleCopy}
            className="ml-1 p-0.5 text-stone-400 hover:text-stone-600"
          >
            {copied ? <Check className="h-3 w-3 text-emerald-500" /> : <Copy className="h-3 w-3" />}
          </button>
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Tag List Component
// ─────────────────────────────────────────────────────────────────────────────

interface TagListProps {
  tags: string[] | undefined;
  variant?: 'default' | 'brand' | 'warning' | 'danger';
}

function TagList({ tags, variant = 'default' }: TagListProps) {
  if (!tags || tags.length === 0) return <span className="text-sm text-stone-400">None</span>;

  return (
    <div className="flex flex-wrap gap-1">
      {tags.map((tag, index) => (
        <Badge key={index} variant={variant} size="sm">
          {tag}
        </Badge>
      ))}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Quality Score Component
// ─────────────────────────────────────────────────────────────────────────────

interface QualityScoreProps {
  score: number | undefined;
  label?: string;
}

function QualityScore({ score, label = 'Score' }: QualityScoreProps) {
  if (score === undefined) return null;

  const getColor = () => {
    if (score >= 80) return 'text-emerald-600 bg-emerald-500';
    if (score >= 60) return 'text-amber-600 bg-amber-500';
    if (score >= 40) return 'text-orange-600 bg-orange-500';
    return 'text-red-600 bg-red-500';
  };

  const getLabel = () => {
    if (score >= 80) return 'Excellent';
    if (score >= 60) return 'Good';
    if (score >= 40) return 'Fair';
    return 'Poor';
  };

  return (
    <div className="flex items-center gap-2">
      <div className="flex-1 h-2 bg-stone-200 dark:bg-stone-700 rounded-full overflow-hidden">
        <div
          className={cn('h-full rounded-full', getColor().split(' ')[1])}
          style={{ width: `${score}%` }}
        />
      </div>
      <span className={cn('text-sm font-medium', getColor().split(' ')[0])}>
        {score}% ({getLabel()})
      </span>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main Metadata Display Component
// ─────────────────────────────────────────────────────────────────────────────

interface MetadataDisplayProps {
  metadata: MasterIndexMetadata;
  compact?: boolean;
  showAllFields?: boolean;
}

export function MetadataDisplay({ metadata, compact = false, showAllFields = false }: MetadataDisplayProps) {
  const [showAll, setShowAll] = useState(showAllFields);

  // Parse JSON arrays if stored as strings
  const parseArray = (value: unknown): string[] => {
    if (Array.isArray(value)) return value;
    if (typeof value === 'string') {
      try {
        const parsed = JSON.parse(value);
        return Array.isArray(parsed) ? parsed : [];
      } catch {
        return value.split(',').map((s) => s.trim()).filter(Boolean);
      }
    }
    return [];
  };

  const aiTags = parseArray(metadata.aiGeneratedTags);
  const piiTypes = parseArray(metadata.piiTypes);
  const complianceTags = parseArray(metadata.complianceTags);
  const relatedTables = parseArray(metadata.relatedTables);
  const storedProcedures = parseArray(metadata.storedProcedures);

  if (compact) {
    return (
      <div className="space-y-2">
        {/* Quick stats row */}
        <div className="flex flex-wrap gap-2">
          {metadata.businessDomain && (
            <Badge variant="brand" size="sm">
              <Building2 className="mr-1 h-3 w-3" />
              {metadata.businessDomain}
            </Badge>
          )}
          {metadata.semanticCategory && (
            <Badge variant="default" size="sm">
              {metadata.semanticCategory}
            </Badge>
          )}
          {metadata.piiIndicator && (
            <Badge variant="warning" size="sm">
              <Shield className="mr-1 h-3 w-3" />
              PII
            </Badge>
          )}
          {metadata.dataClassification && (
            <span className={cn('px-2 py-0.5 rounded text-xs font-medium', classificationColors[metadata.dataClassification])}>
              {metadata.dataClassification}
            </span>
          )}
        </div>
        {/* Quality score */}
        {metadata.completenessScore !== undefined && (
          <QualityScore score={metadata.completenessScore} label="Completeness" />
        )}
      </div>
    );
  }

  return (
    <Card variant="ghost" className="overflow-hidden">
      {/* Identity Section */}
      <MetadataSection
        title="Identity"
        icon={<Database className="h-4 w-4" />}
        defaultOpen={true}
      >
        <div className="space-y-1">
          <FieldRow label="DocId" value={metadata.docId} copyable />
          <FieldRow label="Document Title" value={metadata.documentTitle} />
          <FieldRow label="Document Type" value={metadata.documentType} />
          <FieldRow label="Version" value={metadata.versionNumber} />
          <FieldRow label="JIRA/CAB" value={metadata.cabNumber || metadata.jiraTicket} copyable />
        </div>
      </MetadataSection>

      {/* Database Location Section */}
      <MetadataSection
        title="Database Location"
        icon={<Database className="h-4 w-4" />}
        defaultOpen={true}
      >
        <div className="space-y-1">
          <FieldRow label="System" value={metadata.systemName} />
          <FieldRow label="Database" value={metadata.databaseName} />
          <FieldRow label="Schema" value={metadata.schemaName} />
          <FieldRow label="Table" value={metadata.tableName} />
          <FieldRow label="Column" value={metadata.columnName} />
          <FieldRow label="Data Type" value={metadata.dataType} />
          <FieldRow label="Nullable" value={metadata.isNullable ? 'Yes' : 'No'} />
        </div>
      </MetadataSection>

      {/* Business Context Section */}
      <MetadataSection
        title="Business Context"
        icon={<Building2 className="h-4 w-4" />}
        badge={
          metadata.businessDomain && (
            <Badge variant="brand" size="sm">{metadata.businessDomain}</Badge>
          )
        }
      >
        <div className="space-y-1">
          <FieldRow label="Business Domain" value={metadata.businessDomain} />
          <FieldRow label="Semantic Category" value={metadata.semanticCategory} />
          <FieldRow label="Business Process" value={metadata.businessProcess} />
          <FieldRow label="Business Owner" value={metadata.businessOwner} />
          <FieldRow label="Technical Owner" value={metadata.technicalOwner} />
          {metadata.businessDefinition && (
            <div className="mt-2">
              <p className="text-sm text-stone-500 mb-1">Business Definition</p>
              <p className="text-sm text-stone-700 dark:text-stone-300 bg-stone-50 dark:bg-stone-800 p-2 rounded">
                {metadata.businessDefinition}
              </p>
            </div>
          )}
        </div>
      </MetadataSection>

      {/* Classification & Compliance Section */}
      <MetadataSection
        title="Classification & Compliance"
        icon={<Shield className="h-4 w-4" />}
        badge={
          metadata.piiIndicator && (
            <Badge variant="warning" size="sm">Contains PII</Badge>
          )
        }
      >
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm text-stone-500">Data Classification</span>
            {metadata.dataClassification && (
              <span className={cn('px-2 py-0.5 rounded text-xs font-medium', classificationColors[metadata.dataClassification])}>
                {metadata.dataClassification}
              </span>
            )}
          </div>
          <div className="flex items-center justify-between">
            <span className="text-sm text-stone-500">Sensitivity Level</span>
            {metadata.sensitivityLevel && (
              <span className={cn('px-2 py-0.5 rounded text-xs font-medium', sensitivityColors[metadata.sensitivityLevel])}>
                {metadata.sensitivityLevel}
              </span>
            )}
          </div>
          <FieldRow
            label="Contains PII"
            value={
              metadata.piiIndicator ? (
                <span className="flex items-center gap-1 text-amber-600">
                  <AlertTriangle className="h-3 w-3" /> Yes
                </span>
              ) : (
                'No'
              )
            }
          />
          {piiTypes.length > 0 && (
            <div>
              <p className="text-sm text-stone-500 mb-1">PII Types</p>
              <TagList tags={piiTypes} variant="warning" />
            </div>
          )}
          {complianceTags.length > 0 && (
            <div>
              <p className="text-sm text-stone-500 mb-1">Compliance Tags</p>
              <TagList tags={complianceTags} variant="danger" />
            </div>
          )}
          <FieldRow label="Retention Policy" value={metadata.retentionPolicy} />
        </div>
      </MetadataSection>

      {/* AI-Generated Metadata Section */}
      <MetadataSection
        title="AI-Generated Metadata"
        icon={<Sparkles className="h-4 w-4" />}
        badge={aiTags.length > 0 && <Badge size="sm">{aiTags.length} tags</Badge>}
      >
        <div className="space-y-2">
          {aiTags.length > 0 && (
            <div>
              <p className="text-sm text-stone-500 mb-1">AI-Generated Tags</p>
              <TagList tags={aiTags} variant="brand" />
            </div>
          )}
          {metadata.keywords && (
            <div>
              <p className="text-sm text-stone-500 mb-1">Keywords</p>
              <p className="text-sm text-stone-700 dark:text-stone-300">
                {metadata.keywords}
              </p>
            </div>
          )}
        </div>
      </MetadataSection>

      {/* Relationships Section */}
      <MetadataSection
        title="Relationships & Dependencies"
        icon={<GitBranch className="h-4 w-4" />}
        defaultOpen={false}
      >
        <div className="space-y-2">
          {relatedTables.length > 0 && (
            <div>
              <p className="text-sm text-stone-500 mb-1">Related Tables</p>
              <TagList tags={relatedTables} />
            </div>
          )}
          {storedProcedures.length > 0 && (
            <div>
              <p className="text-sm text-stone-500 mb-1">Stored Procedures</p>
              <TagList tags={storedProcedures} />
            </div>
          )}
          <FieldRow label="Upstream Systems" value={metadata.upstreamSystems?.join(', ')} />
          <FieldRow label="Downstream Systems" value={metadata.downstreamSystems?.join(', ')} />
        </div>
      </MetadataSection>

      {/* Quality Metrics Section */}
      <MetadataSection
        title="Quality Metrics"
        icon={<FileCheck className="h-4 w-4" />}
        defaultOpen={true}
      >
        <div className="space-y-3">
          <div>
            <p className="text-sm text-stone-500 mb-1">Completeness Score</p>
            <QualityScore score={metadata.completenessScore} />
          </div>
          {metadata.qualityScore !== undefined && (
            <div>
              <p className="text-sm text-stone-500 mb-1">Quality Score</p>
              <QualityScore score={metadata.qualityScore} />
            </div>
          )}
          <FieldRow label="Validation Status" value={metadata.validationStatus} />
          <FieldRow label="Last Validated" value={metadata.lastValidated} />
        </div>
      </MetadataSection>

      {/* Show More Toggle */}
      {!showAll && (
        <div className="px-4 py-3 border-t border-stone-200 dark:border-stone-700">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setShowAll(true)}
            className="w-full"
          >
            Show All Fields
          </Button>
        </div>
      )}
    </Card>
  );
}

export default MetadataDisplay;
