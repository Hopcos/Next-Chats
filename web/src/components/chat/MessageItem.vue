<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import MarkdownIt from 'markdown-it'
import mermaid from 'mermaid'
import type { UiMessage } from '@/kernel/plugins'
import ToolCard from '@/components/chat/ToolCard.vue'
import { kernel } from '@/kernel'

const props = defineProps<{ message: UiMessage }>()

const emit = defineEmits<{ regenerate: [messageId: string]; remove: [message: UiMessage] }>()

const { t } = useI18n()
const thinkingOpen = ref(false)

const isAssistant = computed(() => props.message.role === 'assistant')
const streamingNow = computed(() => props.message.status === 'sending')

// 话题操作按钮：仅在 assistant 回复就绪（非流式）时显示
const actionReady = computed(() => isAssistant.value && props.message.status !== 'sending')

async function copyContent() {
  const text = props.message.content || ''
  try {
    await navigator.clipboard.writeText(text)
    kernel.notify.success(t('chat.copied'))
  } catch {
    kernel.notify.warning(t('chat.copyFailed'))
  }
}

const statusText = computed<Record<string, string>>(() => ({
  sending: t('chat.thinking'),
  stopped: t('chat.stoppedShort'),
  failed: t('chat.failedShort'),
  complete: '',
}))

const showingThinking = computed(
  () => props.message.thinkingOpen || (props.message.status === 'sending' && !props.message.content),
)

// ------- 打字机（不依赖 Vue 批处理时序）：独立定时器逐字揭示 -------
// 每 32ms 无条件采样一次内容并向前推进游标。因此无论后端/事件是逐块到达、
// 一次性整批到达、还是组件挂载时内容已完整，都必然以“逐字输出”呈现；
// 仅历史/重放消息（live=false）跳过打字机直接全量。
const textShown = ref(0)
const thinkShown = ref(0)
let revealTimer: number | undefined
let revealSettled = false

const revealSpeed = 6 // 每 32ms 揭示 6 个字符（~190 字/秒）

function tick() {
  if (revealSettled) return
  const m = props.message
  if (!m.live) {
    textShown.value = m.content.length
    thinkShown.value = m.reasoning.length
    revealSettled = true
    return
  }
  if (textShown.value < m.content.length) textShown.value = Math.min(m.content.length, textShown.value + revealSpeed)
  if (thinkShown.value < m.reasoning.length) thinkShown.value = Math.min(m.reasoning.length, thinkShown.value + revealSpeed)
}

revealTimer = window.setInterval(tick, 32)

onUnmounted(() => {
  window.clearInterval(revealTimer)
})

// 首 token 等待反馈（真实模型 TTFT 可能很长）：显示“思考中…N秒”
const waitSec = ref(0)
let waitTimer: number | undefined

watch(
  () => [props.message.status, props.message.content.length] as const,
  ([status, len]) => {
    if (status === 'sending' && len === 0) {
      waitSec.value = 0
      window.clearInterval(waitTimer)
      waitTimer = window.setInterval(() => waitSec.value++, 1000)
    } else {
      window.clearInterval(waitTimer)
    }
  },
  { immediate: true },
)

const showWait = computed(() => props.message.status === 'sending' && !props.message.content)

// 思考：进行中自动展开（完整展示全部推理）；结束后自动折叠回标题（点击可重新展开查看全部）
watch(
  () => props.message.thinkingOpen,
  (v) => {
    thinkingOpen.value = v
  },
)

// 思考进行中实时滚动到底（保证“所有思考过程”持续可见）
const thinkBodyRef = ref<HTMLElement | null>(null)
watch(
  () => [props.message.thinkingOpen, props.message.reasoning.length] as const,
  () => {
    if (props.message.thinkingOpen) {
      void nextTick(() => {
        const el = thinkBodyRef.value
        if (el) el.scrollTop = el.scrollHeight
      })
    }
  },
  { flush: 'post' },
)

// ---------------- Markdown + Mermaid 渲染 ----------------
// 打字机揭示期间显示纯文本（避免半截语法闪烁）；揭示完成后一次性渲染 Markdown，
// Mermaid 代码块经自定义 fence 输出 <pre class="mermaid">，由 mermaid.run 转成 SVG。

