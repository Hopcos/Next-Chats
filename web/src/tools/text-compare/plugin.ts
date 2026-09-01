import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * Text Compare —— 独立 Cordis 工具插件（移植自 dev-tools/TextCompare）。
 * 行级 diff（jsdiff），差异左右分栏高亮。
 */
export class TextCompareToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.text-compare')
    registerToolPlugin({
      key: 'text-compare',
      defaultName: 'Text Compare',
      defaultIcon: 'compare',
      nameKey: 'tools.compare.name',
      descriptionKey: 'tools.compare.desc',
      loader: () => import('./TextCompareView.vue'),
    })
  }
}
