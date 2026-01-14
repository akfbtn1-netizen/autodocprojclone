// ═══════════════════════════════════════════════════════════════════════════
// Approval Workflow Zustand Store
// UI state management for approval dashboard
// ═══════════════════════════════════════════════════════════════════════════

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';
import type { Approval, ApprovalFilters, ApprovalStatus } from '@/types/approval';

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

export type ApprovalViewMode = 'cards' | 'table' | 'kanban';

interface ApprovalStoreState {
  // Selection
  selectedApproval: Approval | null;
  selectedApprovalIds: number[];

  // Filters
  filters: ApprovalFilters;

  // View preferences
  viewMode: ApprovalViewMode;
  showFilters: boolean;
  compactMode: boolean;

  // Modals
  isDetailsModalOpen: boolean;
  isRepromptModalOpen: boolean;
  isEditModalOpen: boolean;
  isApproveModalOpen: boolean;
  isRejectModalOpen: boolean;
  isBulkActionModalOpen: boolean;

  // Editing state
  isEditing: boolean;
  editedSections: Map<string, string>;

  // Notifications
  notificationBadgeCount: number;
}

interface ApprovalStoreActions {
  // Selection actions
  setSelectedApproval: (approval: Approval | null) => void;
  toggleApprovalSelection: (id: number) => void;
  selectAllApprovals: (ids: number[]) => void;
  clearSelection: () => void;

  // Filter actions
  setFilters: (filters: ApprovalFilters) => void;
  updateFilter: <K extends keyof ApprovalFilters>(
    key: K,
    value: ApprovalFilters[K]
  ) => void;
  resetFilters: () => void;
  setStatusFilter: (status: ApprovalStatus[]) => void;
  setSearchQuery: (query: string) => void;

  // View actions
  setViewMode: (mode: ApprovalViewMode) => void;
  toggleFilters: () => void;
  toggleCompactMode: () => void;

  // Modal actions
  openDetailsModal: (approval: Approval) => void;
  closeDetailsModal: () => void;
  openRepromptModal: (approval: Approval) => void;
  closeRepromptModal: () => void;
  openEditModal: (approval: Approval) => void;
  closeEditModal: () => void;
  openApproveModal: (approval: Approval) => void;
  closeApproveModal: () => void;
  openRejectModal: (approval: Approval) => void;
  closeRejectModal: () => void;
  openBulkActionModal: () => void;
  closeBulkActionModal: () => void;
  closeAllModals: () => void;

  // Editing actions
  startEditing: () => void;
  stopEditing: () => void;
  updateEditedSection: (sectionName: string, content: string) => void;
  clearEditedSections: () => void;

  // Notification actions
  setNotificationBadgeCount: (count: number) => void;
}

type ApprovalStore = ApprovalStoreState & ApprovalStoreActions;

// ─────────────────────────────────────────────────────────────────────────────
// Default Values
// ─────────────────────────────────────────────────────────────────────────────

const defaultFilters: ApprovalFilters = {
  status: [],
  documentType: [],
  priority: [],
  assignedTo: undefined,
  dateRange: undefined,
  searchQuery: '',
};

const defaultState: ApprovalStoreState = {
  selectedApproval: null,
  selectedApprovalIds: [],
  filters: defaultFilters,
  viewMode: 'cards',
  showFilters: true,
  compactMode: false,
  isDetailsModalOpen: false,
  isRepromptModalOpen: false,
  isEditModalOpen: false,
  isApproveModalOpen: false,
  isRejectModalOpen: false,
  isBulkActionModalOpen: false,
  isEditing: false,
  editedSections: new Map(),
  notificationBadgeCount: 0,
};

// ─────────────────────────────────────────────────────────────────────────────
// Store
// ─────────────────────────────────────────────────────────────────────────────

