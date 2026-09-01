import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * JSON Formatter —— 独立 Cordis 工具插件（移植自 dev-tools/JsonFormatter）。
 * 能力：格式化 / 压缩 / 缩进选择 / 语法错误定位提示；默认输入等配置见 config.ts。
 */
export class JsonFormatterToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.json-formatter')
    registerToolPlugin({
      key: 'json-formatter',
      defaultName: 'JSON Formatter',
      defaultIcon: 'braces',
      nameKey: 'tools.json.name',
      descriptionKey: 'tools.json.desc',
      loader: () => import('./JsonFormatterView.vue'),
    })
  }
}
