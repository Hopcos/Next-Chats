<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import type { UsageTotals } from '@/api/types'
import { kernel } from '@/kernel'

const { t } = useI18n()

const usage = ref<{ totals: UsageTotals; byDay: { day: string; tokens: number; cost: number; requests: number }[] } | null>(null)
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    usage.value = await http.get('/api/admin/metrics/usage')
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.metrics.loadFailed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}

const cards = [
  { key: 'requests' as const, labelKey: 'admin.metrics.requests', fmt: (v: number) => `${v}` },
  { key: 'totalTokens' as const, labelKey: 'admin.metrics.totalTokens', fmt: (v: number) => v.toLocaleString() },
  { key: 'promptTokens' as const, labelKey: 'admin.metrics.promptTokens', fmt: (v: number) => v.toLocaleString() },
  { key: 'completionTokens' as const, labelKey: 'admin.metrics.completionTokens', fmt: (v: number) => v.toLocaleString() },
  { key: 'cost' as const, labelKey: 'admin.metrics.cost', fmt: (v: number) => v.toFixed(4) },
  { key: 'avgTtftMs' as const, labelKey: 'admin.metrics.avgTtftMs', fmt: (v: number) => `${v}` },
  { key: 'avgTotalMs' as const, labelKey: 'admin.metrics.avgTotalMs', fmt: (v: number) => `${v}` },
  { key: 'toolCalls' as const, labelKey: 'admin.metrics.toolCalls', fmt: (v: number) => `${v}` },
  { key: 'toolErrors' as const, labelKey: 'admin.metrics.toolErrors', fmt: (v: number) => `${v}` },
  { key: 'approvals' as const, labelKey: 'admin.metrics.approvals', fmt: (v: number) => `${v}` },
]

onMounted(load)
</script>

<template>
  <div v-loading="loading">
    <div class="head">
      <h3>{{ t('admin.metrics.title') }}</h3>
      <el-button @click="load">{{ t('common.refresh') }}</el-button>
    </div>

    <template v-if="usage">
      <div class="cards">
        <div v-for="c in cards" :key="c.key" class="stat-card">
          <div class="stat-label nc-dim">{{ t(c.labelKey) }}</div>
          <div class="stat-value">{{ c.fmt(usage.totals[c.key as keyof UsageTotals] as number) }}</div>
        </div>
      </div>

      <el-card class="table-card" shadow="never">
        <template #header>{{ t('admin.metrics.dayTrend') }}</template>
        <el-table :data="usage.byDay" size="small">
          <el-table-column prop="day" :label="t('admin.metrics.day')" width="140" />
          <el-table-column :label="t('admin.metrics.requests')" width="120">
            <template #default="{ row }">{{ row.requests }}</template>
          </el-table-column>
          <el-table-column :label="t('admin.metrics.tokens')">
            <template #default="{ row }">{{ row.tokens.toLocaleString() }}</template>
          </el-table-column>
          <el-table-column :label="t('admin.metrics.cost')">
            <template #default="{ row }">{{ row.cost.toFixed(4) }}</template>
          </el-table-column>
        </el-table>
      </el-card>
    </template>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 12px;
  margin: 16px 0;
}

.stat-card {
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  border-radius: 12px;
  padding: 14px;
  backdrop-filter: blur(8px);
}

.stat-label {
  font-size: 12px;
}

.stat-value {
  font-size: 20px;
  font-weight: 700;
  margin-top: 4px;
}

.table-card {
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  border-radius: 12px;
}
</style>
