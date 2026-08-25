import { getLang, i18n } from '@/i18n'

export interface ApiError {
  code: string
  message: string
}

const TOKEN_KEY = 'nextchats.token'

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

/**
 * 错误码 → 前端 i18n 文案（默认英文/中文随语言切换）。
 * 已知错误码用译文；未知码回退到服务端 message；再兜底通用文案。
 */
export function translateError(code: string | undefined, fallback: string): string {
  if (code) {
    const key = `err.${code}`
    if (i18n.global.te(key)) return i18n.global.t(key)
  }
  return fallback || i18n.global.t('common.requestFailed')
}

/**
 * 轻量 HTTP 客户端：统一携带 JWT、统一错误解析（友好文案 + 错误码，不暴露 stack/Endpoint/Header）。
 */
export async function request<T = unknown>(path: string, options: RequestInit = {}): Promise<T> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Lang': getLang(),
    ...(options.headers as Record<string, string> | undefined),
  }
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, { ...options, headers })
  if (res.status === 401 && !path.startsWith('/api/auth/login')) {
    tokenStore.clear()
    window.dispatchEvent(new CustomEvent('nextchats:unauthorized'))
    throw { code: 'AUTH_EXPIRED', message: translateError('AUTH_EXPIRED', '登录已过期，请重新登录') }
  }

  if (res.status === 204) return undefined as T

  const text = await res.text()
  let body: unknown = null
  try {
    body = text ? JSON.parse(text) : null
  } catch {
    body = text
  }

  if (!res.ok) {
    const err = (body ?? {}) as Partial<ApiError>
    throw { code: err.code ?? 'HTTP_ERROR', message: err.message ? translateError(err.code, err.message) : i18n.global.t('err.HTTP_ERROR') } as ApiError
  }
  return body as T
}

export const http = {
  get: <T = unknown>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T = unknown>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T = unknown>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),
  delete: <T = unknown>(path: string) => request<T>(path, { method: 'DELETE' }),
}

/** 流式 POST（SSE）：读取 data: 行并回调解析后的 JSON 对象 */
export async function streamPost(
  path: string,
  body: unknown,
  onEvent: (data: Record<string, unknown>) => void,
  signal?: AbortSignal,
): Promise<void> {
  const token = tokenStore.get()
  const res = await fetch(path, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Lang': getLang(),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
    signal,
  })

  if (!res.ok || !res.body) {
    let code = 'STREAM_ERROR'
    let message = translateError(code, i18n.global.t('err.STREAM_ERROR'))
    try {
      const err = (await res.json()) as Partial<ApiError>
      if (err.code || err.message) {
        code = err.code ?? code
        message = err.message ? translateError(err.code, err.message) : message
      }
    } catch {
      /* 忽略 */
    }
    throw { code, message } as ApiError
  }

  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    let sepIndex = buffer.indexOf('\n\n')
    while (sepIndex >= 0) {
      const chunk = buffer.slice(0, sepIndex)
      buffer = buffer.slice(sepIndex + 2)
      for (const line of chunk.split('\n')) {
        if (line.startsWith('data:')) {
          const payload = line.slice(5).trim()
          if (!payload) continue
          try {
            onEvent(JSON.parse(payload) as Record<string, unknown>)
          } catch {
            /* 忽略非 JSON 行 */
          }
        }
      }
      sepIndex = buffer.indexOf('\n\n')
    }
  }
}
