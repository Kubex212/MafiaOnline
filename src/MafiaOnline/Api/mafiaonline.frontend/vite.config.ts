import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    server: {
        proxy: {
            '/api': {
                target: 'https://localhost:44330',
                changeOrigin: true,
                secure: false,
            },
            '/lobbyHub': {
                target: 'https://localhost:44330',
                changeOrigin: true,
                secure: false,
                ws: true,
            },
        },
    },
});