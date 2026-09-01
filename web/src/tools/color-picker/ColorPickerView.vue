<script setup lang="ts">
/**
 * Color Picker 工具页（移植自 dev-tools 并增强）：
 * EyeDropper 屏幕取色（Alt+C）+ 原生色板 + HEX 手输，HEX/RGB/HSL 三格式复制，
 * WCAG 对比度实时校验，历史色板 localStorage 持久化。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'
import { copyText } from '@/utils/clipboard'
import {
  LS,
  MAX_HISTORY,
  contrastRatio,
  hexToRgb,
  isValidHex,
  loadHistory,
  rgbToHex,
  rgbToHsl,
  saveHistory,
} from './config'

const { t } = useI18n()

type EyeDropperLike = { open: () => Promise<{ sRGBHex: string }> }

const current = ref(normalize(localStorage.getItem(LS.current) || '#6366F1'))
const hexInput = ref(current.value)
const history = ref<string[]>(loadHistory())
const supported = ref(true)

function normalize(hex: string): string {
  const rgb = hexToRgb(hex)
  return rgb ? rgbToHex(...rgb) : '#6366F1'
}

const rgb = computed<[number, number, number]>(() => hexToRgb(current.value) ?? [99, 102, 241])
const rgbText = computed(() => `rgb(${rgb.value.join(', ')})`)
const hslText = computed(() => {
  const [h, s, l] = rgbToHsl(...rgb.value)
  return `hsl(${h}, ${s}%, ${l}%)`
})
/** 原生 <input type=color> 需要 #rrggbb 小写 */
const nativeColor = computed(() => current.value.toLowerCase())

const onWhite = computed(() => contrastRatio(current.value, [255, 255, 255]))
const onBlack = computed(() => contrastRatio(current.value, [0, 0, 0]))

function ratioBadge(r: number): { level: string; cls: string } {
  if (r >= 7) return { level: 'AAA', cls: 'pass-aaa' }
  if (r >= 4.5) return { level: 'AA', cls: 'pass-aa' }
  if (r >= 3) return { level: 'AA-LG', cls: 'pass-aa-lg' }
  return { level: 'FAIL', cls: 'fail' }
}

function applyColor(hex: string, addToHistory = true) {
  const n = normalize(hex)
  current.value = n
  hexInput.value = n
  try {
    localStorage.setItem(LS.current, n)
  } catch {
    /* ignore */
  }
  if (addToHistory) {
    const rest = history.value.filter((h) => h.toUpperCase() !== n)
    history.value = [n, ...rest].slice(0, MAX_HISTORY)
    saveHistory(history.value)
  }
}

function onHexInput(v: string) {
  hexInput.value = v
  if (isValidHex(v)) applyColor(v)
}

function onNativePick(e: Event) {
  applyColor((e.target as HTMLInputElement).value)
}

async function pickFromScreen() {
  const Ctor = (window as unknown as { EyeDropper?: new () => EyeDropperLike }).EyeDropper
  if (!Ctor) {
    supported.value = false
    kernel.notify.warning(t('tools.color.noEyeDropper'))
    return
  }
  try {
    const result = await new Ctor().open()
    applyColor(result.sRGBHex)
  } catch {
    /* 用户取消取色：静默 */
  }
}

function onKey(e: KeyboardEvent) {
  if (e.altKey && e.key.toLowerCase() === 'c') {
    e.preventDefault()
    void pickFromScreen()
  }
}

onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))

async function copy(text: string, label: string) {
  if (await copyText(text)) kernel.notify.success(t('tools.color.copiedFmt', { fmt: label }))
  else kernel.notify.error(t('tools.common.copyFailed'))
}

function removeHistory(hex: string) {
  history.value = history.value.filter((h) => h !== hex)
  saveHistory(history.value)
}

function clearHistory() {
  history.value = []
  saveHistory([])
}
</script>

