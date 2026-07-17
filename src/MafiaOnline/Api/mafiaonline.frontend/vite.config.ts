import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    // Proxy API requests to the backend. Update the target to match your backend URL/port.
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false
      },
      '/lobbyHub': {
        target: 'https://localhost:5001',
        ws: true,
        secure: false
      }
    }
  }
});
