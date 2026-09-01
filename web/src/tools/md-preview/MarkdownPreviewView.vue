<script setup lang="ts">
/** Markdown Preview 工具页：左侧编辑、右侧实时渲染 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import SplitPane from '../components/SplitPane.vue'
import { loadContent, md, saveContent } from './config'

const { t } = useI18n()

const content = ref(loadContent())
const html = computed(() => md.render(content.value))

let timer: ReturnType<typeof setTimeout> | undefined
watch(content, (v) => {
  clearTimeout(timer)
  timer = setTimeout(() => saveContent(v), 400)
})
</script>

<template>
  <div class="mp-page">
    <SplitPane :left-title="t('tools.md.source')" :right-title="t('tools.md.preview')">
      <template #left>
        <textarea v-model="content" class="mp-area mono" spellcheck="false" :placeholder="t('tools.md.placeholder')" />
      </template>
      <template #right>
        <!-- markdown-it html:false，输出仅安全标签，无 XSS 风险 -->
        <div class="mp-preview nc-scroll" v-html="html" />
      </template>
    </SplitPane>
  </div>
</template>

<style scoped>
.mp-page {
  flex: 1;
  height: 100%;
  min-height: 0;
  display: flex;
  padding: 0; /* 满宽：原始 + 预览两边之和占满整个可视宽度 */
  overflow: hidden;
}

.mp-area {
  flex: 1;
  width: 100%;
  border: 0;
  outline: none;
  resize: none;
  background: #ffffff;
  color: #1e293b;
  padding: 14px 16px;
  font-size: 13px;
  line-height: 1.7;
}

.mono {
  font-family: 'JetBrains Mono', 'Cascadia Code', Consolas, Menlo, monospace;
}

.mp-area::placeholder {
  color: #94a3b8;
  opacity: 0.8;
}

.mp-preview {
  flex: 1;
  overflow: auto;
  background: #ffffff;
  padding: 16px 22px;
  font-size: 14px;
  line-height: 1.75;
  color: #1f2937;
  word-break: break-word;
}

.mp-preview :deep(h1),
.mp-preview :deep(h2),
.mp-preview :deep(h3) {
  margin: 18px 0 10px;
  font-weight: 700;
  line-height: 1.35;
}

.mp-preview :deep(h1) {
  font-size: 22px;
  padding-bottom: 8px;
  border-bottom: 1px solid #eef2f7;
}

.mp-preview :deep(h2) {
  font-size: 18px;
}

.mp-preview :deep(h3) {
  font-size: 15.5px;
}

.mp-preview :deep(p) {
  margin: 8px 0;
}

.mp-preview :deep(a) {
  color: #2563eb;
  text-decoration: none;
}

.mp-preview :deep(a:hover) {
  text-decoration: underline;
}

.mp-preview :deep(code) {
  font-family: Consolas, Menlo, monospace;
  font-size: 12.5px;
  background: rgba(148, 163, 184, 0.16);
  border-radius: 5px;
  padding: 2px 6px;
}

.mp-preview :deep(pre) {
  background: rgba(15, 23, 42, 0.85);
  color: #e2e8f0;
  border-radius: 10px;
  padding: 14px 16px;
  overflow: auto;
  margin: 12px 0;
}

.mp-preview :deep(pre code) {
  background: transparent;
  padding: 0;
  color: inherit;
  font-size: 12.5px;
  line-height: 1.6;
}

.mp-preview :deep(blockquote) {
  margin: 12px 0;
  padding: 6px 14px;
  border-left: 3px solid #2563eb;
  color: #64748b;
  background: #f1f5f9;
  border-radius: 0 8px 8px 0;
}

.mp-preview :deep(table) {
  border-collapse: collapse;
  margin: 12px 0;
  width: 100%;
  font-size: 13px;
}

.mp-preview :deep(th),
.mp-preview :deep(td) {
  border: 1px solid #e2e8f0;
  padding: 7px 12px;
  text-align: left;
}

.mp-preview :deep(th) {
  background: #f1f5f9;
  font-weight: 700;
}

.mp-preview :deep(ul),
.mp-preview :deep(ol) {
  padding-left: 22px;
  margin: 8px 0;
}

.mp-preview :deep(li) {
  margin: 3px 0;
}

.mp-preview :deep(hr) {
  border: 0;
  border-top: 1px solid var(--nc-border);
  margin: 16px 0;
}

.mp-preview :deep(img) {
  max-width: 100%;
  border-radius: 10px;
}

.mp-preview :deep(strong) {
  font-weight: 700;
}
</style>
