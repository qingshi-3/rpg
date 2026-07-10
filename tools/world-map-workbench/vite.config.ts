import { defineConfig } from "vite";

export default defineConfig({
  root: ".",
  build: {
    outDir: "dist/client",
    emptyOutDir: true,
  },
  server: {
    port: 4173,
    strictPort: true,
    proxy: {
      "/api": "http://127.0.0.1:4174",
      "/project-assets": "http://127.0.0.1:4174",
    },
  },
  test: {
    environment: "node",
    include: ["tests/**/*.test.ts"],
    coverage: {
      reporter: ["text", "json-summary"],
    },
  },
});
