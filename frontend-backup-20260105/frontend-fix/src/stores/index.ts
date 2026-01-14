// Zustand Stores - Global state management
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { Document, WorkflowStats, ApprovalRequest, Notification, Agent, SearchFilters, UserSettings } from '@/types';

// Workflow Store
interface WorkflowState {
  documents: Document[];
  selectedDocument: Document | null;
  isLoadingDocuments: boolean;
  stats: WorkflowStats;
  pendingApprovals: ApprovalRequest[];
  agents: Agent[];
  setDocuments: (documents: Document[]) => void;
  addDocument: (document: Document) => void;
  updateDocument: (id: string, updates: Partial<Document>) => void;
  removeDocument: (id: string) => void;
  selectDocument: (document: Document | null) => void;
  setStats: (stats: WorkflowStats) => void;
  setPendingApprovals: (approvals: ApprovalRequest[]) => void;
  updateApprovalStatus: (id: string, status: string) => void;
  setAgents: (agents: Agent[]) => void;
  updateAgentStatus: (agentType: string, status: string) => void;
  setLoading: (loading: boolean) => void;
}

export const useWorkflowStore = create<WorkflowState>((set) => ({
  documents: [],
  selectedDocument: null,
  isLoadingDocuments: false,
  stats: { totalDocuments: 0, pendingApprovals: 0, approvedToday: 0, rejectedToday: 0, avgProcessingTime: '0m', completionRate: 0, aiEnhancedCount: 0, piiDocumentsCount: 0 },
  pendingApprovals: [],
  agents: [],
  setDocuments: (documents) => set({ documents }),
  addDocument: (document) => set((state) => ({ documents: [document, ...state.documents] })),
  updateDocument: (id, updates) => set((state) => ({ documents: state.documents.map((doc) => doc.id === id ? { ...doc, ...updates } : doc), selectedDocument: state.selectedDocument?.id === id ? { ...state.selectedDocument, ...updates } : state.selectedDocument })),
  removeDocument: (id) => set((state) => ({ documents: state.documents.filter((doc) => doc.id !== id), selectedDocument: state.selectedDocument?.id === id ? null : state.selectedDocument })),
  selectDocument: (document) => set({ selectedDocument: document }),
  setStats: (stats) => set({ stats }),
  setPendingApprovals: (approvals) => set({ pendingApprovals: approvals }),
  updateApprovalStatus: (id, status) => set((state) => ({ pendingApprovals: state.pendingApprovals.map((a) => a.id === id ? { ...a, status: status as any } : a) })),
  setAgents: (agents) => set({ agents }),
  updateAgentStatus: (agentType, status) => set((state) => ({ agents: state.agents.map((a) => a.type === agentType ? { ...a, status: status as any } : a) })),
  setLoading: (loading) => set({ isLoadingDocuments: loading }),
}));

// UI Store (persisted)
interface UIState {
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  theme: 'light' | 'dark' | 'system';
  notifications: Notification[];
  unreadCount: number;
  activeModal: string | null;
  modalData: Record<string, unknown> | null;
  searchFilters: SearchFilters;
  recentSearches: string[];
  documentViewMode: 'grid' | 'list';
  showMetadataPanel: boolean;
  toggleSidebar: () => void;
  collapseSidebar: (collapsed: boolean) => void;
  setTheme: (theme: 'light' | 'dark' | 'system') => void;
  addNotification: (notification: Notification) => void;
  markNotificationRead: (id: string) => void;
  markAllNotificationsRead: () => void;
  removeNotification: (id: string) => void;
  clearNotifications: () => void;
  openModal: (modalId: string, data?: Record<string, unknown>) => void;
  closeModal: () => void;
  setSearchFilters: (filters: SearchFilters) => void;
  addRecentSearch: (query: string) => void;
  setDocumentViewMode: (mode: 'grid' | 'list') => void;
  toggleMetadataPanel: () => void;
}

export const useUIStore = create<UIState>()(
  persist(
    (set) => ({
      sidebarOpen: true,
      sidebarCollapsed: false,
      theme: 'system',
      notifications: [],
      unreadCount: 0,
      activeModal: null,
      modalData: null,
      searchFilters: {},
      recentSearches: [],
      documentViewMode: 'list',
      showMetadataPanel: true,
      toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
      collapseSidebar: (collapsed) => set({ sidebarCollapsed: collapsed }),
      setTheme: (theme) => set({ theme }),
      addNotification: (notification) => set((state) => ({ notifications: [notification, ...state.notifications].slice(0, 100), unreadCount: notification.read ? state.unreadCount : state.unreadCount + 1 })),
      markNotificationRead: (id) => set((state) => ({ notifications: state.notifications.map((n) => n.id === id ? { ...n, read: true } : n), unreadCount: state.notifications.find(n => n.id === id && !n.read) ? Math.max(0, state.unreadCount - 1) : state.unreadCount })),
      markAllNotificationsRead: () => set((state) => ({ notifications: state.notifications.map((n) => ({ ...n, read: true })), unreadCount: 0 })),
      removeNotification: (id) => set((state) => ({ notifications: state.notifications.filter((n) => n.id !== id), unreadCount: state.notifications.find(n => n.id === id && !n.read) ? Math.max(0, state.unreadCount - 1) : state.unreadCount })),
      clearNotifications: () => set({ notifications: [], unreadCount: 0 }),
      openModal: (modalId, data) => set({ activeModal: modalId, modalData: data ?? null }),
      closeModal: () => set({ activeModal: null, modalData: null }),
      setSearchFilters: (filters) => set({ searchFilters: filters }),
      addRecentSearch: (query) => set((state) => ({ recentSearches: [query, ...state.recentSearches.filter((q) => q !== query)].slice(0, 10) })),
      setDocumentViewMode: (mode) => set({ documentViewMode: mode }),
      toggleMetadataPanel: () => set((state) => ({ showMetadataPanel: !state.showMetadataPanel })),
    }),
    { name: 'edp-ui-storage', partialize: (state) => ({ theme: state.theme, sidebarCollapsed: state.sidebarCollapsed, recentSearches: state.recentSearches, documentViewMode: state.documentViewMode, showMetadataPanel: state.showMetadataPanel }) }
  )
);

export type { WorkflowState, UIState };
