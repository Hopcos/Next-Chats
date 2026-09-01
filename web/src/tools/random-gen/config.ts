/** Random Generator 插件配置 */
export interface RandomConfig {
  includeNumbers: boolean
  includeLowercase: boolean
  includeUppercase: boolean
  includeSpecial: boolean
  minLength: number
  maxLength: number
  count: number
}

export const DEFAULT_CONFIG: RandomConfig = {
  includeNumbers: true,
  includeLowercase: true,
  includeUppercase: true,
  includeSpecial: true,
  minLength: 8,
  maxLength: 12,
  count: 1,
}

export const LS_KEY = 'nc.tool.random-gen.config'

export function loadConfig(): RandomConfig {
  try {
    const raw = localStorage.getItem(LS_KEY)
    if (raw) return { ...DEFAULT_CONFIG, ...(JSON.parse(raw) as Partial<RandomConfig>) }
  } catch {
    /* ignore */
  }
  return { ...DEFAULT_CONFIG }
}

export function saveConfig(cfg: RandomConfig): void {
  try {
    localStorage.setItem(LS_KEY, JSON.stringify(cfg))
  } catch {
    /* ignore */
  }
}

export const CHARSETS = {
  numbers: '0123456789',
  lowercase: 'abcdefghijklmnopqrstuvwxyz',
  uppercase: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ',
  special: '!@#$%^&*()_+-=[]{}|;:,.<>?',
} as const

/**
 * 无偏差字符集抽样（比原实现的 `% length` 取模多了偏差剔除）：
 * 拒绝落在 floor 截断余数区间内的随机值，保证每个字符等概率。
 */
export function secureSample(charset: string, length: number): string {
  const n = charset.length
  const limit = Math.floor(0xffffffff / n) * n - 1
  const buf = new Uint32Array(length)
  let out = ''
  let filled = 0
  while (filled < length) {
    crypto.getRandomValues(buf)
    for (let i = 0; i < buf.length && filled < length; i++) {
      if (buf[i] <= limit) {
        out += charset[buf[i] % n]
        filled++
      }
    }
  }
  return out
}
