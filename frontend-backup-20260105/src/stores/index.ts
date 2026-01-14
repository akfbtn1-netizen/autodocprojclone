import { create } from 'zustand';
import type { Document, WorkflowStats, ApprovalRequest, Notification } from '@/types';

interface WorkflowState {
  // Documents
  documents: Document[];
  selectedDocument: Document | null;
  isLoadingDocuments: boolean;
  
  // Workflow Stats
  stats: WorkflowStats;
  
  // Approvals
  pendingApprovals: ApprovalRequest[];
  
  // Actions
  setDocuments: (documents: Document[]) => void;
  addDocument: (document: Document) => void;
  updateDocument: (id: string, updates: Partial<Document>) => void;
  removeDocument: (id: string) => void;
  selectDocument: (document: Document | null) => void;
  setStats: (stats: WorkflowStats) => void;
  setPendingApprovals: (approvals: ApprovalRequest[]) => void;
  setLoading: (loading: boolean) => void;
}

export const useWorkflowStore = create<WorkflowState>((set) => ({
  // Initial state
  documents: [],
  selectedDocument: null,
  isLoadingDocuments: false,
  stats: {
    totalDocuments: 0,
    pendingApprovals: 0,
    approvedToday: 0,
    avgProcessingTime: '0m',
    completionRate: 0,
  },
  pendingApprovals: [],

  // Actions
  setDocuments: (documents) => set({ documents }),
  
  addDocument: (document) =>
    set((state) => ({ documents: [...state.documents, document] })),
  
  updateDocument: (id, updates) =>
    set((state) => ({
      documents: state.documents.map((doc) =>
        doc.id === id ? { ...doc, ...updates } : doc
      ),
      selectedDocument:
        state.selectedDocument?.id === id
          ? { ...state.selectedDocument, ...updates }
          : state.selectedDocument,
    })),
  
  removeDocument: (id) =>
    set((state) => ({
      documents: state.documents.filter((doc) => doc.id !== id),
      selectedDocument:
        state.selectedDocument?.id === id ? null : state.selectedDocument,
    })),
  
  selectDocument: (document) => set({ selectedDocument: document }),
  
  setStats: (stats) => set({ stats }),
  
  setPendingApprovals: (approvals) => set({ pendingApprovals: approvals }),
  
  setLoading: (loading) => set({ isLoadingDocuments: loading }),
}));

// UI Store for global UI state
interface UIState {
  // Sidebar
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  
  // Theme
  theme: 'light' | 'dark' | 'system';
  
  // Notifications
  notifications: Notification[];
  unreadCount: number;
  
  // Modal state
  activeModal: string | null;
  modalData: Record<string, unknown> | null;
  
  // Actions
  toggleSidebar: () => void;
  collapseSidebar: (collapsed: boolean) => void;
  setTheme: (theme: 'light' | 'dark' | 'system') => void;
  addNotification: (notification: Notification) => void;
  markNotificationRead: (id: string) => void;
  markAllNotificationsRead: () => void;
  removeNotification: (id: string) => void;
  openModal: (modalId: string, data?: Record<string, unknown>) => void;
  closeModal: () => void;
}

export const useUIStore = create<UIState>((set) => ({
  // Initial state
  sidebarOpen: true,
  sidebarCollapsed: false,
  theme: 'light',
  notifications: [],
  unreadCount: 0,
  activeModal: null,
  modalData: null,

  // Actions
  toggleSidebar: () => set((state) => ({ sidebarOpen: !state.sidebarOpen })),
  
  collapseSidebar: (collapsed) => set({ sidebarCollapsed: collapsed }),
  
  setTheme: (theme) => set({ theme }),
  
  addNotification: (notification) =>
    set((state) => ({
      notifications: [notification, ...state.notifications],
      unreadCount: notification.read ? state.unreadCount : state.unreadCount + 1,
    })),
  
  markNotificationRead: (id) =>
    set((state) => ({
      notifications: state.notifications.map((n) =>
        n.id === id ? { ...n, read: true } : n
      ),
      unreadCount: Math.max(
        0,
        state.unreadCount - (state.notifications.find((n) => n.id === id && !n.read) ? 1 : 0)
      ),
    })),
  
  markAllNotificationsRead: () =>
    set((state) => ({
      notifications: state.notifications.map((n) => ({ ...n, read: true })),
      unreadCount: 0,
    })),
  
  removeNotification: (id) =>
    set((state) => ({
      notifications: state.notifications.filter((n) => n.id !== id),
      unreadCount: Math.max(
        0,
        state.unreadCount - (state.notifications.find((n) => n.id === id && !n.read) ? 1 : 0)
      ),
    })),
  
  openModal: (modalId, data) => set({ activeModal: modalId, modalData: data ?? null }),
  
  closeModal: () => set({ activeModal: null, modalData: null }),
}));

// Export combined type for convenience
export type { WorkflowState, UIState };
