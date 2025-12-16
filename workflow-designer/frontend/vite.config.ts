import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    server: {
      port: parseInt(process.env.PORT || env.VITE_PORT || '5173'),
      proxy: {
        '/api': {
          target: process.env.services__workflowdesigner_api__https__0 ||
            process.env.services__workflowdesigner_api__http__0 ||
            'http://localhost:5000',
          changeOrigin: true,
          secure: false,
        }
      }
    }
  }
})
