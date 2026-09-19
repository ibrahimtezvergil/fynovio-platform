import path from 'node:path'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': path.resolve(__dirname, './src') },
  },
  optimizeDeps: {
    // These are only ever imported from lazy-loaded routes, so Vite never
    // discovers them at cold start. Without pre-bundling them here, the
    // first navigation to /calendar or /demo/charts triggers a mid-session
    // dependency re-optimization + forced reload, and the in-flight dynamic
    // import() for the page chunk loses that race with a spurious
    // "Failed to fetch dynamically imported module" error.
    include: [
      '@fullcalendar/react',
      '@fullcalendar/react/daygrid',
      '@fullcalendar/react/interaction',
      '@fullcalendar/react/list',
      '@fullcalendar/react/locales/tr',
      '@fullcalendar/react/themes/classic',
      '@fullcalendar/react/timegrid',
      'recharts',
    ],
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined
          if (id.includes('fullcalendar')) return 'fullcalendar'
          if (id.includes('@tiptap')) return 'tiptap'
          if (id.includes('recharts')) return 'recharts'
          return undefined
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
