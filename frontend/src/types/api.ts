// API Response Types
export interface ApiResponse<T> {
  data: T;
  success: boolean;
  error?: string;
}

// Pipeline Types
export interface PipelineStatus {
  totalDocuments: number;
  inProgress: number;
  completed: number;
  failed: number;
}

export interface PipelineStage {
  id: string;
  name: string;
  status: 'pending' | 'active' | 'completed' | 'error';
  itemCount: number;
  order: number;
}

export interface PipelineItem {
  id: string;
  documentId: string;
  stage: string;
  status: 'pending' | 'processing' | 'completed' | 'error';
  title: string;
  createdAt: string;
  updatedAt: string;
  error?: string;
}

export interface PipelineStageData {
  stage: PipelineStage;
  items: PipelineItem[];
}

export interface PipelineActivity {
  id: string;
  documentId: string;
  action: string;
  timestamp: string;
  user?: string;
  details?: string;
}

// Document Types
export interface DocumentStatus {
  documentId: string;
  currentStage: string;
  status: 'pending' | 'processing' | 'completed' | 'error';
  progress: number;
  lastUpdated: string;
}

// Pipeline Service Methods
export interface PipelineService {
  getStatus: () => Promise<PipelineStatus>;
  getStages: () => Promise<PipelineStage[]>;
  getActiveItems: () => Promise<PipelineItem[]>;
  getDocumentStatus: (docId: string) => Promise<DocumentStatus>;
  advanceDocument: (docId: string, toStage: string) => Promise<void>;
}