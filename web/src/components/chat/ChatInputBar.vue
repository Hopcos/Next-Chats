<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'
import { http } from '@/api/http'

const { t } = useI18n()
const text = ref('')

// ---- 思考模式：全局开关（默认启用）+ 强度（默认 high），状态持久化到 localStorage ----
const LS_THINKING_ENABLED = 'nc.thinking.enabled'
const LS_THINKING_EFFORT = 'nc.thinking.effort'

const thinkingEnabled = ref(localStorage.getItem(LS_THINKING_ENABLED) !== '0')
const thinkingEffort = ref(localStorage.getItem(LS_THINKING_EFFORT) || 'high')

const effortOptions = [
  { value: 'low', labelKey: 'chat.effortLow' },
  { value: 'medium', labelKey: 'chat.effortMedium' },
  { value: 'high', labelKey: 'chat.effortHigh' },
  { value: 'max', labelKey: 'chat.effortMax' },
]

function saveThinking() {
  localStorage.setItem(LS_THINKING_ENABLED, thinkingEnabled.value ? '1' : '0')
  localStorage.setItem(LS_THINKING_EFFORT, thinkingEffort.value)
}

const streaming = computed(() => kernel.chat.state.streaming)

interface PendingImage {
  id: string
  fileName: string
  mimeType: string
  dataUrl: string
  base64: string
}

const images = ref<PendingImage[]>([])
const visionSupported = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const MAX_RAW_BYTES = 3.75 * 1024 * 1024
const ACCEPT_MIME = ['image/png', 'image/jpeg', 'image/gif', 'image/webp']

onMounted(async () => {
  try {
    const cfg = await http.get<{ supported: boolean }>('/api/chat/vision-config')
    visionSupported.value = cfg.supported
  } catch {
    visionSupported.value = false
  }
})
onBeforeUnmount(() => window.removeEventListener('paste', onWindowPaste, true))

function readFile(file: File) {
  if (!ACCEPT_MIME.includes(file.type)) {
    kernel.notify.warning(t('chat.imageTypeInvalid'))
    return
  }
  if (file.size > MAX_RAW_BYTES) {
    kernel.notify.warning(t('chat.imageTooLarge'))
    return
  }
  const reader = new FileReader()
  reader.onload = () => {
    const dataUrl = reader.result as string
    const base64 = dataUrl.slice(dataUrl.indexOf(',') + 1)
    images.value.push({ id: crypto.randomUUID(), fileName: file.name, mimeType: file.type, dataUrl, base64 })
  }
  reader.readAsDataURL(file)
}

function onPickChange(e: Event) {
  const input = e.target as HTMLInputElement
  for (const f of Array.from(input.files ?? [])) readFile(f)
  input.value = ''
}

function onWindowPaste(e: ClipboardEvent) {
  if (!visionSupported.value || streaming.value) return
  for (const item of Array.from(e.clipboardData?.items ?? [])) {
    if (item.kind === 'file' && item.type.startsWith('image/')) {
      const f = item.getAsFile()
      if (f) readFile(f)
    }
  }
}

onMounted(() => window.addEventListener('paste', onWindowPaste, true))

function removeImage(id: string) {
  images.value = images.value.filter((i) => i.id !== id)
}

async function send() {
  const content = text.value.trim()
  if ((!content && images.value.length === 0) || streaming.value) return
  const imgs = images.value.map((i) => ({ fileName: i.fileName, mimeType: i.mimeType, base64: i.base64 }))
  text.value = ''
  images.value = []
  await kernel.chat.send(content, imgs, { enabled: thinkingEnabled.value, effort: thinkingEffort.value })
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    void send()
  }
}

function interrupt() {
  void kernel.chat.interrupt()
}
</script>

<template>
  <div class="input-bar-wrap">
    <div class="input-bar">
      <div v-if="visionSupported && images.length > 0" class="image-row">
        <div v-for="img in images" :key="img.id" class="image-thumb">
          <img :src="img.dataUrl" :alt="img.fileName" />
          <el-button class="remove" size="small" circle text :aria-label="t('chat.removeImage')" @click="removeImage(img.id)">×</el-button>
        </div>
      </div>
      <el-input
        v-model="text"
        type="textarea"
        :rows="3"
        resize="none"
        :placeholder="t('chat.inputPlaceholder')"
        @keydown="onKeydown"
      />
      <div class="bar-actions">
        <span class="nc-dim hint">
          {{ t('chat.policyHint') }}
          <template v-if="visionSupported">
            <el-tooltip :content="t('chat.imagePasteHint')" placement="top">
              <el-button text size="small" type="primary" @click="fileInput?.click()">🖼 {{ t('chat.uploadImage') }}</el-button>
            </el-tooltip>
            <input ref="fileInput" type="file" accept="image/png,image/jpeg,image/gif,image/webp" multiple class="hidden-file" @change="onPickChange" />
          </template>
          <span class="thinking-row">
            <el-switch v-model="thinkingEnabled" size="small" @change="saveThinking" />
            <span class="thinking-label">{{ t('chat.thinkingMode') }}</span>
            <el-select
              v-model="thinkingEffort"
              size="small"
              :disabled="!thinkingEnabled"
              class="thinking-effort"
              @change="saveThinking"
            >
              <el-option v-for="o in effortOptions" :key="o.value" :value="o.value" :label="t(o.labelKey)" />
            </el-select>
          </span>
        </span>
        <el-button v-if="streaming" type="danger" plain @click="interrupt">■ {{ t('chat.interrupt') }}</el-button>
        <el-button v-else type="primary" :disabled="!text.trim() && images.length === 0" @click="send">{{ t('chat.send') }}</el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.input-bar-wrap {
  padding: 12px 8% 20px;
  border-top: 1px solid var(--nc-border);
  background: var(--nc-surface);
  backdrop-filter: blur(10px);
}

.bar-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}

.hint {
  font-size: 12px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.thinking-row {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-left: 8px;
  padding-left: 10px;
  border-left: 1px solid var(--nc-border);
}

.thinking-label {
  font-size: 12px;
  white-space: nowrap;
}

.thinking-effort {
  width: 96px;
}

.image-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}

.image-thumb {
  position: relative;
  width: 64px;
  height: 64px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--nc-border);
}

.image-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-thumb .remove {
  position: absolute;
  top: 0;
  right: 0;
  min-width: 18px;
  height: 18px;
  padding: 0;
  background: rgba(0, 0, 0, 0.55);
  color: #fff;
  font-size: 13px;
  line-height: 18px;
  border-radius: 0 0 0 6px;
}

.hidden-file {
  display: none;
}
</style>
