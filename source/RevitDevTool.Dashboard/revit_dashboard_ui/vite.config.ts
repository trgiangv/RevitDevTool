import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  // Production: relative paths for WebView2 local file loading
  // Development: absolute paths for dev server
  base: mode === 'production' ? "./" : "/",
  
  plugins: [react()],
  
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },

  server: {
    port: 5173,
    host: true, // Allow external connections
    cors: true,
  },

  build: {
    outDir: "dist",
    sourcemap: mode !== 'production',
    // Optimize for WebView2
    target: "es2020",
    minify: mode === 'production',
  },
}))
