import { useState } from 'react';
import { motion } from 'framer-motion';
import {
  User,
  Bell,
  Shield,
  Palette,
  Database,
  Key,
  Mail,
  Globe,
  Moon,
  Sun,
  Monitor,
  Save,
  RefreshCw,
  CheckCircle2,
  AlertCircle,
} from 'lucide-react';
import {
  Button,
  Input,
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from '@/components/ui';
import { Select } from '@/components/ui/Dropdown';
import { Avatar } from '@/components/ui/Avatar';
import { cn } from '@/lib/utils';
import { useUIStore } from '@/stores';

const tabs = [
  { id: 'profile', label: 'Profile', icon: User },
  { id: 'notifications', label: 'Notifications', icon: Bell },
  { id: 'appearance', label: 'Appearance', icon: Palette },
  { id: 'security', label: 'Security', icon: Shield },
  { id: 'integrations', label: 'Integrations', icon: Database },
];

const themeOptions = [
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
  { value: 'system', label: 'System', icon: Monitor },
];

const timezoneOptions = [
  { value: 'America/New_York', label: 'Eastern Time (ET)' },
  { value: 'America/Chicago', label: 'Central Time (CT)' },
  { value: 'America/Denver', label: 'Mountain Time (MT)' },
  { value: 'America/Los_Angeles', label: 'Pacific Time (PT)' },
  { value: 'UTC', label: 'UTC' },
];

const languageOptions = [
  { value: 'en', label: 'English' },
  { value: 'es', label: 'Spanish' },
  { value: 'fr', label: 'French' },
  { value: 'de', label: 'German' },
];

interface ToggleProps {
  enabled: boolean;
  onChange: (enabled: boolean) => void;
  label: string;
  description?: string;
}

function Toggle({ enabled, onChange, label, description }: ToggleProps) {
  return (
    <div className="flex items-center justify-between py-3">
      <div>
        <p className="text-sm font-medium text-stone-900 dark:text-stone-100">{label}</p>
        {description && (
          <p className="text-xs text-stone-500 dark:text-stone-400 mt-0.5">{description}</p>
        )}
      </div>
      <button
        onClick={() => onChange(!enabled)}
        className={cn(
          'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
          enabled ? 'bg-teal-500' : 'bg-stone-300 dark:bg-stone-600'
        )}
      >
        <span
          className={cn(
            'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
            enabled ? 'translate-x-6' : 'translate-x-1'
          )}
        />
      </button>
    </div>
  );
}

export function Settings() {
  const [activeTab, setActiveTab] = useState('profile');
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const { theme, setTheme } = useUIStore();

  // Profile state
  const [profile, setProfile] = useState({
    name: 'Alex Kirby',
    email: 'alex.kirby@tfic.com',
    role: 'Senior Developer',
    department: 'IT Operations',
    timezone: 'America/Chicago',
    language: 'en',
  });

  // Notification state
  const [notifications, setNotifications] = useState({
    emailApprovals: true,
    emailComments: true,
    emailMentions: true,
    pushApprovals: true,
    pushComments: false,
    pushMentions: true,
    digestFrequency: 'daily',
  });

  // Security state
  const [security, setSecurity] = useState({
    twoFactor: true,
    sessionTimeout: '30',
    loginAlerts: true,
  });

  // Integration state
  const [integrations, setIntegrations] = useState({
    sharepoint: { connected: true, lastSync: '2 hours ago' },
    jira: { connected: true, lastSync: '15 minutes ago' },
    azureAD: { connected: true, lastSync: '1 hour ago' },
    teams: { connected: false, lastSync: null },
  });

  const handleSave = async () => {
    setIsSaving(true);
    await new Promise((resolve) => setTimeout(resolve, 1000));
    setIsSaving(false);
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  const renderTabContent = () => {
    switch (activeTab) {
      case 'profile':
        return (
          <div className="space-y-6">
            <div className="flex items-center gap-6">
              <Avatar name={profile.name} size="2xl" />
              <div>
                <Button variant="outline" size="sm">
                  Change Photo
                </Button>
                <p className="text-xs text-stone-500 dark:text-stone-400 mt-2">
                  JPG, PNG or GIF. Max 2MB.
                </p>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                label="Full Name"
                value={profile.name}
                onChange={(e) => setProfile({ ...profile, name: e.target.value })}
              />
              <Input
                label="Email"
                type="email"
                value={profile.email}
                onChange={(e) => setProfile({ ...profile, email: e.target.value })}
                leftIcon={<Mail className="h-4 w-4" />}
              />
              <Input
                label="Role"
                value={profile.role}
                onChange={(e) => setProfile({ ...profile, role: e.target.value })}
              />
              <Input
                label="Department"
                value={profile.department}
                onChange={(e) => setProfile({ ...profile, department: e.target.value })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-1.5">
                  Timezone
                </label>
                <Select
                  value={profile.timezone}
                  onChange={(value) => setProfile({ ...profile, timezone: value })}
                  options={timezoneOptions}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-stone-700 dark:text-stone-300 mb-1.5">
                  Language
                </label>
                <Select
                  value={profile.language}
                  onChange={(value) => setProfile({ ...profile, language: value })}
                  options={languageOptions}
                />
              </div>
            </div>
          </div>
        );

      case 'notifications':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Email Notifications
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Choose what updates you receive via email
              </p>
              <div className="divide-y divide-stone-100 dark:divide-stone-800">
                <Toggle
                  label="Approval Requests"
                  description="Receive emails when documents need your approval"
                  enabled={notifications.emailApprovals}
                  onChange={(v) => setNotifications({ ...notifications, emailApprovals: v })}
                />
                <Toggle
                  label="Comments"
                  description="Receive emails when someone comments on your documents"
                  enabled={notifications.emailComments}
                  onChange={(v) => setNotifications({ ...notifications, emailComments: v })}
                />
                <Toggle
                  label="Mentions"
                  description="Receive emails when you are mentioned"
                  enabled={notifications.emailMentions}
                  onChange={(v) => setNotifications({ ...notifications, emailMentions: v })}
                />
              </div>
            </div>

            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Push Notifications
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Choose what updates appear as push notifications
              </p>
              <div className="divide-y divide-stone-100 dark:divide-stone-800">
                <Toggle
                  label="Approval Requests"
                  enabled={notifications.pushApprovals}
                  onChange={(v) => setNotifications({ ...notifications, pushApprovals: v })}
                />
                <Toggle
                  label="Comments"
                  enabled={notifications.pushComments}
                  onChange={(v) => setNotifications({ ...notifications, pushComments: v })}
                />
                <Toggle
                  label="Mentions"
                  enabled={notifications.pushMentions}
                  onChange={(v) => setNotifications({ ...notifications, pushMentions: v })}
                />
              </div>
            </div>
          </div>
        );

      case 'appearance':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Theme
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Select your preferred color scheme
              </p>
              <div className="grid grid-cols-3 gap-3">
                {themeOptions.map((option) => {
                  const Icon = option.icon;
                  const isActive = theme === option.value;
                  return (
                    <button
                      key={option.value}
                      onClick={() => setTheme(option.value as 'light' | 'dark' | 'system')}
                      className={cn(
                        'flex flex-col items-center gap-2 p-4 rounded-lg border-2 transition-all',
                        isActive
                          ? 'border-teal-500 bg-teal-50 dark:bg-teal-950/30'
                          : 'border-stone-200 dark:border-stone-700 hover:border-stone-300 dark:hover:border-stone-600'
                      )}
                    >
                      <Icon
                        className={cn(
                          'h-6 w-6',
                          isActive ? 'text-teal-600 dark:text-teal-400' : 'text-stone-500'
                        )}
                      />
                      <span
                        className={cn(
                          'text-sm font-medium',
                          isActive ? 'text-teal-700 dark:text-teal-300' : 'text-stone-700 dark:text-stone-300'
                        )}
                      >
                        {option.label}
                      </span>
                    </button>
                  );
                })}
              </div>
            </div>

            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Interface Density
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Adjust the spacing and sizing of interface elements
              </p>
              <div className="grid grid-cols-3 gap-3">
                {['Compact', 'Comfortable', 'Spacious'].map((density, i) => (
                  <button
                    key={density}
                    className={cn(
                      'p-4 rounded-lg border-2 transition-all text-sm font-medium',
                      i === 1
                        ? 'border-teal-500 bg-teal-50 dark:bg-teal-950/30 text-teal-700 dark:text-teal-300'
                        : 'border-stone-200 dark:border-stone-700 hover:border-stone-300 dark:hover:border-stone-600 text-stone-700 dark:text-stone-300'
                    )}
                  >
                    {density}
                  </button>
                ))}
              </div>
            </div>
          </div>
        );

      case 'security':
        return (
          <div className="space-y-6">
            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Two-Factor Authentication
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Add an extra layer of security to your account
              </p>
              <div className="flex items-center justify-between p-4 bg-stone-50 dark:bg-stone-800/50 rounded-lg">
                <div className="flex items-center gap-3">
                  <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-100 dark:bg-emerald-900/30">
                    <Shield className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-stone-900 dark:text-stone-100">
                      2FA is enabled
                    </p>
                    <p className="text-xs text-stone-500 dark:text-stone-400">
                      Using authenticator app
                    </p>
                  </div>
                </div>
                <Button variant="outline" size="sm">
                  Manage
                </Button>
              </div>
            </div>

            <div className="divide-y divide-stone-100 dark:divide-stone-800">
              <Toggle
                label="Login Alerts"
                description="Get notified when someone logs into your account"
                enabled={security.loginAlerts}
                onChange={(v) => setSecurity({ ...security, loginAlerts: v })}
              />
            </div>

            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Session Management
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Manage your active sessions
              </p>
              <Button variant="outline" leftIcon={<Key className="h-4 w-4" />}>
                View Active Sessions
              </Button>
            </div>

            <div>
              <h3 className="text-sm font-semibold text-stone-900 dark:text-stone-100 mb-1">
                Password
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mb-4">
                Last changed 30 days ago
              </p>
              <Button variant="outline">
                Change Password
              </Button>
            </div>
          </div>
        );

      case 'integrations':
        return (
          <div className="space-y-4">
            {Object.entries(integrations).map(([key, integration]) => (
              <div
                key={key}
                className="flex items-center justify-between p-4 border border-stone-200 dark:border-stone-700 rounded-lg"
              >
                <div className="flex items-center gap-4">
                  <div
                    className={cn(
                      'flex h-12 w-12 items-center justify-center rounded-lg',
                      integration.connected
                        ? 'bg-emerald-100 dark:bg-emerald-900/30'
                        : 'bg-stone-100 dark:bg-stone-800'
                    )}
                  >
                    {key === 'sharepoint' && <Globe className="h-6 w-6 text-blue-600" />}
                    {key === 'jira' && <AlertCircle className="h-6 w-6 text-blue-500" />}
                    {key === 'azureAD' && <Shield className="h-6 w-6 text-sky-600" />}
                    {key === 'teams' && <User className="h-6 w-6 text-purple-600" />}
                  </div>
                  <div>
                    <p className="text-sm font-medium text-stone-900 dark:text-stone-100 capitalize">
                      {key === 'azureAD' ? 'Azure AD' : key}
                    </p>
                    {integration.connected ? (
                      <p className="text-xs text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                        <CheckCircle2 className="h-3 w-3" />
                        Connected · Last synced {integration.lastSync}
                      </p>
                    ) : (
                      <p className="text-xs text-stone-500 dark:text-stone-400">
                        Not connected
                      </p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {integration.connected && (
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      className="text-stone-500 hover:text-stone-700"
                    >
                      <RefreshCw className="h-4 w-4" />
                    </Button>
                  )}
                  <Button variant="outline" size="sm">
                    {integration.connected ? 'Configure' : 'Connect'}
                  </Button>
                </div>
              </div>
            ))}
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-display text-2xl font-semibold text-stone-900 dark:text-stone-100">
          Settings
        </h1>
        <p className="mt-1 text-sm text-stone-500 dark:text-stone-400">
          Manage your account settings and preferences
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-4">
        {/* Sidebar Navigation */}
        <Card variant="ghost" className="lg:col-span-1 h-fit">
          <CardContent className="p-2">
            <nav className="space-y-1">
              {tabs.map((tab) => {
                const Icon = tab.icon;
                const isActive = activeTab === tab.id;
                return (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={cn(
                      'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-teal-50 dark:bg-teal-950/50 text-teal-700 dark:text-teal-300'
                        : 'text-stone-600 dark:text-stone-400 hover:bg-stone-100 dark:hover:bg-stone-800'
                    )}
                  >
                    <Icon className="h-4 w-4" />
                    {tab.label}
                  </button>
                );
              })}
            </nav>
          </CardContent>
        </Card>

        {/* Main Content */}
        <Card variant="elevated" className="lg:col-span-3">
          <CardHeader className="border-b border-stone-200 dark:border-stone-700">
            <CardTitle>
              {tabs.find((t) => t.id === activeTab)?.label}
            </CardTitle>
            <CardDescription>
              {activeTab === 'profile' && 'Update your personal information'}
              {activeTab === 'notifications' && 'Manage how you receive updates'}
              {activeTab === 'appearance' && 'Customize the look and feel'}
              {activeTab === 'security' && 'Keep your account secure'}
              {activeTab === 'integrations' && 'Connect with external services'}
            </CardDescription>
          </CardHeader>
          <CardContent className="p-6">
            <motion.div
              key={activeTab}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.2 }}
            >
              {renderTabContent()}
            </motion.div>
          </CardContent>
        </Card>
      </div>

      {/* Save Button */}
      <div className="flex items-center justify-end gap-3">
        {saveSuccess && (
          <motion.span
            initial={{ opacity: 0, x: 10 }}
            animate={{ opacity: 1, x: 0 }}
            className="flex items-center gap-1.5 text-sm text-emerald-600 dark:text-emerald-400"
          >
            <CheckCircle2 className="h-4 w-4" />
            Settings saved
          </motion.span>
        )}
        <Button variant="outline">Cancel</Button>
        <Button
          variant="primary"
          onClick={handleSave}
          isLoading={isSaving}
          leftIcon={<Save className="h-4 w-4" />}
        >
          Save Changes
        </Button>
      </div>
    </div>
  );
}

export default Settings;
