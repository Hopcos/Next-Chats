import type { ApprovalDecision } from '@/api/types'

/** Cordis 事件声明：跨插件通信契约（驱动一切皆插件的架构） */
declare module 'cordis' {
  interface Events {
    'auth:changed': (user: import('@/api/types').UserProfile | null) => void
    'session:current-changed': (sessionId: string | null) => void
    'chat:settings-changed': (settings: import('@/api/types').ChatSettings) => void
    'chat:streaming-changed': (streaming: boolean) => void
    'notify:message': (type: 'success' | 'warning' | 'error' | 'info', message: string) => void
    'theme:changed': (theme: string) => void
    'three:toggled': (enabled: boolean) => void
    'approval:required': (approval: {
      approvalId: string
      serverName: string
      toolName: string
      argumentsJson?: string
    }) => void
    'approval:decided': (approvalId: string, decision: ApprovalDecision) => void
  }
}

export {}
