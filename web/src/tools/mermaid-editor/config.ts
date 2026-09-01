/** Mermaid Editor 插件配置 */
export const DEFAULT_CODE = `graph TD
    A[Christmas] -->|Get money| B(Go shopping)
    B --> C{Let me think}
    C -->|One| D[Laptop]
    C -->|Two| E[iPhone]
    C -->|Three| F[fa:fa-car Car]`

export const LS_KEY = 'nc.tool.mermaid-editor.code'

export function loadCode(): string {
  try {
    const v = localStorage.getItem(LS_KEY)
    return v === null ? DEFAULT_CODE : v
  } catch {
    return DEFAULT_CODE
  }
}

export function saveCode(v: string): void {
  try {
    localStorage.setItem(LS_KEY, v)
  } catch {
    /* ignore */
  }
}
