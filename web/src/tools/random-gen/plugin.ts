import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * Random Generator —— 独立 Cordis 工具插件（移植自 dev-tools/RandomTool）。
 * 密码学随机（crypto.getRandomValues + 取模偏差剔除）；配置与结果仅存 localStorage。
 */
export class RandomGenToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.random-gen')
    registerToolPlugin({
      key: 'random-gen',
      defaultName: 'Random Generator',
      defaultIcon: 'dice',
      nameKey: 'tools.random.name',
      descriptionKey: 'tools.random.desc',
      loader: () => import('./RandomGenView.vue'),
    })
  }
}
