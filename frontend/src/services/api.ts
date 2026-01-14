import { apiClient } from './apiClient';

// Types
export interface DashboardKpi {
  totalDocuments: number;
  pendingApprovals: number;
  completedToday: number;
  rejectedCount: number;
}

export interface Activity {
  id: string;
  type: string;
  message: string;
  timestamp: string;
  user?: string;
}

export interface Document {
  id: string;
  title: string;
  type: string;
  status: string;
  createdAt: string;
  createdBy: string;
}

export interface Approval {
  id: string;
  documentId: string;
  documentTitle: string;
  requestedBy: string;
  requestedAt: string;
  status: 'pending' | 'approved' | 'rejected';
}

// API Service
export const api = {
  // Dashboard endpoints
  dashboard: {
    getKpis: async (): Promise<DashboardKpi> => {
      try {
        const response = await apiClient.get<DashboardKpi>('/dashboard/kpis');
        return response.data;
      } catch (error) {
        console.error('Failed to fetch dashboard KPIs:', error);
        // Return default values if backend not available
        return {
          totalDocuments: 0,
          pendingApprovals: 0,
          completedToday: 0,
          rejectedCount: 0,
        };
      }
    },
    
    getActivity: async (limit = 10): Promise<Activity[]> => {
      try {
        const response = await apiClient.get<Activity[]>(`/dashboard/activity?limit=${limit}`);
        return response.data;
      } catch (error) {
        console.error('Failed to fetch activity:', error);
        return [];
      }
    },
  },

  // Document endpoints
  documents: {
    getRecent: async (limit = 5): Promise<Document[]> => {
      try {
        const response = await apiClient.get<Document[]>(`/documents/recent?limit=${limit}`);
        return response.data;
      } catch (error) {
        console.error('Failed to fetch recent documents:', error);
        return [];
      }
    },
    
    getById: async (id: string): Promise<Document> => {
      const response = await apiClient.get<Document>(`/documents/${id}`);
      return response.data;
    },
    
    getAll: async (): Promise<Document[]> => {
      const response = await apiClient.get<Document[]>('/documents');
      return response.data;
    },
  },

  // Approval endpoints
  approvals: {
    getPending: async (page = 1, pageSize = 20): Promise<{ items: Approval[]; total: number }> => {
      try {
        const response = await apiClient.get<{ items: Approval[]; total: number }>(
          `/approvals/pending?page=${page}&pageSize=${pageSize}`
        );
        return response.data;
      } catch (error) {
        console.error('Failed to fetch pending approvals:', error);
        return { items: [], total: 0 };
      }
    },
    
    approve: async (id: string, comment?: string): Promise<void> => {
      await apiClient.post(`/approvals/${id}/approve`, { comment });
    },
    
    reject: async (id: string, reason: string): Promise<void> => {
      await apiClient.post(`/approvals/${id}/reject`, { reason });
    },
  },
};

export default api;