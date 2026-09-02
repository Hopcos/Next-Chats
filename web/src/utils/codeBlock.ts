import hljs from 'highlight.js/lib/core'
import javascript from 'highlight.js/lib/languages/javascript'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import css from 'highlight.js/lib/languages/css'
import scss from 'highlight.js/lib/languages/scss'
import less from 'highlight.js/lib/languages/less'
import json from 'highlight.js/lib/languages/json'
import markdown from 'highlight.js/lib/languages/markdown'
import bash from 'highlight.js/lib/languages/bash'
import shell from 'highlight.js/lib/languages/shell'
import powershell from 'highlight.js/lib/languages/powershell'
import sql from 'highlight.js/lib/languages/sql'
import python from 'highlight.js/lib/languages/python'
import go from 'highlight.js/lib/languages/go'
import java from 'highlight.js/lib/languages/java'
import c from 'highlight.js/lib/languages/c'
import cpp from 'highlight.js/lib/languages/cpp'
import csharp from 'highlight.js/lib/languages/csharp'
import rust from 'highlight.js/lib/languages/rust'
import ruby from 'highlight.js/lib/languages/ruby'
import php from 'highlight.js/lib/languages/php'
import yaml from 'highlight.js/lib/languages/yaml'
import ini from 'highlight.js/lib/languages/ini'
import dockerfile from 'highlight.js/lib/languages/dockerfile'
import diff from 'highlight.js/lib/languages/diff'
import kotlin from 'highlight.js/lib/languages/kotlin'
import plaintext from 'highlight.js/lib/languages/plaintext'

hljs.registerLanguage('javascript', javascript)
hljs.registerLanguage('typescript', typescript)
hljs.registerLanguage('xml', xml) // html/xml/vue 模板
hljs.registerLanguage('css', css)
hljs.registerLanguage('scss', scss)
hljs.registerLanguage('less', less)
hljs.registerLanguage('json', json)
hljs.registerLanguage('markdown', markdown)
hljs.registerLanguage('bash', bash)
hljs.registerLanguage('shell', shell)
hljs.registerLanguage('powershell', powershell)
hljs.registerLanguage('sql', sql)
hljs.registerLanguage('python', python)
hljs.registerLanguage('go', go)
hljs.registerLanguage('java', java)
hljs.registerLanguage('c', c)
hljs.registerLanguage('cpp', cpp)
hljs.registerLanguage('csharp', csharp)
hljs.registerLanguage('rust', rust)
hljs.registerLanguage('ruby', ruby)
hljs.registerLanguage('php', php)
hljs.registerLanguage('yaml', yaml)
hljs.registerLanguage('ini', ini)
hljs.registerLanguage('dockerfile', dockerfile)
hljs.registerLanguage('diff', diff)
hljs.registerLanguage('kotlin', kotlin)
hljs.registerLanguage('plaintext', plaintext)

export { hljs }

/** 未知/不支持语言时的安全回退 */
function escapeHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}

/**
 * 按语言高亮代码（markdown fence 内容）。
 * - 语言已知：hljs.highlight（ignoreIllegals 避免非法内容抛错）
 * - 语言缺失/未知：自动探测，探测无把握则原样转义返回
 */
export function highlightCode(code: string, lang?: string): string {
  const l = (lang ?? '').trim().toLowerCase()
  if (l) {
    if (hljs.getLanguage(l)) {
      try {
        return hljs.highlight(code, { language: l, ignoreIllegals: true }).value
      } catch {
        /* fallthrough */
      }
    }
  }
  try {
    const r = hljs.highlightAuto(code)
    if (r.language && r.relevance > 0) return r.value
  } catch {
    /* fallthrough */
  }
  return escapeHtml(code)
}

/** 语言名 → prettier parser 插件组合（仅官方 standalone 支持的语言可格式化；estree 为 JS 系 printer） */
const FORMAT_SPEC: Record<string, { parser: string; load: (() => Promise<Record<string, unknown>>)[] }> = {
  javascript: { parser: 'babel', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  js: { parser: 'babel', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  jsx: { parser: 'babel', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  mjs: { parser: 'babel', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  cjs: { parser: 'babel', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  typescript: { parser: 'typescript', load: [() => import('prettier/plugins/typescript.mjs'), () => import('prettier/plugins/estree.mjs')] },
  ts: { parser: 'typescript', load: [() => import('prettier/plugins/typescript.mjs'), () => import('prettier/plugins/estree.mjs')] },
  tsx: { parser: 'typescript', load: [() => import('prettier/plugins/typescript.mjs'), () => import('prettier/plugins/estree.mjs')] },
  json: { parser: 'json', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  jsonc: { parser: 'json', load: [() => import('prettier/plugins/babel.mjs'), () => import('prettier/plugins/estree.mjs')] },
  html: { parser: 'html', load: [() => import('prettier/plugins/html.mjs')] },
  vue: { parser: 'vue', load: [() => import('prettier/plugins/html.mjs')] },
  css: { parser: 'css', load: [() => import('prettier/plugins/postcss.mjs')] },
  scss: { parser: 'scss', load: [() => import('prettier/plugins/postcss.mjs')] },
  less: { parser: 'less', load: [() => import('prettier/plugins/postcss.mjs')] },
  md: { parser: 'markdown', load: [() => import('prettier/plugins/markdown.mjs')] },
  markdown: { parser: 'markdown', load: [() => import('prettier/plugins/markdown.mjs')] },
  yaml: { parser: 'yaml', load: [() => import('prettier/plugins/yaml.mjs')] },
  yml: { parser: 'yaml', load: [() => import('prettier/plugins/yaml.mjs')] },
  graphql: { parser: 'graphql', load: [() => import('prettier/plugins/graphql.mjs')] },
}

export function canFormat(lang: string): boolean {
  return lang.toLowerCase() in FORMAT_SPEC
}

/**
 * 按语言格式化代码（prettier standalone，parser 按需懒加载）。
 * 不支持的语言抛错（由调用方提示）。
 */
export async function formatCode(lang: string, code: string): Promise<string> {
  const spec = FORMAT_SPEC[lang.toLowerCase()]
  if (!spec) throw new Error('UNSUPPORTED_LANGUAGE')
  const [prettierNS, ...pluginMods] = await Promise.all([import('prettier/standalone'), ...spec.load.map((f) => f())])
  const prettier = prettierNS as unknown as {
    format(source: string, opts: Record<string, unknown>): Promise<string>
  }
  return await prettier.format(code, {
    parser: spec.parser,
    plugins: pluginMods as unknown[],
    semi: false,
    singleQuote: true,
    printWidth: 100,
    tabWidth: 2,
  })
}
