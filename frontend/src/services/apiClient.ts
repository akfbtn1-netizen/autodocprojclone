import axios, { AxiosInstance, AxiosError } from 'axios';

// API Client Configuration
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5195/api';

// Create axios instance
export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      console.error('Unauthorized - clearing auth');
      localStorage.removeItem('authToken');
    }
    
    if (error.code === 'ECONNREFUSED' || error.code === 'ERR_NETWORK') {
      console.error('API server not reachable - is backend running on', API_BASE_URL, '?');
    }
    
    return Promise.reject(error);
  }
);

export default apiClient;