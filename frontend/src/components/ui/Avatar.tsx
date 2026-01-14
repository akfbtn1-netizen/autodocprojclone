import { forwardRef, type HTMLAttributes } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn, getInitials } from '@/lib/utils';

const avatarVariants = cva(
  'relative inline-flex items-center justify-center font-medium uppercase overflow-hidden',
  {
    variants: {
      size: {
        xs: 'h-6 w-6 text-2xs',
        sm: 'h-8 w-8 text-xs',
        md: 'h-10 w-10 text-sm',
        lg: 'h-12 w-12 text-base',
        xl: 'h-16 w-16 text-lg',
        '2xl': 'h-20 w-20 text-xl',
      },
      shape: {
        circle: 'rounded-full',
        square: 'rounded-xl',
      },
    },
    defaultVariants: {
      size: 'md',
      shape: 'circle',
    },
  }
);

export interface AvatarProps
  extends HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof avatarVariants> {
  src?: string;
  alt?: string;
  name?: string;
  fallbackColor?: string;
  online?: boolean;
}

// Generate consistent color from name
function getColorFromName(name: string): string {
  const colors = [
    'bg-red-100 text-red-600',
    'bg-orange-100 text-orange-600',
    'bg-amber-100 text-amber-600',
    'bg-yellow-100 text-yellow-600',
    'bg-lime-100 text-lime-600',
    'bg-green-100 text-green-600',
    'bg-emerald-100 text-emerald-600',
    'bg-teal-100 text-teal-600',
    'bg-cyan-100 text-cyan-600',
    'bg-sky-100 text-sky-600',
    'bg-blue-100 text-blue-600',
    'bg-indigo-100 text-indigo-600',
    'bg-violet-100 text-violet-600',
    'bg-purple-100 text-purple-600',
    'bg-fuchsia-100 text-fuchsia-600',
    'bg-pink-100 text-pink-600',
  ];
  
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  
  return colors[Math.abs(hash) % colors.length] ?? colors[0];
}

const Avatar = forwardRef<HTMLDivElement, AvatarProps>(
  (
    {
      className,
      size,
      shape,
      src,
      alt,
      name,
      fallbackColor,
      online,
      ...props
    },
    ref
  ) => {
    const initials = name ? getInitials(name) : '?';
    const colorClass = fallbackColor ?? (name ? getColorFromName(name) : 'bg-surface-100 text-surface-500');

    return (
      <div className="relative inline-block">
        <div
          ref={ref}
          className={cn(
            avatarVariants({ size, shape }),
            !src && colorClass,
            className
          )}
          {...props}
        >
          {src ? (
            <img
              src={src}
              alt={alt ?? name ?? 'Avatar'}
              className="h-full w-full object-cover"
              onError={(e) => {
                // Hide image on error to show fallback
                e.currentTarget.style.display = 'none';
              }}
            />
          ) : (
            <span>{initials}</span>
          )}
        </div>

        {online !== undefined && (
          <span
            className={cn(
              'absolute bottom-0 right-0 block rounded-full ring-2 ring-white',
              size === 'xs' && 'h-1.5 w-1.5',
              size === 'sm' && 'h-2 w-2',
              size === 'md' && 'h-2.5 w-2.5',
              size === 'lg' && 'h-3 w-3',
              size === 'xl' && 'h-3.5 w-3.5',
              size === '2xl' && 'h-4 w-4',
              online ? 'bg-green-500' : 'bg-surface-300'
            )}
          />
        )}
      </div>
    );
  }
);

Avatar.displayName = 'Avatar';

// Avatar Group
interface AvatarGroupProps {
  children: React.ReactNode;
  max?: number;
  size?: AvatarProps['size'];
}

export function AvatarGroup({ children, max = 4, size = 'md' }: AvatarGroupProps) {
  const avatars = Array.isArray(children) ? children : [children];
  const visibleAvatars = avatars.slice(0, max);
  const remaining = avatars.length - max;

  return (
    <div className="flex -space-x-2">
      {visibleAvatars.map((avatar, index) => (
        <div
          key={index}
          className="relative ring-2 ring-white rounded-full"
          style={{ zIndex: visibleAvatars.length - index }}
        >
          {avatar}
        </div>
      ))}
      {remaining > 0 && (
        <div
          className={cn(
            avatarVariants({ size, shape: 'circle' }),
            'ring-2 ring-white bg-surface-100 text-surface-600'
          )}
          style={{ zIndex: 0 }}
        >
          <span>+{remaining}</span>
        </div>
      )}
    </div>
  );
}

export { Avatar, avatarVariants };
