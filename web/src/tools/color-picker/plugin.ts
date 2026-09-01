import { Service } from 'cordis'
import type { Context } from 'cordis'
import { registerToolPlugin } from '../registry'

/**
 * Color Picker —— 独立 Cordis 工具插件（移植自 dev-tools/ColorPicker 并增强）。
 * 原版仅支持 EyeDropper 屏幕取色，增强：色板/HEX 手动取色（无 EyeDropper 浏览器可用）、
 * HSL/RGB/HEX 三格式一键复制、WCAG 对比度实时校验、历史色板 localStorage 持久化。
 */
export class ColorPickerToolPlugin extends Service {
  constructor(ctx: Context) {
    super(ctx, 'tool.color-picker')
    registerToolPlugin({
      key: 'color-picker',
      defaultName: 'Color Picker',
      defaultIcon: 'eyedropper',
      nameKey: 'tools.color.name',
      descriptionKey: 'tools.color.desc',
      loader: () => import('./ColorPickerView.vue'),
    })
  }
}
