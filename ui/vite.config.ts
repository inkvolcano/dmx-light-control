import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Build goes straight into the app's wwwroot. `npm run dev` proxies the API to a running app.
export default defineConfig({
  plugins: [react()],
  base: "./",
  build: {
    outDir: "../src/DmxLicht.App/wwwroot",
    emptyOutDir: true,
    chunkSizeWarningLimit: 800,
  },
  server: {
    port: 5173,
    proxy: {
      "/api": "http://127.0.0.1:5150",
      "/ws": { target: "ws://127.0.0.1:5150", ws: true },
    },
  },
});
