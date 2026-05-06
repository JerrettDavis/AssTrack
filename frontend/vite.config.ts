import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_E2E_PROXY_TARGET || 'http://localhost:5019'
  const bridgeProxyTarget = env.VITE_BRIDGE_PROXY_TARGET || 'http://localhost:5056'

  return {
    plugins: [react()],
    server: {
      port: 5174,
      proxy: {
        '/api': {
          target: proxyTarget,
          changeOrigin: true,
        },
        '/bridge': {
          target: bridgeProxyTarget,
          changeOrigin: true,
        },
      },
    },
  }
})
