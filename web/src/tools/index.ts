import { AiTranslateToolPlugin } from './ai-translate/plugin'

/**
 * 工具插件聚合入口 —— 沉浸式工具栏的"插拔面板"：
 * 新增工具 = 新建 tools/<name>/ 插件目录 + 在此数组加一项；移除 = 删一行。主应用零改动。
 */
export const toolPlugins = [AiTranslateToolPlugin]
