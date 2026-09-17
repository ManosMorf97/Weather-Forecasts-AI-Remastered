import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import basicSsl from '@vitejs/plugin-basic-ssl'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  // YOUR_* are ambient machine env vars shared with the backend (see WeatherUserActions'
  // Program.cs) - exposing them under import.meta.env keeps the naming consistent.
  envPrefix: ['VITE_', 'YOUR_'],
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] }),
    // Dev-only: serves the Vite dev server itself over https with an auto-generated,
    // self-signed cert (separate from the backend's ASP.NET Core dev cert).
    basicSsl(),
  ],
  server: {
    proxy: {
      // Dev-only: forwards to the ASP.NET Core backend (see launchSettings.json) so the
      // browser never has to deal with cross-origin requests or its dev HTTPS certificate.
      '/api': {
        target: 'https://localhost:7237',
        changeOrigin: true,
        secure: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
  },
})
