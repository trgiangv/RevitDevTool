import path from "node:path"
import react from "@vitejs/plugin-react"
import { defineConfig } from "vite-plus"

export default defineConfig(({ mode }) => ({
  base: mode === "production" ? "./" : "/",
  plugins: [react()],
  resolve: {
    alias: [
      {
        find: "@douyinfe/semi-ui/dist/css/semi.min.css",
        replacement: path.resolve(
          __dirname,
          "node_modules/@douyinfe/semi-ui/dist/css/semi.min.css",
        ),
      },
      { find: "@", replacement: path.resolve(__dirname, "./src") },
    ],
  },
  server: {
    port: 5173,
    host: true,
    cors: true,
  },
  build: {
    outDir: "dist",
    sourcemap: mode !== "production",
    target: "es2022",
    minify: mode === "production",
  },
  fmt: {
    semi: false,
    singleQuote: false,
  },
  lint: {
    ignorePatterns: ["dist/**"],
    options: {
      typeAware: true,
      typeCheck: true,
    },
  },
}))
