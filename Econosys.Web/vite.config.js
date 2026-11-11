import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import fs from 'fs';
import { randomUUID } from 'node:crypto';

// https://vite.dev/config/
export default defineConfig(({ command }) => {
  const buildId = command === 'build'
    ? `${new Date().toISOString().replace(/\D/g, '').slice(0, 14)}-${randomUUID().slice(0, 8)}`
    : 'development';

  return {
    define: {
      __APP_BUILD_ID__: JSON.stringify(buildId),
    },
    plugins: [
      react(),
      {
        name: 'app-build-version',
        generateBundle() {
          this.emitFile({
            type: 'asset',
            fileName: 'version.json',
            source: JSON.stringify({ buildId }),
          });
        },
      },
    ],
    server: {
      https: command === 'serve' ? {
        key: fs.readFileSync('localhost-key.pem'),
        cert: fs.readFileSync('localhost.pem'),
      } : undefined,
      proxy: {
        '/api': {
          target: 'https://localhost:5001',
          changeOrigin: true,
          ws: true,
          rejectUnauthorized: false,
        }
      }
    }
  };
})
