/**
 * prettier 3 部分插件（yaml/graphql 等）未随包提供 .d.mts 类型，
 * 此处提供通配声明以通过 vue-tsc（仅在无官方声明时生效）。
 * 动态 import 得到的是模块命名空间，传给 prettier standalone 的可迭代插件集合。
 */
declare module 'prettier/plugins/*' {
  const languages: unknown
  const parsers: unknown
  const printers: unknown
  const options: unknown
  const plugin: Record<string, unknown>
  export default plugin
}
