import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import basicSsl from '@vitejs/plugin-basic-ssl';

export default defineConfig({
    plugins: [
        react(),
    ],
    server: {
        host: '0.0.0.0',
        proxy: {
            '/api': {
                target: 'https://localhost:7270',
                changeOrigin: true,
                secure: false,
            },
            '/lobbyHub': {
                target: 'https://localhost:7270',
                changeOrigin: true,
                secure: false,
                ws: true,
            },
        },
    },
});