const md = new MarkdownIt({
  html: false, // 不渲染原始 HTML，防 XSS
  linkify: true,
  breaks: true,
})

const defaultLinkOpen =
  md.renderer.rules.link_open ??
  ((tokens, idx, options, _env, self) => self.renderToken(tokens, idx, options))
md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
  tokens[idx].attrSet('target', '_blank')
  tokens[idx].attrSet('rel', 'noopener noreferrer')
  return defaultLinkOpen(tokens, idx, options, env, self)
}

const defaultFence = md.renderer.rules.fence
md.renderer.rules.fence = (tokens, idx, options, env, self) => {
  const tok = tokens[idx]
  const info = (tok.info ?? '').trim().toLowerCase()
  if (info === 'mermaid' || info.startsWith('mermaid ')) {
    return `<pre class="mermaid">${md.utils.escapeHtml(tok.content)}</pre>\n`
  }
  return defaultFence ? defaultFence(tokens, idx, options, env, self) : self.renderToken(tokens, idx, options)
}

mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })

const mdHtml = ref('')
const mdReady = ref(false) // 揭示完成、Markdown 已渲染
const contentRef = ref<HTMLElement | null>(null)

const shownContent = computed(() => props.message.content.slice(0, textShown.value))

watch(
  () => [shownContent.value, props.message.content] as const,
  async () => {
    const full = props.message.content
    const typing = props.message.live && textShown.value < full.length
    if (typing) {
      mdReady.value = false
      return
    }
    mdHtml.value = md.render(full)
    mdReady.value = true
    await nextTick()
    void renderMermaid()
  },
  { flush: 'post' },
)

async function renderMermaid() {
  const host = contentRef.value
  if (!host) return
  const blocks = Array.from(host.querySelectorAll('pre.mermaid')) as HTMLElement[]
  if (!blocks.length) return
  try {
    await mermaid.run({ nodes: blocks, suppressErrors: true })
  } catch {
    // 渲染失败保留源码文本，不影响消息
  }
}

