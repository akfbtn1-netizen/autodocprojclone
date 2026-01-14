import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Bell,
  Search,
  Settings,
  LogOut,
  User,
  Moon,
  Sun,
  ChevronDown,
  Menu,
  X,
} from 'lucide-react';
import { cn, formatRelativeTime } from '@/lib/utils';
import {
  Button,
  Avatar,
  Badge,
  Dropdown,
  DropdownItem,
  DropdownSeparator,
  Input,
} from '@/components/ui';
import { useUIStore } from '@/stores';

interface HeaderProps {
  onMenuClick?: () => void;
}

export function Header({ onMenuClick }: HeaderProps) {
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const { notifications, unreadCount, markAllNotificationsRead } = useUIStore();

  return (
    <header className="sticky top-0 z-30 h-16 bg-white/80 backdrop-blur-xl border-b border-surface-200/50">
      <div className="flex items-center justify-between h-full px-4 lg:px-6">
        {/* Left section */}
        <div className="flex items-center gap-4">
          {/* Mobile menu button */}
          <Button
            variant="ghost"
            size="icon"
            className="lg:hidden"
            onClick={onMenuClick}
          >
            <Menu className="w-5 h-5" />
          </Button>

          {/* Search */}
          <div className="hidden sm:block relative">
            <Input
              placeholder="Search documents..."
              leftIcon={<Search className="w-4 h-4" />}
              className="w-64 lg:w-80"
            />
            <kbd className="absolute right-3 top-1/2 -translate-y-1/2 hidden lg:inline-flex items-center gap-1 px-1.5 py-0.5 text-2xs text-surface-400 bg-surface-100 rounded border border-surface-200">
              ⌘K
            </kbd>
          </div>

          {/* Mobile search button */}
          <Button
            variant="ghost"
            size="icon"
            className="sm:hidden"
            onClick={() => setIsSearchOpen(!isSearchOpen)}
          >
            <Search className="w-5 h-5" />
          </Button>
        </div>

        {/* Right section */}
        <div className="flex items-center gap-2">
          {/* Notifications */}
          <Dropdown
            align="right"
            trigger={
              <Button variant="ghost" size="icon" className="relative">
                <Bell className="w-5 h-5" />
                {unreadCount > 0 && (
                  <span className="absolute top-1.5 right-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-2xs font-bold text-white">
                    {unreadCount > 9 ? '9+' : unreadCount}
                  </span>
                )}
              </Button>
            }
          >
            <div className="w-80">
              <div className="flex items-center justify-between px-3 py-2 border-b border-surface-100">
                <h3 className="font-semibold text-sm">Notifications</h3>
                {unreadCount > 0 && (
                  <button
                    onClick={markAllNotificationsRead}
                    className="text-xs text-brand-600 hover:underline"
                  >
                    Mark all read
                  </button>
                )}
              </div>
              {notifications.length === 0 ? (
                <div className="p-6 text-center text-sm text-surface-500">
                  No notifications
                </div>
              ) : (
                <div className="max-h-80 overflow-y-auto py-2">
                  {notifications.slice(0, 5).map((notification) => (
                    <button
                      key={notification.id}
                      className={cn(
                        'flex items-start gap-3 w-full px-3 py-2 hover:bg-surface-50 text-left',
                        !notification.read && 'bg-brand-50/50'
                      )}
                    >
                      <div
                        className={cn(
                          'w-2 h-2 mt-1.5 rounded-full',
                          notification.read ? 'bg-surface-200' : 'bg-brand-500'
                        )}
                      />
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-surface-800 truncate">
                          {notification.title}
                        </p>
                        <p className="text-xs text-surface-500 truncate">
                          {notification.message}
                        </p>
                        <p className="text-2xs text-surface-400 mt-0.5">
                          {formatRelativeTime(notification.timestamp)}
                        </p>
                      </div>
                    </button>
                  ))}
                </div>
              )}
              {notifications.length > 5 && (
                <div className="px-3 py-2 border-t border-surface-100">
                  <Button variant="ghost" size="sm" className="w-full">
                    View all notifications
                  </Button>
                </div>
              )}
            </div>
          </Dropdown>

          {/* Theme toggle */}
          <Button variant="ghost" size="icon">
            <Sun className="w-5 h-5" />
          </Button>

          {/* User menu */}
          <Dropdown
            align="right"
            trigger={
              <button className="flex items-center gap-2 p-1.5 rounded-xl hover:bg-surface-50 transition-colors">
                <Avatar name="Alex Kirby" size="sm" />
                <span className="hidden lg:block text-sm font-medium text-surface-700">
                  Alex
                </span>
                <ChevronDown className="hidden lg:block w-4 h-4 text-surface-400" />
              </button>
            }
          >
            <div className="px-3 py-2 border-b border-surface-100">
              <p className="text-sm font-medium text-surface-900">Alex Kirby</p>
              <p className="text-xs text-surface-500">alex.kirby@company.com</p>
            </div>
            <div className="py-1">
              <DropdownItem icon={<User className="w-4 h-4" />}>
                Profile
              </DropdownItem>
              <DropdownItem icon={<Settings className="w-4 h-4" />}>
                Settings
              </DropdownItem>
            </div>
            <DropdownSeparator />
            <DropdownItem icon={<LogOut className="w-4 h-4" />} danger>
              Sign out
            </DropdownItem>
          </Dropdown>
        </div>
      </div>

      {/* Mobile search overlay */}
      <AnimatePresence>
        {isSearchOpen && (
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            className="absolute inset-x-0 top-full p-4 bg-white border-b border-surface-200 shadow-lg sm:hidden"
          >
            <div className="relative">
              <Input
                placeholder="Search documents..."
                leftIcon={<Search className="w-4 h-4" />}
                autoFocus
              />
              <Button
                variant="ghost"
                size="icon-sm"
                className="absolute right-2 top-1/2 -translate-y-1/2"
                onClick={() => setIsSearchOpen(false)}
              >
                <X className="w-4 h-4" />
              </Button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  );
}
