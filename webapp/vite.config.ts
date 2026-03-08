import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: false,
  },
  build: {
    ssr: false,
    emptyOutDir: false,
    // External node modules that should not be bundled
    rollupOptions: {
      external: ['fs', 'path', 'os', 'node-ssh', 'node-pty', 'ssh2'],
      output: {
        globals: {},
      },
    },
  },
  resolve: {
    alias: {
      '@components': path.resolve(__dirname, 'src/components'),
      '@hooks': path.resolve(__dirname, 'src/hooks'),
      '@services': path.resolve(__dirname, 'src/services'),
      '@types': path.resolve(__dirname, 'src/types'),
      '@utils': path.resolve(__dirname, 'src/utils'),
      '@styles': path.resolve(__dirname, 'src/styles'),
      '@bridge': path.resolve(__dirname, 'src/bridge'),
    },
  },
})