function prettyArgs(raw?: string): string {
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
</script>

<template>
  <!-- user 话题锚点（话题导航条定位用）：topic- + 消息 id -->
  <div
    class="row"
    :class="message.role"
    :key="message.id"
    :id="message.role === 'user' ? 'topic-' + message.id : undefined"
  >
    <div v-if="isAssistant" class="avatar-mini">NC</div>
    <div class="body">
      <!-- 思考（可折叠）：浅灰背景与最终输出区分；折叠是收起动画，内容始终保留可展开 -->
      <div
        v-if="isAssistant && (message.reasoning || message.thinkingOpen || message.tools.length > 0 || (message.status === 'sending' && !message.content))"
        class="think-block"
      >
        <div class="think-head nc-dim" @click="thinkingOpen = !thinkingOpen">
          <span :class="['caret', { open: thinkingOpen }]">▸</span>
          <span v-if="showingThinking">
            🧠 {{ t('chat.thinkInProgress') }}
            <template v-if="showWait"> · {{ waitSec }}s</template>
          </span>
          <span v-else-if="message.reasoning">{{ t('chat.thinkProcess', { len: message.reasoning.length }) }}</span>
          <span v-else>{{ t('chat.thinkEmpty') }}</span>
        </div>
        <div class="think-body-wrap" :class="{ collapsed: !thinkingOpen && !message.thinkingOpen }">
          <div v-if="message.reasoning" ref="thinkBodyRef" class="think-body">{{ message.reasoning.slice(0, thinkShown) }}</div>
          <div v-else-if="thinkingOpen || message.thinkingOpen" class="think-body nc-dim think-wait">{{ t('chat.thinkingWait') }}</div>
        </div>
        <ToolCard v-for="tCard in message.tools" :key="tCard.key" :card="tCard" />
        <div v-for="(note, i) in message.contextNotes" :key="'n' + i" class="context-note nc-dim">
          ℹ️ {{ note }}
        </div>
      </div>

      <!-- 用户附件图片 -->
      <div v-if="!isAssistant && message.images && message.images.length" class="images">
        <img
          v-for="(img, i) in message.images"
          :key="i"
          class="msg-image"
          :src="'data:' + (img.mimeType || 'image/png') + ';base64,' + img.base64"
          :alt="img.fileName || ''"
        />
      </div>

      <!-- 正文（打字机揭示 → Markdown + Mermaid） -->
      <div v-if="message.content || message.status !== 'sending'" class="bubble" :class="{ streaming: streamingNow }">
        <template v-if="message.content && !mdReady"><div class="plain-text">{{ shownContent }}</div></template>
        <div v-else-if="mdReady" ref="contentRef" class="md" v-html="mdHtml"></div>
        <span v-else-if="streamingNow" class="skeleton">▍</span>
        <span v-else-if="message.status === 'stopped'" class="nc-dim">{{ t('chat.stoppedNote') }}</span>
        <span v-else-if="message.status === 'failed'" class="nc-dim">{{ t('chat.failedNote') }}</span>
      </div>

      <!-- 用量/模型信息 -->
      <div v-if="isAssistant && message.usage" class="usage nc-dim">
        {{ t('chat.usageTokens', { model: message.model ?? '', tokens: message.usage.totalTokens }) }}
        <template v-if="message.usage.ttftMs > 0">{{ t('chat.usageTtft', { ms: message.usage.ttftMs }) }}</template>
        <template v-if="message.usage.totalMs > 0">{{ t('chat.usageTotalMs', { ms: message.usage.totalMs }) }}</template>
      </div>

      <!-- 话题操作：复制 / 重新生成 / 删除（assistant 回复就绪后显示） -->
      <div v-if="actionReady" class="actions">
        <button class="act nc-dim" :title="t('chat.copy')" @click="copyContent">📋 {{ t('chat.copy') }}</button>
        <button class="act nc-dim" :title="t('chat.regenerate')" @click="emit('regenerate', message.id)">🔄 {{ t('chat.regenerate') }}</button>
        <button class="act nc-dim danger" :title="t('common.delete')" @click="emit('remove', message)">🗑 {{ t('common.delete') }}</button>
      </div>
    </div>
    <div v-if="!isAssistant && statusText[message.status]" class="status nc-dim">
      {{ statusText[message.status] }}
    </div>
  </div>
</template>

<style scoped>
.row {
  display: flex;
  gap: 8px;
  margin-bottom: 4px;
  align-items: flex-start;
}

.row.user {
  flex-direction: row-reverse;
}

.avatar-mini {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  flex-shrink: 0;
  background: linear-gradient(135deg, var(--nc-primary), #a78bfa);
  color: #04121f;
  font-size: 10px;
  font-weight: 800;
  display: flex;
  align-items: center;
  justify-content: center;
}

.body {
  max-width: 78%;
  min-width: 120px;
  /* 行距随 .md 容器（1.15）；plain-text 打字机阶段继承此值 */
  line-height: 1.15;
}

.row.user .body {
  max-width: 70%;
}

.bubble.streaming {
  border-color: color-mix(in srgb, var(--nc-primary) 55%, transparent);
}

.images {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 4px;
  justify-content: flex-end;
}

.msg-image {
  max-width: 220px;
  max-height: 220px;
  border-radius: 8px;
  border: 1px solid var(--nc-border);
  object-fit: cover;
}

.status {
  font-size: 12px;
  margin-top: 4px;
}

/* ---- 思考区：浅灰背景（与最终输出区分），折叠为收起动画，内容始终在 DOM ---- */
.think-block {
  margin-bottom: 6px;
  border: 1px solid var(--nc-border);
  border-radius: 8px;
  padding: 4px 10px;
  background: color-mix(in srgb, var(--nc-text-dim) 12%, transparent);
}

.think-head {
  font-size: 12.5px;
  cursor: pointer;
  user-select: none;
  display: flex;
  gap: 6px;
  align-items: center;
}

.caret {
  transition: transform 0.15s;
  display: inline-block;
}

.caret.open {
  transform: rotate(90deg);
}

.think-body-wrap {
  max-height: 420px;
  overflow: hidden;
  transition: max-height 0.25s ease;
}

.think-body-wrap.collapsed {
  max-height: 0;
}

.think-body {
  margin-top: 4px;
  font-size: 13px;
  line-height: 1.15;
  white-space: pre-wrap;
  opacity: 0.82;
  max-height: 320px;
  overflow-y: auto;
  padding: 6px 10px;
  border-left: 2px solid color-mix(in srgb, var(--nc-primary) 40%, transparent);
  background: color-mix(in srgb, var(--nc-bg) 30%, transparent);
}

.context-note {
  font-size: 11.5px;
  margin-top: 3px;
}

.skeleton {
  animation: blink 1s infinite;
  color: var(--nc-primary);
}

@keyframes blink {
  50% {
    opacity: 0.2;
  }
}

.usage {
  font-size: 11px;
  margin-top: 4px;
}

/* ---- 话题操作按钮（hover 显示） ---- */
.actions {
  margin-top: 5px;
  opacity: 0;
  transition: opacity 0.15s;
  display: flex;
  gap: 2px;
}

.body:hover .actions,
.row:focus-within .actions {
  opacity: 1;
}

.act {
  border: none;
  background: transparent;
  cursor: pointer;
  padding: 2px 8px;
  font-size: 12px;
  border-radius: 6px;
  color: var(--nc-dim, var(--nc-text-dim));
  transition: background 0.15s, color 0.15s;
}

.act:hover {
  background: color-mix(in srgb, var(--nc-text-dim) 15%, transparent);
  color: var(--nc-text);
}

.act.danger:hover {
  color: var(--nc-danger, #f56c6c);
}

/* ---- 打字机阶段的纯文本 ---- */
.plain-text {
  white-space: pre-wrap;
}
</style>

<!--
  全局（非 scoped）Markdown 内容样式：
  v-html 渲染的内容不在组件 scoped 作用域内，scoped + :deep() 的覆盖不可靠，
  这里统一用全局类 .md 直接控制，行距压到 1.3（密集排版）。
-->
<style>
/* 行距唯一事实源 = .md 容器（继承给所有子元素），子元素不再单独设 line-height，
   避免任何子级规则被跳过/覆盖导致行距不一致；!important 免疫全局样式 */
.md {
  line-height: 1.1 !important;
  font-size: 13.5px;
}

.md p {
  margin: 0.08em 0 !important;
}

.md li {
  margin: 0.02em 0 !important;
}

.md ol,
.md ul {
  margin: 0.08em 0 !important;
  padding-left: 1.25em;
}

.md h1,
.md h2,
.md h3,
.md h4 {
  margin: 0.26em 0 0.12em !important;
}

.md h1 { font-size: 1.38em; }
.md h2 { font-size: 1.22em; }
.md h3 { font-size: 1.08em; }
.md h4 { font-size: 1em; }

.md a {
  color: var(--nc-primary);
}

.md hr {
  border: none;
  border-top: 1px solid var(--nc-border);
  margin: 0.4em 0 !important;
}

.md img {
  max-width: 100%;
  border-radius: 8px;
}

.md blockquote {
  border-left: 3px solid var(--nc-primary);
  margin: 0.2em 0 !important;
  padding-left: 10px;
  color: var(--nc-text-dim);
}

.md table {
  border-collapse: collapse;
  margin: 0.2em 0 !important;
}

.md th,
.md td {
  border: 1px solid var(--nc-border);
  padding: 1px 7px;
}

.md code {
  font-family: 'JetBrains Mono', ui-monospace, 'Cascadia Code', monospace;
  font-size: 0.9em;
}

.md :not(pre) > code {
  background: color-mix(in srgb, var(--nc-text-dim) 18%, transparent);
  padding: 0 4px;
  border-radius: 4px;
}

.md pre {
  background: color-mix(in srgb, var(--nc-text-dim) 10%, transparent);
  border: 1px solid var(--nc-border);
  border-radius: 8px;
  padding: 8px 10px;
  overflow-x: auto;
  margin: 0.3em 0 !important;
}

/* Mermaid 图：居中、自适应宽度 */
.md pre.mermaid {
  background: transparent;
  border: none;
  text-align: center;
  padding: 4px 0;
}

.md pre.mermaid svg {
  max-width: 100%;
  height: auto;
  margin: 0 auto;
}
</style>