<template>
  <div class="cp-page">
    <div class="cp-main">
      <!-- 大色块预览：白/黑文字样本 + 对比度 -->
      <div class="cp-preview" :style="{ background: current }">
        <div class="cp-preview-light">
          <span class="cp-aa" style="color: #ffffff">Aa</span>
          <span class="cp-ratio" style="color: #ffffff">{{ onWhite.toFixed(2) }}:1</span>
        </div>
        <div class="cp-preview-dark">
          <span class="cp-aa" style="color: #000000">Aa</span>
          <span class="cp-ratio" style="color: #000000">{{ onBlack.toFixed(2) }}:1</span>
        </div>
      </div>

      <div class="cp-controls">
        <el-button type="primary" size="large" class="cp-pick" @click="pickFromScreen">
          <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="m14.5 3.8 5.7 5.7-2 2-1.2-1.2-7.4 7.4-.3 3-2.7-2.7 3-.3 7.4-7.4-1.2-1.2 2-2Z" />
            <path d="M4 20.5 6.3 18.2" />
          </svg>
          {{ t('tools.color.pick') }} <kbd class="cp-kbd">Alt + C</kbd>
        </el-button>

        <label class="cp-swatch" :title="t('tools.color.palette')">
          <input type="color" :value="nativeColor" @input="onNativePick" />
          <span class="cp-swatch-face" :style="{ background: current }" />
          <span class="cp-swatch-label">{{ t('tools.color.palette') }}</span>
        </label>

        <div class="cp-hex">
          <span class="cp-hex-label">HEX</span>
          <el-input v-model="hexInput" size="large" class="cp-hex-input" @input="onHexInput" />
          <span v-if="!isValidHex(hexInput)" class="cp-hex-bad">{{ t('tools.color.invalidHex') }}</span>
        </div>
      </div>

      <!-- 格式行：点击复制 -->
      <div class="cp-formats">
        <button class="cp-fmt" @click="copy(current, 'HEX')">
          <span class="cp-fmt-key">HEX</span><code>{{ current }}</code><span class="cp-fmt-copy">{{ t('tools.common.copy') }}</span>
        </button>
        <button class="cp-fmt" @click="copy(rgbText, 'RGB')">
          <span class="cp-fmt-key">RGB</span><code>{{ rgbText }}</code><span class="cp-fmt-copy">{{ t('tools.common.copy') }}</span>
        </button>
        <button class="cp-fmt" @click="copy(hslText, 'HSL')">
          <span class="cp-fmt-key">HSL</span><code>{{ hslText }}</code><span class="cp-fmt-copy">{{ t('tools.common.copy') }}</span>
        </button>
      </div>

      <!-- WCAG -->
      <div class="cp-wcag">
        <span class="cp-wcag-title">{{ t('tools.color.wcag') }}</span>
        <span class="cp-wcag-item">
          <i class="cp-chip white" /> vs #FFF
          <b :class="['cp-badge', ratioBadge(onWhite).cls]">{{ ratioBadge(onWhite).level }}</b>
          <em>{{ onWhite.toFixed(2) }}:1</em>
        </span>
        <span class="cp-wcag-item">
          <i class="cp-chip black" /> vs #000
          <b :class="['cp-badge', ratioBadge(onBlack).cls]">{{ ratioBadge(onBlack).level }}</b>
          <em>{{ onBlack.toFixed(2) }}:1</em>
        </span>
      </div>

      <p v-if="!supported" class="cp-note">{{ t('tools.color.noEyeDropper') }}</p>
    </div>

    <!-- 历史 -->
    <aside class="cp-history">
      <div class="cp-history-head">
        <span>{{ t('tools.color.history') }}</span>
        <button v-if="history.length" class="cp-clear" @click="clearHistory">{{ t('tools.color.clearHistory') }}</button>
      </div>
      <div v-if="history.length" class="cp-history-grid">
        <div
          v-for="hex in history"
          :key="hex"
          class="cp-hcell"
          :style="{ background: hex }"
          :title="`${hex} — ${t('tools.color.pickFromHistory')}`"
          @click="applyColor(hex, false)"
        >
          <button class="cp-hdel" :title="t('tools.common.delete')" @click.stop="removeHistory(hex)">×</button>
        </div>
      </div>
      <div v-else class="cp-history-empty">{{ t('tools.color.historyEmpty') }}</div>
    </aside>
  </div>
</template>

<style scoped>
.cp-page {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: 20px;
  padding: 22px 26px;
  align-content: start;
}

.cp-main {
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-width: 0;
}

.cp-preview {
  height: 170px;
  border-radius: 16px;
  border: 1px solid var(--nc-border);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 44px;
  box-shadow: 0 10px 30px rgba(2, 12, 27, 0.2);
  transition: background 0.2s;
}

