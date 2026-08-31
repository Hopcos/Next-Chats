<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import { kernel } from '@/kernel'
import { copyText as copyTextUtil } from '@/utils/clipboard'

const router = useRouter()
const { t } = useI18n()

interface FavoriteItem {
  id: string
  title: string
  questionText?: string | null
  answerText?: string | null
  createdAt: string
}

const list = ref<FavoriteItem[]>([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    list.value = await http.get<FavoriteItem[]>('/api/chat/favorites')
  } catch {
    kernel.notify.error(t('chat.favoriteLoadFailed'))
  } finally {
    loading.value = false
  }
}

onMounted(load)

/** 收藏夹默认展示固定长度的对话提示词（标题截断），可手工重命名 */
function preview(text?: string | null): string {
  const s = (text ?? '').replace(/\s+/g, ' ').trim()
  return s.length > 48 ? s.slice(0, 48) + '…' : s || '—'
}

function fmtTime(iso: string): string {
  try {
    const d = new Date(iso)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
  } catch {
    return ''
  }
}

// ---------------- 详情弹窗：提问 / 回答 + 各自复制 ----------------
const viewOpen = ref(false)
const viewing = ref<FavoriteItem | null>(null)

function openView(f: FavoriteItem) {
  viewing.value = f
  viewOpen.value = true
}

async function copyText(text?: string | null, label = '') {
  const value = text ?? ''
  if (await copyTextUtil(value)) {
    kernel.notify.success(label ? `${label} ${t('chat.copied')}` : t('chat.copied'))
  } else {
    kernel.notify.warning(t('chat.copyFailed'))
  }
}

// ---------------- 重命名 ----------------
async function rename(f: FavoriteItem) {
  try {
    const { value } = await ElMessageBox.prompt(t('chat.favoriteRenameTitle'), t('chat.favoriteRename'), {
      inputValue: f.title,
      inputPattern: /\S+/,
      inputErrorMessage: t('chat.favoriteRenameEmpty'),
      inputPlaceholder: t('chat.favoriteRename'),
      confirmButtonText: t('common.save'),
      cancelButtonText: t('common.cancel'),
    })
    const title = (value ?? '').trim()
    if (!title || title === f.title) return
    await http.put(`/api/chat/favorites/${f.id}`, { title })
    f.title = title
    kernel.notify.success(t('chat.favoriteRenamed'))
    void load()
  } catch {
    /* 取消 */
  }
}

// ---------------- 删除（确认后） ----------------
async function remove(f: FavoriteItem) {
  try {
    await ElMessageBox.confirm(
      t('chat.favoriteDeleteConfirm', { title: f.title }),
      t('chat.favoriteDeleteTitle'),
      { type: 'warning', confirmButtonText: t('common.delete'), cancelButtonText: t('common.cancel') },
    )
  } catch {
    return
  }
  try {
    await http.delete(`/api/chat/favorites/${f.id}`)
    list.value = list.value.filter((x) => x.id !== f.id)
    if (viewing.value?.id === f.id) viewOpen.value = false
    kernel.notify.success(t('chat.favoriteDeleted'))
  } catch {
    kernel.notify.error(t('chat.favoriteDeleteFailed'))
  }
}
</script>

