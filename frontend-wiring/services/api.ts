// =============================================
// API CLIENT - Axios instance with interceptors
// File: frontend/src/services/api.ts
// =============================================

import axios, { AxiosInstance, InternalAxiosRequestConfig, AxiosError } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5195/api';

export const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor - add auth token
api.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('authToken');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor - handle errors
api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    // Handle 401 Unauthorized - redirect to login
    if (error.response?.status === 401) {
      localStorage.removeItem('authToken');
      localStorage.removeItem('user');
      localStorage.removeItem('userId');
      localStorage.removeItem('userEmail');
      window.location.href = '/login';
      return Promise.reject(new Error('Session expired. Please log in again.'));
    }

    // Handle 403 Forbidden
    if (error.response?.status === 403) {
      return Promise.reject(new Error('You do not have permission to perform this action.'));
    }

    // Handle 404 Not Found
    if (error.response?.status === 404) {
      return Promise.reject(new Error('The requested resource was not found.'));
    }

    // Handle 500 Server Error
    if (error.response?.status === 500) {
      console.error('Server error:', error.response.data);
      return Promise.reject(new Error('An unexpected server error occurred. Please try again.'));
    }

    // Handle network errors
    if (!error.response) {
      return Promise.reject(new Error('Network error - please check your connection.'));
    }

    // Generic error
    const message = (error.response?.data as { message?: string })?.message || error.message;
    return Promise.reject(new Error(message));
  }
);

export default api;
