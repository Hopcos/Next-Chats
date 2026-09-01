/** Color Picker 插件配置与色彩算法（零依赖实现） */

export const LS = {
  history: 'nc.tool.color-picker.history',
  current: 'nc.tool.color-picker.current',
} as const

/** 历史上限（超出淘汰最旧） */
export const MAX_HISTORY = 60

export function hexToRgb(hex: string): [number, number, number] | null {
  const m = /^#?([a-f\d])([a-f\d])([a-f\d])$/i.exec(hex.trim())
  const full = m ? `#${m[1]}${m[1]}${m[2]}${m[2]}${m[3]}${m[3]}` : hex.trim()
  const r = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(full)
  return r ? [parseInt(r[1], 16), parseInt(r[2], 16), parseInt(r[3], 16)] : null
}

export function isValidHex(hex: string): boolean {
  return hexToRgb(hex) !== null
}

export function rgbToHex(r: number, g: number, b: number): string {
  const h = (v: number) => Math.round(v).toString(16).padStart(2, '0')
  return `#${h(r)}${h(g)}${h(b)}`.toUpperCase()
}

export function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
  const rn = r / 255
  const gn = g / 255
  const bn = b / 255
  const max = Math.max(rn, gn, bn)
  const min = Math.min(rn, gn, bn)
  const l = (max + min) / 2
  if (max === min) return [0, 0, Math.round(l * 100)]
  const d = max - min
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  let h = 0
  switch (max) {
    case rn:
      h = ((gn - bn) / d + (gn < bn ? 6 : 0)) / 6
      break
    case gn:
      h = ((bn - rn) / d + 2) / 6
      break
    default:
      h = ((rn - gn) / d + 4) / 6
  }
  return [Math.round(h * 360), Math.round(s * 100), Math.round(l * 100)]
}

/** 相对亮度（WCAG） */
function luminance(r: number, g: number, b: number): number {
  const f = (v: number) => {
    const c = v / 255
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4)
  }
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
}

/** 与指定色的对比度（1~21） */
export function contrastRatio(hex: string, against: [number, number, number]): number {
  const rgb = hexToRgb(hex)
  if (!rgb) return 1
  const l1 = luminance(...rgb)
  const l2 = luminance(...against)
  const [hi, lo] = l1 >= l2 ? [l1, l2] : [l2, l1]
  return (hi + 0.05) / (lo + 0.05)
}

export function loadHistory(): string[] {
  try {
    const raw = localStorage.getItem(LS.history)
    const arr = raw ? (JSON.parse(raw) as unknown) : []
    return Array.isArray(arr) ? (arr as string[]).filter((x) => typeof x === 'string' && isValidHex(x)) : []
  } catch {
    return []
  }
}

export function saveHistory(list: string[]): void {
  try {
    localStorage.setItem(LS.history, JSON.stringify(list))
  } catch {
    /* ignore */
  }
}