.cp-preview-light,
.cp-preview-dark {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.cp-aa {
  font-size: 44px;
  font-weight: 800;
  line-height: 1;
}

.cp-ratio {
  font-size: 12px;
  opacity: 0.85;
}

.cp-controls {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-wrap: wrap;
}

.cp-pick {
  border-radius: 12px;
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.cp-kbd {
  background: rgba(255, 255, 255, 0.18);
  border-radius: 5px;
  padding: 1px 6px;
  font-size: 11px;
  font-family: inherit;
}

.cp-swatch {
  position: relative;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  border: 1px solid var(--nc-border);
  border-radius: 12px;
  padding: 8px 14px;
  background: var(--nc-surface);
}

.cp-swatch:hover {
  border-color: var(--nc-primary);
}

.cp-swatch input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.cp-swatch-face {
  width: 30px;
  height: 30px;
  border-radius: 8px;
  border: 1px solid rgba(128, 128, 128, 0.35);
}

.cp-swatch-label {
  font-size: 11px;
  color: var(--nc-text-dim, #8a94a6);
}

.cp-hex {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.cp-hex-label {
  font-size: 12px;
  font-weight: 700;
  color: var(--nc-text-dim, #8a94a6);
}

.cp-hex-input {
  width: 130px;
}

.cp-hex-bad {
  font-size: 12px;
  color: #e11d48;
}

.cp-formats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 10px;
}

.cp-fmt {
  display: flex;
  align-items: center;
  gap: 10px;
  border: 1px solid var(--nc-border);
  border-radius: 10px;
  background: var(--nc-surface);
  padding: 10px 12px;
  cursor: pointer;
  color: var(--nc-text);
  transition: all 0.15s;
  text-align: left;
}

.cp-fmt:hover {
  border-color: var(--nc-primary);
}

.cp-fmt code {
  flex: 1;
  font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
  font-size: 12.5px;
  word-break: break-all;
}

.cp-fmt-key {
  font-size: 11px;
  font-weight: 800;
  color: var(--nc-primary);
  background: color-mix(in srgb, var(--nc-primary) 12%, transparent);
  border-radius: 6px;
  padding: 2px 7px;
}

.cp-fmt-copy {
  font-size: 11.5px;
  color: var(--nc-text-dim, #8a94a6);
  flex-shrink: 0;
}

.cp-wcag {
  display: flex;
  align-items: center;
  gap: 18px;
  flex-wrap: wrap;
  border: 1px dashed var(--nc-border);
  border-radius: 12px;
  padding: 10px 14px;
}

.cp-wcag-title {
  font-size: 12px;
  font-weight: 700;
  color: var(--nc-text-dim, #8a94a6);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.cp-wcag-item {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  font-size: 12.5px;
  color: var(--nc-text-dim, #8a94a6);
}

.cp-wcag-item em {
  font-style: normal;
  color: var(--nc-text);
  font-family: Consolas, monospace;
}

.cp-chip {
  width: 12px;
  height: 12px;
  border-radius: 4px;
  border: 1px solid var(--nc-border);
}

.cp-chip.white {
  background: #fff;
}

.cp-chip.black {
  background: #000;
}

.cp-badge {
  font-size: 10.5px;
  font-weight: 800;
  border-radius: 5px;
  padding: 1px 6px;
}

.cp-badge.pass-aaa {
  color: #052e16;
  background: #4ade80;
}

.cp-badge.pass-aa {
  color: #14532d;
  background: #bbf7d0;
}

.cp-badge.pass-aa-lg {
  color: #713f12;
  background: #fde68a;
}

.cp-badge.fail {
  color: #fff;
  background: #e11d48;
}

.cp-note {
  margin: 0;
  font-size: 12px;
  color: #d97706;
}

.cp-history {
  border: 1px solid var(--nc-border);
  border-radius: 14px;
  background: var(--nc-surface);
  padding: 14px;
  align-self: start;
  max-height: calc(100vh - 160px);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.cp-history-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
  font-weight: 700;
}

.cp-clear {
  border: 0;
  background: transparent;
  color: #e11d48;
  font-size: 12px;
  cursor: pointer;
}

.cp-history-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 8px;
  overflow: auto;
}

.cp-hcell {
  position: relative;
  aspect-ratio: 1;
  border-radius: 10px;
  border: 1px solid rgba(128, 128, 128, 0.3);
  cursor: pointer;
  transition: transform 0.12s;
}

.cp-hcell:hover {
  transform: scale(1.06);
}

.cp-hdel {
  position: absolute;
  top: -6px;
  right: -6px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  border: 0;
  background: #0f172a;
  color: #fff;
  font-size: 11px;
  line-height: 1;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.12s;
}

.cp-hcell:hover .cp-hdel {
  opacity: 1;
}

.cp-history-empty {
  font-size: 12.5px;
  color: var(--nc-text-dim, #8a94a6);
  text-align: center;
  padding: 24px 0;
}

@media (max-width: 960px) {
  .cp-page {
    grid-template-columns: 1fr;
  }
}
</style>
