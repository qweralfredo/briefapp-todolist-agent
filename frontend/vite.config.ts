/// <reference types="vitest" />
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Context-Box RAG Python API (port 8482)
      '/api/context': {
        target: 'http://localhost:8482',
        changeOrigin: true,
      },
      // Main .NET API (port 8480)
      '/api': {
        target: 'http://localhost:8480',
        changeOrigin: true,
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
    },
  },
})