export const useApprovalStore = create<ApprovalStore>()(
  devtools(
    persist(
      (set, get) => ({
        // Initial state
        ...defaultState,

        // Selection actions
        setSelectedApproval: (approval) =>
          set({ selectedApproval: approval }, false, 'setSelectedApproval'),

        toggleApprovalSelection: (id) =>
          set(
            (state) => {
              const ids = state.selectedApprovalIds.includes(id)
                ? state.selectedApprovalIds.filter((i) => i !== id)
                : [...state.selectedApprovalIds, id];
              return { selectedApprovalIds: ids };
            },
            false,
            'toggleApprovalSelection'
          ),

        selectAllApprovals: (ids) =>
          set({ selectedApprovalIds: ids }, false, 'selectAllApprovals'),

        clearSelection: () =>
          set(
            { selectedApproval: null, selectedApprovalIds: [] },
            false,
            'clearSelection'
          ),

        // Filter actions
        setFilters: (filters) =>
          set({ filters }, false, 'setFilters'),

        updateFilter: (key, value) =>
          set(
            (state) => ({
              filters: { ...state.filters, [key]: value },
            }),
            false,
            'updateFilter'
          ),

        resetFilters: () =>
          set({ filters: defaultFilters }, false, 'resetFilters'),

        setStatusFilter: (status) =>
          set(
            (state) => ({
              filters: { ...state.filters, status },
            }),
            false,
            'setStatusFilter'
          ),

        setSearchQuery: (query) =>
          set(
            (state) => ({
              filters: { ...state.filters, searchQuery: query },
            }),
            false,
            'setSearchQuery'
          ),

        // View actions
        setViewMode: (mode) =>
          set({ viewMode: mode }, false, 'setViewMode'),

        toggleFilters: () =>
          set(
            (state) => ({ showFilters: !state.showFilters }),
            false,
            'toggleFilters'
          ),

        toggleCompactMode: () =>
          set(
            (state) => ({ compactMode: !state.compactMode }),
            false,
            'toggleCompactMode'
          ),

        // Modal actions
        openDetailsModal: (approval) =>
          set(
            {
              selectedApproval: approval,
              isDetailsModalOpen: true,
            },
            false,
            'openDetailsModal'
          ),

        closeDetailsModal: () =>
          set({ isDetailsModalOpen: false }, false, 'closeDetailsModal'),

        openRepromptModal: (approval) =>
          set(
            {
              selectedApproval: approval,
              isRepromptModalOpen: true,
            },
            false,
            'openRepromptModal'
          ),

        closeRepromptModal: () =>
          set({ isRepromptModalOpen: false }, false, 'closeRepromptModal'),

        openEditModal: (approval) =>
          set(
            {
              selectedApproval: approval,
              isEditModalOpen: true,
              isEditing: true,
            },
            false,
            'openEditModal'
          ),

        closeEditModal: () =>
          set(
            {
              isEditModalOpen: false,
              isEditing: false,
              editedSections: new Map(),
            },
            false,
            'closeEditModal'
          ),

        openApproveModal: (approval) =>
          set(
            {
              selectedApproval: approval,
              isApproveModalOpen: true,
            },
            false,
            'openApproveModal'
          ),

        closeApproveModal: () =>
          set({ isApproveModalOpen: false }, false, 'closeApproveModal'),

        openRejectModal: (approval) =>
          set(
            {
              selectedApproval: approval,
              isRejectModalOpen: true,
            },
            false,
            'openRejectModal'
          ),

        closeRejectModal: () =>
          set({ isRejectModalOpen: false }, false, 'closeRejectModal'),

        openBulkActionModal: () =>
          set({ isBulkActionModalOpen: true }, false, 'openBulkActionModal'),

        closeBulkActionModal: () =>
          set({ isBulkActionModalOpen: false }, false, 'closeBulkActionModal'),

        closeAllModals: () =>
          set(
            {
              isDetailsModalOpen: false,
              isRepromptModalOpen: false,
              isEditModalOpen: false,
              isApproveModalOpen: false,
              isRejectModalOpen: false,
              isBulkActionModalOpen: false,
              isEditing: false,
            },
            false,
            'closeAllModals'
          ),

        // Editing actions
        startEditing: () =>
          set({ isEditing: true }, false, 'startEditing'),

        stopEditing: () =>
          set(
            { isEditing: false, editedSections: new Map() },
            false,
            'stopEditing'
          ),

        updateEditedSection: (sectionName, content) =>
          set(
            (state) => {
              const newMap = new Map(state.editedSections);
              newMap.set(sectionName, content);
              return { editedSections: newMap };
            },
            false,
            'updateEditedSection'
          ),

        clearEditedSections: () =>
          set({ editedSections: new Map() }, false, 'clearEditedSections'),

        // Notification actions
        setNotificationBadgeCount: (count) =>
          set({ notificationBadgeCount: count }, false, 'setNotificationBadgeCount'),
      }),
      {
        name: 'approval-store',
        // Only persist view preferences
        partialize: (state) => ({
          viewMode: state.viewMode,
          showFilters: state.showFilters,
          compactMode: state.compactMode,
        }),
      }
    ),
    { name: 'ApprovalStore' }
  )
);

// ─────────────────────────────────────────────────────────────────────────────
// Selectors
// ─────────────────────────────────────────────────────────────────────────────

export const selectSelectedApproval = (state: ApprovalStore) =>
  state.selectedApproval;

export const selectFilters = (state: ApprovalStore) => state.filters;

export const selectViewMode = (state: ApprovalStore) => state.viewMode;

export const selectHasActiveFilters = (state: ApprovalStore) => {
  const { filters } = state;
  return (
    (filters.status && filters.status.length > 0) ||
    (filters.documentType && filters.documentType.length > 0) ||
    (filters.priority && filters.priority.length > 0) ||
    !!filters.assignedTo ||
    !!filters.dateRange ||
    !!filters.searchQuery
  );
};

export const selectSelectedCount = (state: ApprovalStore) =>
  state.selectedApprovalIds.length;

export const selectIsAnyModalOpen = (state: ApprovalStore) =>
  state.isDetailsModalOpen ||
  state.isRepromptModalOpen ||
  state.isEditModalOpen ||
  state.isApproveModalOpen ||
  state.isRejectModalOpen ||
  state.isBulkActionModalOpen;

export default useApprovalStore;
