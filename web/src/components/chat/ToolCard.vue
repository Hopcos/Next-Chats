<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ToolCard as ToolCardModel } from '@/kernel/plugins'
import { kernel } from '@/kernel'

const props = defineProps<{ card: ToolCardModel }>()

const { t } = useI18n()

const icon = computed(() => {
  const c = props.card
  if (c.approvalStatus === 'pending') return '⚠️'
  if (c.approvalStatus === 'approved') return '✅'
  if (c.approvalStatus === 'rejected' || c.approvalStatus === 'expired') return '⛔'
  if (c.status === 'running') return '⏳'
  return c.status === 'ok' ? '✅' : '❌'
})

const open = computed(() => !!props.card.argumentsJson || !!props.card.resultPreview)

function pretty(raw?: string): string {
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
</script>

<template>
  <el-collapse v-if="open" class="tool-card" :model-value="[]">
    <el-collapse-item :name="card.key">
      <template #title>
        <span class="t-icon">{{ icon }}</span>
        <span class="t-name">{{ card.serverName ? card.serverName + '.' : '' }}{{ card.toolName }}</span>
        <el-tag v-if="card.approvalStatus === 'pending'" type="warning" size="small" style="margin-left: 8px">{{ t('chat.awaitingApproval') }}</el-tag>
        <el-tag v-else-if="card.approvalStatus === 'approved'" type="success" size="small" style="margin-left: 8px">{{ t('chat.approved') }}</el-tag>
        <el-tag v-else-if="card.approvalStatus === 'rejected'" type="danger" size="small" style="margin-left: 8px">{{ t('chat.rejected') }}</el-tag>
        <span v-if="card.durationMs != null" class="t-dur nc-dim">{{ t('chat.toolDuration', { ms: card.durationMs }) }}</span>
      </template>
      <div v-if="card.argumentsJson" class="t-section">
        <div class="t-label">{{ t('chat.args') }}</div>
        <pre>{{ pretty(card.argumentsJson) }}</pre>
      </div>
      <div v-if="card.resultPreview || card.errorCode" class="t-section">
        <div class="t-label">{{ t('chat.result') }} <span v-if="card.errorCode" class="nc-dim">[{{ card.errorCode }}]</span></div>
        <pre :class="{ err: card.status === 'error' }">{{ card.resultPreview ?? t('chat.noContent') }}</pre>
      </div>
      <el-button v-if="card.approvalStatus === 'pending'" size="small" type="primary" style="margin-top: 6px" @click="kernel.notify.info(t('chat.approvalHint'))">
        {{ t('chat.goApproval') }}
      </el-button>
    </el-collapse-item>
  </el-collapse>
</template>

<style scoped>
.tool-card {
  margin: 6px 0;
  border: 1px solid var(--nc-border);
  border-radius: 10px;
  background: rgba(15, 23, 42, 0.5);
}

:deep(.el-collapse-item__header) {
  background: transparent;
  font-size: 12.5px;
  padding: 0 10px;
  border: none;
}

:deep(.el-collapse-item__content) {
  padding: 0 12px 10px;
}

.t-icon {
  margin-right: 6px;
}

.t-dur {
  margin-left: 8px;
  font-size: 11px;
}

.t-section pre {
  background: rgba(0, 0, 0, 0.3);
  border-radius: 6px;
  padding: 8px;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 4px 0 8px;
}

.t-section pre.err {
  color: #f87171;
}

.t-label {
  font-size: 11px;
  opacity: 0.7;
  margin: 4px 0;
}
</style>
