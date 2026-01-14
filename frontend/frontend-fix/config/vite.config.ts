import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
      '@components': path.resolve(__dirname, './src/components'),
      '@hooks': path.resolve(__dirname, './src/hooks'),
      '@stores': path.resolve(__dirname, './src/stores'),
      '@services': path.resolve(__dirname, './src/services'),
      '@types': path.resolve(__dirname, './src/types'),
      '@lib': path.resolve(__dirname, './src/lib'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // V2 API on port 5195
      '/api': {
        target: 'http://localhost:5195',
        changeOrigin: true,
        secure: false,
      },
      // SignalR WebSocket hub
      '/hubs': {
        target: 'http://localhost:5195',
        changeOrigin: true,
        secure: false,
        ws: true,
      },
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom', 'react-router-dom'],
          'flow-vendor': ['@xyflow/react'],
          'animation-vendor': ['framer-motion'],
          'data-vendor': ['@tanstack/react-query', 'zustand', 'axios'],
        },
      },
    },
  },
});
