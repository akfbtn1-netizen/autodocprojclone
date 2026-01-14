import { useState, useRef, useEffect, type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from './Button';

interface DropdownProps {
  trigger: ReactNode;
  children: ReactNode;
  align?: 'left' | 'right';
  width?: 'auto' | 'trigger' | number;
}

export function Dropdown({
  trigger,
  children,
  align = 'left',
  width = 'auto',
}: DropdownProps) {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLDivElement>(null);

  // Close on click outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Close on escape
  useEffect(() => {
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsOpen(false);
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, []);

  const getWidth = () => {
    if (width === 'auto') return undefined;
    if (width === 'trigger') return triggerRef.current?.offsetWidth;
    return width;
  };

  return (
    <div ref={dropdownRef} className="relative inline-block">
      <div ref={triggerRef} onClick={() => setIsOpen(!isOpen)}>
        {trigger}
      </div>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, y: -8, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -8, scale: 0.96 }}
            transition={{ duration: 0.15, ease: 'easeOut' }}
            className={cn(
              'absolute z-50 mt-2 min-w-[180px] rounded-xl bg-white p-1.5 shadow-elevated border border-surface-100',
              align === 'left' ? 'left-0' : 'right-0'
            )}
            style={{ width: getWidth() }}
          >
            {children}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// Dropdown Item
interface DropdownItemProps {
  children: ReactNode;
  onClick?: () => void;
  icon?: ReactNode;
  danger?: boolean;
  disabled?: boolean;
}

export function DropdownItem({
  children,
  onClick,
  icon,
  danger,
  disabled,
}: DropdownItemProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={cn(
        'flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-left text-sm transition-colors',
        danger
          ? 'text-red-600 hover:bg-red-50'
          : 'text-surface-700 hover:bg-surface-50',
        disabled && 'opacity-50 cursor-not-allowed hover:bg-transparent'
      )}
    >
      {icon && <span className="w-4 h-4">{icon}</span>}
      {children}
    </button>
  );
}

// Dropdown Separator
export function DropdownSeparator() {
  return <div className="my-1 h-px bg-surface-100" />;
}

// Select component
interface SelectOption {
  value: string;
  label: string;
}

interface SelectProps {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
}

export function Select({
  options,
  value,
  onChange,
  placeholder = 'Select...',
  disabled,
  className,
}: SelectProps) {
  const selectedOption = options.find((opt) => opt.value === value);

  return (
    <Dropdown
      width="trigger"
      trigger={
        <Button
          variant="secondary"
          className={cn('justify-between w-full', className)}
          disabled={disabled}
        >
          <span className={!selectedOption ? 'text-surface-400' : undefined}>
            {selectedOption?.label ?? placeholder}
          </span>
          <ChevronDown className="h-4 w-4 text-surface-400" />
        </Button>
      }
    >
      {options.map((option) => (
        <DropdownItem
          key={option.value}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </DropdownItem>
      ))}
    </Dropdown>
  );
}
