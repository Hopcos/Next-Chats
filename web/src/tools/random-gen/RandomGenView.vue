<script setup lang="ts">
/** Random Generator 工具页（移植自 dev-tools，配置持久到 localStorage） */
import { reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'
import { copyText } from '@/utils/clipboard'
import { CHARSETS, loadConfig, saveConfig, secureSample } from './config'

const { t } = useI18n()

const config = reactive(loadConfig())
const results = ref<string[]>([])
const copiedIndex = ref<number | null>(null)

function persist() {
  saveConfig({ ...config })
}

function generate() {
  let charset = ''
  if (config.includeNumbers) charset += CHARSETS.numbers
  if (config.includeLowercase) charset += CHARSETS.lowercase
  if (config.includeUppercase) charset += CHARSETS.uppercase
  if (config.includeSpecial) charset += CHARSETS.special
  if (!charset) {
    kernel.notify.warning(t('tools.random.noCharset'))
    return
  }

  const min = Math.max(1, Math.min(config.minLength, config.maxLength))
  const max = Math.max(config.minLength, config.maxLength)
  const count = Math.min(100, Math.max(1, config.count))

  // 长度均匀分布同样走密码学随机
  const lenBuf = new Uint32Array(1)
  const next: string[] = []
  for (let i = 0; i < count; i++) {
    crypto.getRandomValues(lenBuf)
    const length = min + (lenBuf[0] % (max - min + 1))
    next.push(secureSample(charset, length))
  }
  results.value = next
  copiedIndex.value = null
  persist()
}

async function copyOne(text: string, index: number) {
  if (await copyText(text)) {
    copiedIndex.value = index
    setTimeout(() => (copiedIndex.value === index ? (copiedIndex.value = null) : null), 2000)
  } else {
    kernel.notify.error(t('tools.common.copyFailed'))
  }
}

async function copyAll() {
  if (results.value.length && (await copyText(results.value.join('\n')))) kernel.notify.success(t('tools.common.copied'))
}
</script>

<template>
  <div class="rg-page">
    <div class="rg-card">
      <h3 class="rg-title">{{ t('tools.random.charTypes') }}</h3>
      <div class="rg-checks">
        <el-checkbox v-model="config.includeNumbers" @change="persist">123 {{ t('tools.random.numbers') }}</el-checkbox>
        <el-checkbox v-model="config.includeLowercase" @change="persist">abc {{ t('tools.random.lowercase') }}</el-checkbox>
        <el-checkbox v-model="config.includeUppercase" @change="persist">ABC {{ t('tools.random.uppercase') }}</el-checkbox>
        <el-checkbox v-model="config.includeSpecial" @change="persist">!@#$ {{ t('tools.random.special') }}</el-checkbox>
      </div>

      <h3 class="rg-title">{{ t('tools.random.params') }}</h3>
      <div class="rg-fields">
        <label class="rg-field">
          <span>{{ t('tools.random.minLen') }}</span>
          <el-input-number v-model="config.minLength" :min="1" :max="128" size="small" @change="persist" />
        </label>
        <label class="rg-field">
          <span>{{ t('tools.random.maxLen') }}</span>
          <el-input-number v-model="config.maxLength" :min="1" :max="128" size="small" @change="persist" />
        </label>
        <label class="rg-field">
          <span>{{ t('tools.random.count') }}</span>
          <el-input-number v-model="config.count" :min="1" :max="100" size="small" @change="persist" />
        </label>
      </div>

      <el-button type="primary" class="rg-gen" @click="generate">
        <svg class="rg-ico" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M20 11A8 8 0 1 0 6.3 6.3L4 8.6" />
          <path d="M4 4v4.6h4.6" />
        </svg>
        {{ t('tools.random.generate') }}
      </el-button>
    </div>

    <div v-if="results.length" class="rg-results">
      <div class="rg-results-head">
        <span class="rg-results-title">{{ t('tools.random.results') }}</span>
        <button class="rg-copy-all" @click="copyAll">{{ t('tools.random.copyAll') }}</button>
      </div>
      <div v-for="(res, idx) in results" :key="idx" class="rg-item">
        <code class="rg-code">{{ res }}</code>
        <button class="rg-copy" :title="t('tools.common.copy')" @click="copyOne(res, idx)">
          <svg v-if="copiedIndex !== idx" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="9" y="9" width="11" height="11" rx="2" />
            <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
          </svg>
          <svg v-else viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="#10b981" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="m4.5 12.5 5 5 10-11" />
          </svg>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rg-page {
  flex: 1;
  overflow: auto;
  padding: 22px 26px;
  display: flex;
  flex-direction: column;
  gap: 18px;
  align-items: stretch;
  max-width: 880px;
  width: 100%;
  margin: 0 auto;
}

.rg-card {
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  border-radius: 14px;
  padding: 20px 22px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  box-shadow: 0 8px 26px rgba(2, 12, 27, 0.14);
}

.rg-title {
  margin: 0;
  font-size: 13px;
  font-weight: 700;
  color: var(--nc-text-dim, #8a94a6);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.rg-checks {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 6px 16px;
}

.rg-fields {
  display: flex;
  gap: 22px;
  flex-wrap: wrap;
}

.rg-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 12.5px;
  color: var(--nc-text-dim, #8a94a6);
}

.rg-gen {
  align-self: flex-start;
  border-radius: 10px;
  padding: 10px 20px;
}

.rg-ico {
  margin-right: 6px;
  vertical-align: -2px;
}

.rg-results {
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  border-radius: 14px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.rg-results-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 2px;
}

.rg-results-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--nc-text-dim, #8a94a6);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.rg-copy-all {
  border: 1px solid var(--nc-border);
  background: transparent;
  color: var(--nc-primary);
  font-size: 12px;
  border-radius: 7px;
  padding: 3px 10px;
  cursor: pointer;
}

.rg-copy-all:hover {
  background: color-mix(in srgb, var(--nc-primary) 10%, transparent);
}

.rg-item {
  display: flex;
  align-items: center;
  gap: 10px;
  background: rgba(148, 163, 184, 0.09);
  border-radius: 9px;
  padding: 9px 12px;
}

.rg-code {
  flex: 1;
  font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
  font-size: 13.5px;
  letter-spacing: 0.03em;
  word-break: break-all;
  color: var(--nc-text);
}

.rg-copy {
  border: 0;
  background: transparent;
  color: var(--nc-text-dim, #8a94a6);
  cursor: pointer;
  padding: 4px;
  border-radius: 6px;
  display: inline-flex;
}

.rg-copy:hover {
  color: var(--nc-primary);
  background: color-mix(in srgb, var(--nc-primary) 12%, transparent);
}
</style>
