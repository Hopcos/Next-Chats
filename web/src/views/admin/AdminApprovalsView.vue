<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import type { ToolApprovalDto } from '@/api/types'
import { kernel } from '@/kernel'

const { t } = useI18n()

const list = ref<ToolApprovalDto[]>([])
const statusFilter = ref('')
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    const q = statusFilter.value ? `?status=${statusFilter.value}` : ''
    list.value = await http.get<ToolApprovalDto[]>(`/api/admin/approvals${q}`)
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.approvals.loadFailed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}

async function decide(row: ToolApprovalDto, approved: boolean) {
  try {
    await http.post(`/api/admin/approvals/${row.id}/decide`, { approved, reason: null })
    kernel.notify.success(approved ? t('admin.approvals.approved') : t('admin.approvals.rejected'))
    await load()
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.approvals.operationFailed'), (e as { code?: string }).code)
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

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h3>{{ t('admin.approvals.title') }}</h3>
      <el-select v-model="statusFilter" :placeholder="t('admin.approvals.allStatus')" style="width: 140px" clearable @change="load">
        <el-option :label="t('admin.approvals.pending')" value="Pending" />
        <el-option :label="t('admin.approvals.approved')" value="Approved" />
        <el-option :label="t('admin.approvals.rejected')" value="Rejected" />
        <el-option :label="t('admin.approvals.expired')" value="Expired" />
      </el-select>
    </div>

    <el-table :data="list" v-loading="loading" size="small" stripe>
      <el-table-column prop="createdAt" :label="t('admin.approvals.time')" width="160">
        <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString(undefined) }}</template>
      </el-table-column>
      <el-table-column :label="t('admin.approvals.tool')" min-width="180">
        <template #default="{ row }">
          <span class="nc-mono">{{ row.mcpServerName }}.{{ row.toolName }}</span>
        </template>
      </el-table-column>
      <el-table-column :label="t('admin.approvals.args')" min-width="180">
        <template #default="{ row }">
          <el-tooltip :content="prettyArgs(row.argumentsJson)" placement="top" :show-after="300">
            <span class="nc-mono nc-dim">{{ (row.argumentsJson ?? '').slice(0, 60) }}</span>
          </el-tooltip>
        </template>
      </el-table-column>
      <el-table-column :label="t('common.status')" width="90">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Pending' ? 'warning' : row.status === 'Approved' ? 'success' : row.status === 'Rejected' ? 'danger' : 'info'" size="small">
            {{ row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('common.actions')" width="140" fixed="right">
        <template #default="{ row }">
          <template v-if="row.status === 'Pending'">
            <el-button size="small" type="success" plain @click="decide(row, true)">{{ t('admin.approvals.approve') }}</el-button>
            <el-button size="small" type="danger" plain @click="decide(row, false)">{{ t('admin.approvals.reject') }}</el-button>
          </template>
          <span v-else class="nc-dim">{{ t('common.placeholderDash') }}</span>
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
