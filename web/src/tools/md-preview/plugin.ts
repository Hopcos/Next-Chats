import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * Markdown Preview —— 独立 Cordis 工具插件（移植自 dev-tools/MarkdownPreview）。
 * GFM 风格语法（表格/删除线/任务列表）实时预览；React 项目里的 react-markdown 以当前系统已有的 markdown-it 等价实现。
 */
export class MarkdownPreviewToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.md-preview')
    registerToolPlugin({
      key: 'md-preview',
      defaultName: 'Markdown Preview',
      defaultIcon: 'markdown',
      nameKey: 'tools.md.name',
      descriptionKey: 'tools.md.desc',
      loader: () => import('./MarkdownPreviewView.vue'),
    })
  }
}
