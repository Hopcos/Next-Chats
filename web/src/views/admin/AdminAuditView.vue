<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import type { AuditLogDto } from '@/api/types'
import { kernel } from '@/kernel'

const { t } = useI18n()

const list = ref<AuditLogDto[]>([])
const userIdFilter = ref('')
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const q = userIdFilter.value ? `?userId=${userIdFilter.value}` : ''
    list.value = await http.get<AuditLogDto[]>(`/api/admin/audit${q}`)
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.audit.loadFailed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}

const actionTypes: Record<string, string> = {
  'LLM_PROVIDER.CREATE': 'primary', 'LLM_PROVIDER.UPDATE': 'primary', 'LLM_PROVIDER.DELETE': 'danger',
  'MCP.CREATE': 'warning', 'MCP.UPDATE': 'warning', 'MCP.DELETE': 'danger', 'MCP.FETCH': 'warning', 'MCP.ITEM_TOGGLE': 'warning',
  'PROMPT.CREATE': 'success', 'PROMPT.UPDATE': 'success', 'PROMPT.DELETE': 'danger',
  'SKILL.CREATE': 'success', 'SKILL.UPDATE': 'success', 'SKILL.DELETE': 'danger',
  'USER.CREATE': 'primary', 'USER.UPDATE': 'primary', 'USER.DELETE': 'danger',
  'ROLE.CREATE': 'primary', 'ROLE.UPDATE': 'primary', 'ROLE.DELETE': 'danger', 'ROLE.BINDINGS': 'primary',
  'APPROVAL.APPROVED': 'success', 'APPROVAL.REJECTED': 'danger',
  'CHAT.SEND': 'info', 'LOGIN.SUCCESS': 'success', 'LOGIN.FAILED': 'danger',
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h3>{{ t('admin.audit.title') }}</h3>
      <el-input v-model="userIdFilter" :placeholder="t('admin.audit.filterByUser')" style="width: 220px" clearable @keyup.enter="load" @clear="load">
        <template #append><el-button @click="load">{{ t('admin.audit.search') }}</el-button></template>
      </el-input>
    </div>

    <el-table :data="list" v-loading="loading" size="small" stripe>
      <el-table-column prop="createdAt" :label="t('admin.audit.time')" width="160">
        <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString(undefined) }}</template>
      </el-table-column>
      <el-table-column :label="t('admin.audit.action')" width="160">
        <template #default="{ row }">
          <el-tag :type="(actionTypes[row.action] as any) ?? 'info'" size="small">{{ row.action }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('admin.audit.traceId')" width="180">
        <template #default="{ row }"><span class="nc-mono nc-dim">{{ row.traceId }}</span></template>
      </el-table-column>
      <el-table-column prop="target" :label="t('admin.audit.target')" min-width="140" show-overflow-tooltip />
      <el-table-column :label="t('admin.audit.detail')" min-width="180" show-overflow-tooltip>
        <template #default="{ row }"><span class="nc-mono nc-dim">{{ row.detailJson ?? t('common.placeholderDash') }}</span></template>
      </el-table-column>
      <el-table-column :label="t('admin.audit.suspicious')" width="70">
        <template #default="{ row }">
          <el-tag v-if="row.isSuspicious" type="danger" size="small">{{ t('admin.audit.yes') }}</el-tag>
          <span v-else class="nc-dim">{{ t('admin.audit.no') }}</span>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
}
</style>
