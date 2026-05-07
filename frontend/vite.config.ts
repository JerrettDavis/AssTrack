import { defineConfig, loadEnv, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_E2E_PROXY_TARGET || 'http://localhost:5019'
  const bridgeProxyTarget = env.VITE_BRIDGE_PROXY_TARGET || 'http://localhost:5056'
  const port = Number.parseInt(env.PORT || env.VITE_PORT || '5174', 10)

  return {
    plugins: [reactRefreshPreamble(command), react()],
    server: {
      host: '127.0.0.1',
      port: Number.isNaN(port) ? 5174 : port,
      strictPort: true,
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

function reactRefreshPreamble(command: 'serve' | 'build'): Plugin | undefined {
  if (command !== 'serve') {
    return undefined
  }

  return {
    name: 'asstrack-react-refresh-preamble',
    transformIndexHtml() {
      return [
        {
          tag: 'script',
          attrs: { type: 'module' },
          children: `
import RefreshRuntime from "/@react-refresh"
RefreshRuntime.injectIntoGlobalHook(window)
window.$RefreshReg$ = () => {}
window.$RefreshSig$ = () => (type) => type
window.__vite_plugin_react_preamble_installed__ = true
          `.trim(),
          injectTo: 'head-prepend',
        },
      ]
    },
  }
}
