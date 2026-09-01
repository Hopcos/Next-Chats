/**
 * 系统内置图标库：管理端为工具挑选 ICON 的唯一来源（key 存库，前端渲染）。
 * 每个图标 = viewBox(0 0 24 24) 下若干 stroke path 段，风格统一（线性、圆角端点、currentColor）。
 */
export interface BuiltinIcon {
  key: string
  /** 渲染用的 path 段列表（stroke 绘制） */
  paths: string[]
}

export const BUILTIN_ICONS: BuiltinIcon[] = [
  { key: 'toolbox', paths: ['M3 8h18v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8Z', 'M8 8V6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2', 'M3 12h18', 'M12 13.4v2.2'] },
  { key: 'translate', paths: ['M4 6h9', 'M8.5 4.2V6', 'M11 6C10.3 9.6 7.8 12.6 4.2 14.2', 'M6.8 9.4c1.1 2.1 2.9 3.8 5.2 4.8', 'M13.2 20l4.4-10.4L22 20', 'M14.7 16.6h5.6'] },
  { key: 'chat', paths: ['M4 4h16v12H8.5L4 20V4Z', 'M8 8.5h8', 'M8 11.5h5'] },
  { key: 'globe', paths: ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z', 'M3.5 9h17', 'M3.5 15h17', 'M12 3c-2.6 2.4-4 5.6-4 9s1.4 6.6 4 9c2.6-2.4 4-5.6 4-9s-1.4-6.6-4-9Z'] },
  { key: 'sparkles', paths: ['M11 4.5l1.4 4.1 4.1 1.4-4.1 1.4L11 15.5l-1.4-4.1L5.5 10l4.1-1.4L11 4.5Z', 'M18.5 14.5l.9 2.6 2.6.9-2.6.9-.9 2.6-.9-2.6-2.6-.9 2.6-.9.9-2.6Z'] },
  { key: 'book', paths: ['M5 4a1.5 1.5 0 0 1 1.5-1.5H20v16H6.5A1.5 1.5 0 0 0 5 20V4Z', 'M5 20a1.5 1.5 0 0 1 1.5-1.5H20', 'M9 6.5h7', 'M9 9.5h5'] },
  { key: 'code', paths: ['M9 7l-5 5 5 5', 'M15 7l5 5-5 5', 'M13 5l-2 14'] },
  { key: 'chart', paths: ['M4 20V10', 'M10 20V4', 'M16 20v-8', 'M21 20H3'] },
  { key: 'lock', paths: ['M5.5 11h13a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-13a1 1 0 0 1-1-1v-7a1 1 0 0 1 1-1Z', 'M8.5 11V8a3.5 3.5 0 0 1 7 0v3', 'M12 14.5v2.5'] },
  { key: 'rocket', paths: ['M12 2.5c2.8 1.9 4.5 5.3 4.5 9.2l-1.7 3h-5.6l-1.7-3C7.5 7.8 9.2 4.4 12 2.5Z', 'M12 8.5v.1', 'M9.5 16.5 8 21l3-1.6L14 21l-1.5-4.5', 'M7.5 13 5 14.5 6 11.5', 'M16.5 13 19 14.5 18 11.5'] },
  { key: 'mic', paths: ['M12 3a2.5 2.5 0 0 1 2.5 2.5v5a2.5 2.5 0 0 1-5 0v-5A2.5 2.5 0 0 1 12 3Z', 'M6 10.5a6 6 0 0 0 12 0', 'M12 16.5V20', 'M9 20h6'] },
  { key: 'image', paths: ['M4 5h16a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z', 'M9 10.5v.1', 'M20 15l-5-5-8 8'] },
]

const iconMap = new Map(BUILTIN_ICONS.map((i) => [i.key, i]))

export function getIcon(key: string): BuiltinIcon | undefined {
  return iconMap.get(key)
}

/** 工具默认兜底图标 */
export const FALLBACK_ICON = 'toolbox'