<template>
  <div class="fav-page">
    <header class="fav-topbar">
      <button class="back-btn" @click="router.push('/')">
        <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M10 3 5 8l5 5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" /></svg>
        {{ t('common.back') }}
      </button>
      <h2 class="fav-title-head">
        <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M8 1.8 9.9 5.8l4.4.6-3.2 3.1.8 4.4L8 11.9l-3.9 2 .8-4.4L1.7 6.4l4.4-.6L8 1.8Z" fill="currentColor" stroke="currentColor" stroke-width="1" stroke-linejoin="round" /></svg>
        {{ t('chat.favorites') }}
        <span class="count nc-dim">{{ list.length }}</span>
      </h2>
      <button class="refresh-btn" title="refresh" @click="void load()">
        <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M13.5 8a5.5 5.5 0 1 1-1.6-3.9M13.5 1.8v2.7h-2.7" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" /></svg>
      </button>
    </header>

    <div v-if="loading" class="fav-empty nc-dim">{{ t('chat.favoriteLoading') }}</div>
    <div v-else-if="list.length === 0" class="fav-empty">
      <div class="empty-star">⭐</div>
      <p class="nc-dim">{{ t('chat.favoriteEmpty') }}</p>
    </div>

    <div v-else class="fav-grid">
      <div v-for="f in list" :key="f.id" class="fav-card" @click="openView(f)">
        <div class="fav-title">{{ f.title }}</div>
        <div class="fav-prev nc-dim">{{ preview(f.questionText) }}</div>
        <div class="fav-foot">
          <span class="fav-time nc-dim">{{ fmtTime(f.createdAt) }}</span>
          <span class="fav-ops" @click.stop>
            <button class="op-btn" :title="t('chat.favoriteRename')" @click="void rename(f)">
              <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path d="m11.3 2.2 2.5 2.5L5.5 13H3v-2.5l8.3-8.3Z" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linejoin="round" /><path d="M9.8 3.7 12.3 6.2" stroke="currentColor" stroke-width="1.4" /></svg>
            </button>
            <button class="op-btn danger" :title="t('common.delete')" @click="void remove(f)">
              <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M3 4.5h10M6.5 4.5V3h3v1.5M5 4.5l.5 8.5h5l.5-8.5M6.8 7v3.5M9.2 7v3.5" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round" /></svg>
            </button>
          </span>
        </div>
      </div>
    </div>

    <!-- 详情弹窗：提问 / 回答 + 各自复制 -->
    <el-dialog
      v-model="viewOpen"
      :title="viewing?.title"
      width="min(720px, 92vw)"
      class="fav-dialog"
      append-to-body
    >
      <div v-if="viewing" class="qa-wrap">
        <div class="qa-block q">
          <div class="qa-head">
            <span class="qa-tag">{{ t('chat.favoriteQuestion') }}</span>
            <button class="op-btn" :title="t('chat.copy')" @click="copyText(viewing.questionText, t('chat.favoriteQuestion'))">
              <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><rect x="5.5" y="5.5" width="8" height="8" rx="1.5" fill="none" stroke="currentColor" stroke-width="1.3" /><path d="M10.5 5.5v-2a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2" fill="none" stroke="currentColor" stroke-width="1.3" /></svg>
            </button>
          </div>
          <div class="qa-text">{{ viewing.questionText }}</div>
        </div>
        <div class="qa-block a">
          <div class="qa-head">
            <span class="qa-tag alt">{{ t('chat.favoriteAnswer') }}</span>
            <button class="op-btn" :title="t('chat.copy')" @click="copyText(viewing.answerText, t('chat.favoriteAnswer'))">
              <svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><rect x="5.5" y="5.5" width="8" height="8" rx="1.5" fill="none" stroke="currentColor" stroke-width="1.3" /><path d="M10.5 5.5v-2a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2" fill="none" stroke="currentColor" stroke-width="1.3" /></svg>
            </button>
          </div>
          <div class="qa-text">{{ viewing.answerText }}</div>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<style scoped>
.fav-page {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: var(--nc-bg);
  color: var(--nc-text);
}

.fav-topbar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 24px;
  border-bottom: 1px solid var(--nc-border);
  background: var(--nc-surface);
}

.back-btn,
.refresh-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid var(--nc-border);
  border-radius: 8px;
  padding: 5px 12px;
  background: transparent;
  color: var(--nc-text);
  cursor: pointer;
  font-size: 13px;
}

.back-btn:hover,
.refresh-btn:hover {
  background: color-mix(in srgb, var(--nc-text-dim) 10%, transparent);
}

.ico {
  width: 15px;
  height: 15px;
}

.fav-title-head {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  font-size: 17px;
}

.fav-title-head .ico {
  color: var(--nc-primary);
}

.count {
  font-size: 12px;
}

.fav-grid {
  flex: 1;
  overflow-y: auto;
  padding: 20px 24px 40px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;
  align-content: start;
}

.fav-card {
  border: 1px solid var(--nc-border);
  border-radius: 12px;
  padding: 14px 16px;
  background: var(--nc-surface);
  cursor: pointer;
  transition: transform 0.12s, box-shadow 0.12s, border-color 0.12s;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 108px;
}

.fav-card:hover {
  transform: translateY(-2px);
  border-color: color-mix(in srgb, var(--nc-primary) 55%, transparent);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.18);
}

.fav-title {
  font-weight: 600;
  font-size: 14px;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  word-break: break-word;
}

.fav-prev {
  font-size: 12px;
  line-height: 1.5;
  flex: 1;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  word-break: break-word;
}

.fav-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.fav-time {
  font-size: 11px;
}

.fav-ops {
  display: inline-flex;
  gap: 4px;
}

.op-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--nc-text-dim);
  cursor: pointer;
}

.op-btn:hover {
  background: color-mix(in srgb, var(--nc-text-dim) 14%, transparent);
  color: var(--nc-text);
}

.op-btn.danger:hover {
  background: color-mix(in srgb, #f56c6c 18%, transparent);
  color: #f56c6c;
}

.fav-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  font-size: 13px;
}

.empty-star {
  font-size: 40px;
  opacity: 0.6;
}

/* 详情弹窗 */
.qa-wrap {
  display: flex;
  flex-direction: column;
  gap: 14px;
  max-height: 60vh;
  overflow-y: auto;
}

.qa-block {
  border: 1px solid var(--nc-border);
  border-radius: 10px;
  padding: 12px 14px;
  background: color-mix(in srgb, var(--nc-bg) 55%, transparent);
}

.qa-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.qa-tag {
  font-size: 12px;
  font-weight: 600;
  padding: 2px 10px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--nc-primary) 16%, transparent);
  color: var(--nc-primary);
}

.qa-tag.alt {
  background: color-mix(in srgb, #22c55e 14%, transparent);
  color: #22c55e;
}

.qa-text {
  font-size: 13.5px;
  line-height: 1.65;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
