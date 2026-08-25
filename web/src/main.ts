import { createApp } from 'vue'
import ElementPlus, { ElMessage } from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import App from './App.vue'
import { router } from './router'
import { bootstrap } from './kernel'
import { i18n } from './i18n'
import './styles/base.css'

async function main() {
  await bootstrap()
  const app = createApp(App)
  // 全局渲染异常兜底：不让 Vue 渲染错误导致“静默空白”，尽量保留界面并给出提示
  app.config.errorHandler = (err, _instance, info) => {
    console.error('[vue] render error', info, err)
    const message = err instanceof Error ? err.message : String(err)
    ElMessage.error(`${info || 'render'} · ${message}`)
  }
  app.use(ElementPlus)
  app.use(i18n)
  app.use(router)
  app.mount('#app')
}

void main()
