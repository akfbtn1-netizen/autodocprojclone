import { forwardRef, type HTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const badgeVariants = cva(
  'inline-flex items-center gap-1.5 rounded-full text-xs font-semibold transition-colors',
  {
    variants: {
      variant: {
        default: 'bg-surface-100 text-surface-700 border border-surface-200',
        draft: 'bg-status-draft-bg text-status-draft-text border border-status-draft-border/30',
        pending: 'bg-status-pending-bg text-status-pending-text border border-status-pending-border/30',
        review: 'bg-status-review-bg text-status-review-text border border-status-review-border/30',
        approved: 'bg-status-approved-bg text-status-approved-text border border-status-approved-border/30',
        completed: 'bg-status-completed-bg text-status-completed-text border border-status-completed-border/30',
        rejected: 'bg-status-rejected-bg text-status-rejected-text border border-status-rejected-border/30',
        brand: 'bg-brand-100 text-brand-700 border border-brand-200',
        success: 'bg-green-100 text-green-700 border border-green-200',
        warning: 'bg-yellow-100 text-yellow-700 border border-yellow-200',
        danger: 'bg-red-100 text-red-700 border border-red-200',
        info: 'bg-blue-100 text-blue-700 border border-blue-200',
      },
      size: {
        sm: 'px-2 py-0.5 text-2xs',
        md: 'px-2.5 py-1 text-xs',
        lg: 'px-3 py-1.5 text-sm',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'md',
    },
  }
);

export interface BadgeProps
  extends HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {
  dot?: boolean;
  pulse?: boolean;
}

const Badge = forwardRef<HTMLSpanElement, BadgeProps>(
  ({ className, variant, size, dot, pulse, children, ...props }, ref) => {
    return (
      <span
        ref={ref}
        className={cn(badgeVariants({ variant, size, className }))}
        {...props}
      >
        {dot && (
          <span className="relative flex h-2 w-2">
            {pulse && (
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-current opacity-75" />
            )}
            <span className="relative inline-flex h-2 w-2 rounded-full bg-current" />
          </span>
        )}
        {children}
      </span>
    );
  }
);

Badge.displayName = 'Badge';

// Helper to get badge variant from status string
export function getStatusVariant(status: string): BadgeProps['variant'] {
  const statusMap: Record<string, BadgeProps['variant']> = {
    draft: 'draft',
    pending: 'pending',
    review: 'review',
    approved: 'approved',
    completed: 'completed',
    rejected: 'rejected',
  };
  return statusMap[status.toLowerCase()] ?? 'default';
}

export { Badge, badgeVariants };
