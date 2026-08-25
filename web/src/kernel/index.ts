import { Context } from 'cordis'
import {
  AuthService,
  CatalogService,
  ChatService,
  NotifyService,
  SessionService,
  SettingsService,
  ThemeService,
  ThreeService,
} from '@/kernel/plugins'

/**
 * Next Chats 内核——一切皆插件，由 Cordis 驱动：
 *   应用 = Context（作用域/生命周期/事件总线）+ 一组 Service 插件（能力注入）。
 * 组件只依赖 kernel 暴露的服务契约，不直接持有业务状态；事件解耦跨模块协作。
 */
export const app = new Context()

/** 服务注册顺序即依赖拓扑：基础服务在前，业务服务在后 */
export function registerPlugins() {
  // 基础服务（无依赖）
  app.plugin(NotifyService)
  app.plugin(ThemeService)
  // 偏好（localStorage + 服务端同步）
  app.plugin(SettingsService)
  // 认证
  app.plugin(AuthService)
  // 能力目录（需要认证）
  app.plugin(CatalogService)
  // 会话与聊天（依赖设置）
  app.plugin(SessionService)
  app.plugin(ChatService)
  // 3D 背景开关
  app.plugin(ThreeService)
}

/** 类型化内核访问器（cordis 运行时按名称注入，类型在此收敛，避免组件里 any） */
export interface Kernel {
  notify: NotifyService
  theme: ThemeService
  settings: SettingsService
  auth: AuthService
  catalog: CatalogService
  session: SessionService
  chat: ChatService
  three: ThreeService
}

export const kernel = {
  get notify() {
    return app.get('notify') as Kernel['notify']
  },
  get theme() {
    return app.get('theme') as Kernel['theme']
  },
  get settings() {
    return app.get('settings') as Kernel['settings']
  },
  get auth() {
    return app.get('auth') as Kernel['auth']
  },
  get catalog() {
    return app.get('catalog') as Kernel['catalog']
  },
  get session() {
    return app.get('session') as Kernel['session']
  },
  get chat() {
    return app.get('chat') as Kernel['chat']
  },
  get three() {
    return app.get('three') as Kernel['three']
  },
} satisfies Kernel

export async function bootstrap() {
  registerPlugins()
  await app.start()
}
