/**
 * UUID v4 生成：`crypto.randomUUID()` 仅在安全上下文（HTTPS / localhost）可用；
 * 通过局域网 IP（http://10.x.x.x）访问时缺失，这里按能力降级：
 * ① crypto.randomUUID（安全上下文）→ ② crypto.getRandomValues 手排（非安全上下文仍可用）→ ③ 渲染兜底。
 */
export function uuid(): string {
  const c = globalThis.crypto
  if (c && typeof c.randomUUID === 'function') {
    return c.randomUUID()
  }
  try {
    if (c && typeof c.getRandomValues === 'function') {
      const b = c.getRandomValues(new Uint8Array(16))
      b[6] = (b[6] & 0x0f) | 0x40 // version 4
      b[8] = (b[8] & 0x3f) | 0x80 // variant 10
      const hex = Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('')
      return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
    }
  } catch {
    /* fall through */
  }
  // 最终兜底（极老浏览器 / 极端环境）
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
    const r = (Math.random() * 16) | 0
    const v = ch === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}
