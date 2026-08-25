<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'

const { t } = useI18n()
const router = useRouter()
const user = kernel.auth.state.user
const catalog = kernel.catalog.state

onMounted(() => {
  if (!catalog.loaded) void kernel.catalog.load()
})

async function logout() {
  await kernel.auth.logout()
  void router.push('/login')
}
</script>

<template>
  <div class="page">
    <header class="page-head">
      <el-button text @click="router.push('/')">{{ t('settings.back') }}</el-button>
      <h2>{{ t('settings.title') }}</h2>
      <div class="spacer" />
      <el-button text type="danger" @click="logout">{{ t('common.logout') }}</el-button>
    </header>

    <main class="page-body nc-scroll">
      <el-card class="card" shadow="never">
        <template #header>{{ t('settings.basic') }}</template>
        <p>{{ t('settings.usernameRow', { label: t('settings.usernameLabel'), username: user?.username ?? '', displayName: user?.displayName ?? '' }) }}</p>
        <p class="nc-dim">{{ t('settings.rolesLabel') }}：{{ user?.roles.join(', ') }}</p>
      </el-card>

      <el-card class="card" shadow="never">
        <template #header>{{ t('settings.promptsTitle') }}</template>
        <el-empty v-if="catalog.prompts.length === 0" :description="t('settings.noPrompts')" :image-size="60" />
        <el-table v-else :data="catalog.prompts" size="small">
          <el-table-column prop="name" :label="t('settings.colName')" width="140" />
          <el-table-column prop="summary" :label="t('settings.colSummary')" />
        </el-table>
      </el-card>

      <el-card class="card" shadow="never">
        <template #header>{{ t('settings.mcpsTitle') }}</template>
        <el-empty v-if="catalog.mcps.length === 0" :description="t('settings.noMcps')" :image-size="60" />
        <el-collapse v-else>
          <el-collapse-item v-for="m in catalog.mcps" :key="m.id" :title="t('settings.mcpTitle', { name: m.name, endpoint: m.endpoint ?? t('common.placeholderDash') })">
            <p class="nc-dim" v-if="m.description">{{ m.description }}</p>
            <el-tag v-for="i in m.items" :key="i.id" size="small" style="margin: 2px">
              {{ i.name }}
            </el-tag>
          </el-collapse-item>
        </el-collapse>
      </el-card>

      <el-card class="card" shadow="never">
        <template #header>{{ t('settings.skillsTitle') }}</template>
        <el-empty v-if="catalog.skills.length === 0" :description="t('settings.noSkills')" :image-size="60" />
        <el-table v-else :data="catalog.skills" size="small">
          <el-table-column prop="name" :label="t('settings.colName')" width="140" />
          <el-table-column prop="metaToolName" :label="t('settings.colMetaTool')" width="160" />
          <el-table-column prop="summary" :label="t('settings.colSummary')" />
        </el-table>
      </el-card>
    </main>
  </div>
</template>

<style scoped>
.page {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.page-head {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 20px;
  height: 56px;
  border-bottom: 1px solid var(--nc-border);
  background: var(--nc-surface);
  backdrop-filter: blur(10px);
}

.spacer {
  flex: 1;
}

.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px 10%;
}

.card {
  margin-bottom: 16px;
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  border-radius: var(--nc-radius);
}
</style>
