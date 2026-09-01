import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * AI 翻译 —— 沉浸式工具栏的第一个 Cordis 工具插件。
 * 作为独立 Cordis 插件：构造即自注册（kernel 装载插件时进入注册中心），
 * 与主应用零耦合；拔除 = 从 tools/index.ts 的数组里删掉本插件即可，全站无残留引用。
 * 翻译相关任何内容都不落库：状态（方向/模型/专家提示词）只写浏览器 localStorage。
 */
export class AiTranslateToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.ai-translate')
    registerToolPlugin({
      key: 'ai-translate',
      defaultName: 'AI Translate',
      defaultIcon: 'translate',
      nameKey: 'tools.translate.name',
      descriptionKey: 'tools.translate.desc',
      loader: () => import('./AiTranslateView.vue'),
    })
  }
}
