import { createI18n } from 'vue-i18n'
import enCommon from './locales/en/common'
import enLogin from './locales/en/login'
import enChat from './locales/en/chat'
import enSettings from './locales/en/settings'
import enAdmin from './locales/en/admin'
import enTools from './locales/en/tools'
import zhCommon from './locales/zh/common'
import zhLogin from './locales/zh/login'
import zhChat from './locales/zh/chat'
import zhSettings from './locales/zh/settings'
import zhAdmin from './locales/zh/admin'
import zhTools from './locales/zh/tools'

export type AppLang = 'en' | 'zh'

const LANG_KEY = 'nextchats.lang'

/** 浅合并会丢嵌套；这里做递归深合并（后源优先） */
function deepAssign<T>(target: T, ...sources: Record<string, unknown>[]): T {
  const out: Record<string, unknown> = { ...(target as Record<string, unknown>) }
  for (const src of sources) {
    for (const [k, v] of Object.entries(src)) {
      const existing = out[k]
      if (v && typeof v === 'object' && !Array.isArray(v) && existing && typeof existing === 'object' && !Array.isArray(existing)) {
        out[k] = deepAssign(existing as Record<string, unknown>, v as Record<string, unknown>)
      } else if (v !== undefined) {
        out[k] = v
      }
    }
  }
  return out as T
}

function loadLang(): AppLang {
  try {
    return localStorage.getItem(LANG_KEY) === 'zh' ? 'zh' : 'en'
  } catch {
    return 'en'
  }
}

const en = deepAssign({}, enCommon, enLogin, enChat, enSettings, enAdmin, enTools)
const zh = deepAssign({}, zhCommon, zhLogin, zhChat, zhSettings, zhAdmin, zhTools)

export const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: loadLang(),
  fallbackLocale: 'en',
  messages: { en, zh },
})

export function getLang(): AppLang {
  return (i18n.global.locale.value as AppLang) ?? 'en'
}

export function setLang(lang: AppLang): void {
  i18n.global.locale.value = lang
  try {
    localStorage.setItem(LANG_KEY, lang)
  } catch {
    /* ignore */
  }
  document.documentElement.lang = lang
}

document.documentElement.lang = loadLang()
