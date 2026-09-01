import MarkdownIt from 'markdown-it'

/** Markdown Preview 插件配置：渲染器 + 默认示例 + 本地状态键 */
export const DEFAULT_CONTENT = `# Hello Markdown

实时预览：**粗体**、~~删除线~~、\`行内代码\`、[链接](https://example.com)

## 列表

- 无序列表项
- [ ] 任务待办
- [x] 已完成任务

## 表格

| 功能 | 状态 |
| ---- | ---- |
| 表格 | ✅   |
| 代码块 | ✅ |

## 代码块

\`\`\`js
console.log('Hello Next Chats')
\`\`\`

> 引用：编辑左侧内容即可实时看到效果。
`

export const LS_KEY = 'nc.tool.md-preview.content'

/** 与聊天渲染一致的安全配置：不渲染原始 HTML（防 XSS） */
export const md = new MarkdownIt({ html: false, linkify: true, breaks: true })

export function loadContent(): string {
  try {
    return localStorage.getItem(LS_KEY) || DEFAULT_CONTENT
  } catch {
    return DEFAULT_CONTENT
  }
}

export function saveContent(v: string): void {
  try {
    localStorage.setItem(LS_KEY, v)
  } catch {
    /* ignore */
  }
}
