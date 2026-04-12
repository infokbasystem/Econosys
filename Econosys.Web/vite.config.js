import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import fs from 'fs';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    https: {
      key: fs.readFileSync('localhost-key.pem'),
      cert: fs.readFileSync('localhost.pem'),
    },
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        ws: true,
        rejectUnauthorized: false,
      }
    }
  }
})
