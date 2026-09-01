/**
 * i18n 消息防回归检查：用与 vue-i18n v10 运行时完全一致的编译路径（jit + onError throw）
 * 扫描全部 locale 消息，捕获会导致生产渲染崩溃的非法消息语法。
 *
 * 背景：vue-i18n v10 在浏览器中运行时编译消息。消息里的裸 `@`（链接语法 @:key）或
 * 未转义的 `{...}` 都会抛 SyntaxError（如 INVALID_LINKED_FORMAT），表现为 "[vue] render error"。
 * 字面文本需转义：`@` → {'@'}；`{`/`}` → 整段用 {'...'} 包裹。
 *
 * 用法：node scripts/check-i18n-messages.cjs（或 npm run i18n:check）
 */
const fs = require('fs')
const path = require('path')
const { baseCompile } = require('@intlify/message-compiler')

let fails = 0
let total = 0
for (const d of ['zh', 'en']) {
  const dir = path.join(__dirname, '..', 'src', 'i18n', 'locales', d)
  for (const f of fs.readdirSync(dir).filter((x) => x.endsWith('.ts'))) {
    const src = fs.readFileSync(path.join(dir, f), 'utf8')
    for (const line of src.split(/\r?\n/)) {
      // 只编译对象值的字符串（'k': <value> 形式），跳过 key、import、注释等
      const re = /:\s*('((?:[^'\\]|\\.)*)'|"((?:[^"\\]|\\.)*)"|`((?:[^`\\]|\\.)*)`)/g
      let m
      while ((m = re.exec(line))) {
        const v = (m[2] ?? m[3] ?? m[4]).replace(/\\'/g, "'").replace(/\\"/g, '"').replace(/\\\\/g, '\\')
        total++
        try {
          baseCompile(v, {
            jit: true,
            location: false,
            optimize: true,
            onError: (err) => {
              throw err
            },
          })
        } catch (e) {
          fails++
          console.error(`FAIL ${d}/${f}: SyntaxError: ${e.code} (${e.message})\n  line: ${line.trim()}`)
        }
      }
    }
  }
}
console.log(`i18n message check: ${total} messages scanned, ${fails} invalid`)
process.exit(fails ? 1 : 0)
