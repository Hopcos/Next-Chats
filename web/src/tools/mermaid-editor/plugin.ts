import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * Mermaid Editor —— 独立 Cordis 工具插件（移植自 dev-tools/MermaidTool）。
 * 实时预览（防抖）、拖拽平移、滚轮/按钮缩放、SVG/PNG 导出；草稿仅存 localStorage。
 */
export class MermaidEditorToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.mermaid-editor')
    registerToolPlugin({
      key: 'mermaid-editor',
      defaultName: 'Mermaid Editor',
      defaultIcon: 'flow',
      nameKey: 'tools.mermaid.name',
      descriptionKey: 'tools.mermaid.desc',
      loader: () => import('./MermaidEditorView.vue'),
    })
  }
}
