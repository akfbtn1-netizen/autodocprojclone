// ═══════════════════════════════════════════════════════════════════════════
// API Client - Base Configuration
// Connects to: V2 API on port 5195
// ═══════════════════════════════════════════════════════════════════════════

import axios, { type AxiosError, type AxiosInstance, type AxiosRequestConfig } from 'axios';
import type { ApiError, Result } from '@/types';

// Create axios instance with default config
const api: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor for adding auth token
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor for error handling
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as AxiosRequestConfig & { _retry?: boolean };

    // Handle 401 errors (token refresh)
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const refreshToken = localStorage.getItem('refreshToken');
        if (refreshToken) {
          const response = await axios.post('/api/auth/refresh', { refreshToken });
          const { token } = response.data;
          localStorage.setItem('authToken', token);

          if (originalRequest.headers) {
            originalRequest.headers.Authorization = `Bearer ${token}`;
          }
          return api(originalRequest);
        }
      } catch (refreshError) {
        localStorage.removeItem('authToken');
        localStorage.removeItem('refreshToken');
        window.location.href = '/login';
      }
    }

    return Promise.reject(error);
  }
);

// Helper to parse API errors
function parseApiError(error: unknown): ApiError {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ApiError>;
    if (axiosError.response?.data) {
      return axiosError.response.data;
    }
    return {
      code: axiosError.code || 'NETWORK_ERROR',
      message: axiosError.message || 'A network error occurred',
    };
  }
  return {
    code: 'UNKNOWN_ERROR',
    message: error instanceof Error ? error.message : 'An unknown error occurred',
  };
}

// Generic API request wrapper with Result type
async function request<T>(config: AxiosRequestConfig): Promise<Result<T>> {
  try {
    const response = await api.request<T>(config);
    return { success: true, data: response.data };
  } catch (error) {
    return { success: false, error: parseApiError(error) };
  }
}

// Typed request helpers
export const apiClient = {
  get: <T>(url: string, config?: AxiosRequestConfig) =>
    request<T>({ ...config, method: 'GET', url }),

  post: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    request<T>({ ...config, method: 'POST', url, data }),

  put: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    request<T>({ ...config, method: 'PUT', url, data }),

  patch: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
    request<T>({ ...config, method: 'PATCH', url, data }),

  delete: <T>(url: string, config?: AxiosRequestConfig) =>
    request<T>({ ...config, method: 'DELETE', url }),
};

// Export raw axios instance for special cases (file downloads, etc.)
export { api };
