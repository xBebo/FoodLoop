import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Same-origin API in development: the browser calls /api and Vite forwards it to ASP.NET Core (http profile),
    // so the Identity and antiforgery cookies work without CORS.
    // Keys starting with ^ are RegExps: match /api, /api/... and /api?... but not /apix.
    proxy: { '^/api(?:[/?]|$)': 'http://localhost:5179' },
  },
})
