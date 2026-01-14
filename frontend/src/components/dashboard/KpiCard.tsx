import { motion } from 'framer-motion';
import {
  ArrowUpRight,
  ArrowDownRight,
  FileText,
  Clock,
  CheckCircle2,
  AlertCircle,
  TrendingUp,
  Sparkles,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Card } from '@/components/ui';

interface KpiCardProps {
  title: string;
  value: string | number;
  change?: number;
  changeLabel?: string;
  icon: 'documents' | 'pending' | 'approved' | 'time' | 'rate' | 'ai';
  variant?: 'default' | 'brand' | 'success' | 'warning' | 'danger';
  delay?: number;
}

const iconMap = {
  documents: FileText,
  pending: Clock,
  approved: CheckCircle2,
  time: AlertCircle,
  rate: TrendingUp,
  ai: Sparkles,
};

const variantStyles = {
  default: {
    icon: 'bg-surface-100 text-surface-600',
    trend: 'text-surface-500',
  },
  brand: {
    icon: 'bg-brand-100 text-brand-600',
    trend: 'text-brand-600',
  },
  success: {
    icon: 'bg-green-100 text-green-600',
    trend: 'text-green-600',
  },
  warning: {
    icon: 'bg-yellow-100 text-yellow-600',
    trend: 'text-yellow-600',
  },
  danger: {
    icon: 'bg-red-100 text-red-600',
    trend: 'text-red-600',
  },
};

export function KpiCard({
  title,
  value,
  change,
  changeLabel = 'vs last week',
  icon,
  variant = 'default',
  delay = 0,
}: KpiCardProps) {
  const Icon = iconMap[icon];
  const styles = variantStyles[variant];
  const isPositive = change && change > 0;
  const isNegative = change && change < 0;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, delay: delay * 0.1 }}
    >
      <Card className="group hover:shadow-card-hover transition-all duration-300">
        <div className="flex items-start justify-between">
          <div className="space-y-3">
            <p className="text-sm font-medium text-surface-500">{title}</p>
            <p className="font-display text-3xl font-bold text-surface-900 tracking-tight">
              {value}
            </p>

            {change !== undefined && (
              <div className="flex items-center gap-1.5">
                {isPositive && (
                  <span className="flex items-center text-green-600 text-sm font-medium">
                    <ArrowUpRight className="w-4 h-4" />+{change}%
                  </span>
                )}
                {isNegative && (
                  <span className="flex items-center text-red-600 text-sm font-medium">
                    <ArrowDownRight className="w-4 h-4" />
                    {change}%
                  </span>
                )}
                {change === 0 && (
                  <span className="text-surface-400 text-sm">No change</span>
                )}
                <span className="text-xs text-surface-400">{changeLabel}</span>
              </div>
            )}
          </div>

          <div
            className={cn(
              'flex items-center justify-center w-12 h-12 rounded-xl transition-transform duration-300 group-hover:scale-110',
              styles.icon
            )}
          >
            <Icon className="w-6 h-6" />
          </div>
        </div>
      </Card>
    </motion.div>
  );
}

// Grid of KPI cards
interface KpiGridProps {
  children: React.ReactNode;
}

export function KpiGrid({ children }: KpiGridProps) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      {children}
    </div>
  );
}
