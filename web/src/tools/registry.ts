import type { Component } from 'vue'

/**
 * 沉浸式工具栏注册中心：每个工具是一个独立的 Cordis 插件（见 tools/<name>/plugin.ts），
 * install 时调用 registerToolPlugin 自注册；主应用（kernel/页面/后台管理）只依赖此契约，与任何具体工具零耦合。
 * 拔插 = 在 tools/index.ts 的数组里增删一行。
 */
export interface ToolPluginDefinition {
  /** 唯一标识：后台管理“工具唯一标识”下拉即来源于此（选择而非手工输入） */
  key: string
  /** 默认显示名（管理端未命名时兜底） */
  defaultName: string
  /** 默认图标 key（内置图标库） */
  defaultIcon: string
  /** 名称 i18n key（可选，优先于 defaultName） */
  nameKey?: string
  /** 描述 i18n key（可选） */
  descriptionKey?: string
  /** 工具页面组件（懒加载，插件间互不打包影响） */
  loader: () => Promise<{ default: Component }>
}

const definitions = new Map<string, ToolPluginDefinition>()

export function registerToolPlugin(def: ToolPluginDefinition): void {
  definitions.set(def.key, def)
}

export function getToolDefinition(key: string): ToolPluginDefinition | undefined {
  return definitions.get(key)
}

/** 全部已注册插件标识（管理端下拉数据源） */
export function listToolPlugins(): ToolPluginDefinition[] {
  return [...definitions.values()]
}
