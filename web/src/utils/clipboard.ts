/**
 * 复制文本：优先 Clipboard API（仅安全上下文 HTTPS / localhost 可用）；
 * 局域网 http://IP 访问时回退到 document.execCommand('copy')（仍然可用）。
 */
export async function copyText(value: string): Promise<boolean> {
  try {
    if (window.isSecureContext && navigator.clipboard) {
      await navigator.clipboard.writeText(value)
      return true
    }
  } catch {
    /* fall through to fallback */
  }
  try {
    const ta = document.createElement('textarea')
    ta.value = value
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.focus()
    ta.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(ta)
    return ok
  } catch {
    return false
  }
}
