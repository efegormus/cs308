import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    // The "http" profile in launchSettings.json. The browser only ever talks to the Vite origin,
    // so the session and CSRF cookies keep working as same-origin cookies.
    proxy: { "/api": "http://localhost:5200" }
  }
});
