<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElDialog, ElButton } from 'element-plus'
import { kernel } from '@/kernel'

const { t } = useI18n()

const approval = computed(() => kernel.chat.state.pendingApproval)

const prettyArgs = computed(() => {
  const raw = approval.value?.argumentsJson
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
})

async function decide(approved: boolean) {
  const a = approval.value
  if (!a) return
  try {
    await kernel.chat.decideApproval(a.approvalId, approved ? 'Approved' : 'Rejected')
    kernel.notify.success(approved ? t('chat.approvedRun') : t('chat.rejectedOp'))
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('chat.approvalFailed'), (e as { code?: string }).code)
  }
}
</script>

<template>
  <ElDialog
    :model-value="!!approval"
    :title="t('chat.approvalTitle')"
    width="480px"
    :close-on-click-modal="false"
    class="approval-dialog"
  >
    <template v-if="approval">
      <p style="margin-top: 0">
        {{ t('chat.approvalBody', { tool: `${approval.serverName}.${approval.toolName}` }) }}
      </p>
      <pre class="args" v-if="prettyArgs">{{ prettyArgs }}</pre>
      <p class="nc-dim" style="font-size: 12px">{{ t('chat.approvalExpireNote') }}</p>
    </template>
    <template #footer>
      <ElButton type="danger" plain @click="decide(false)">{{ t('chat.reject') }}</ElButton>
      <ElButton type="primary" @click="decide(true)">{{ t('chat.approve') }}</ElButton>
    </template>
  </ElDialog>
</template>

<style scoped>
.args {
  background: rgba(0, 0, 0, 0.35);
  border: 1px solid var(--nc-border);
  border-radius: 8px;
  padding: 10px;
  font-size: 12px;
  max-height: 220px;
  overflow: auto;
}
</style>
