<script setup lang="ts">
/** 工具页壳：按路由 :key 从 Cordis 插件注册中心解析组件（懒加载）并沉浸渲染 */
import { computed, defineAsyncComponent, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { http } from '@/api/http'
import { getToolDefinition } from '@/tools/registry'
import ToolIcon from '@/tools/ToolIcon.vue'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const toolKey = computed(() => String(route.params.key ?? ''))
const def = computed(() => getToolDefinition(toolKey.value))

/** 名称/图标优先取管理端配置（/api/me/tools），未就绪时回落插件默认 */
interface UserToolDto {
  id: string
  key: string
  name: string
  icon: string
  description?: string | null
}
const adminMeta = ref<UserToolDto | null>(null)
onMounted(() => {
  void http
    .get<UserToolDto[]>('/api/me/tools')
    .then((list) => (adminMeta.value = list.find((x) => x.key === toolKey.value) ?? null))
    .catch(() => {})
})

const displayName = computed(() => {
  if (adminMeta.value?.name) return adminMeta.value.name
  const d = def.value
  return d ? (d.nameKey ? t(d.nameKey) : d.defaultName) : toolKey.value
})
const displayIcon = computed(() => adminMeta.value?.icon || def.value?.defaultIcon || 'toolbox')

const component = computed(() => (def.value ? defineAsyncComponent(def.value.loader) : null))
</script>

<template>
  <div v-if="def" class="tp">
    <header class="tp-top">
      <button class="tp-back" @click="router.push('/tools')">
        <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M15 5l-7 7 7 7" />
        </svg>
        {{ t('tools.backToHub') }}
      </button>
      <div class="tp-brand">
        <span class="tp-ico"><ToolIcon :icon="displayIcon" :size="18" /></span>
        <h1 class="tp-title">{{ displayName }}</h1>
      </div>
    </header>
    <main class="tp-body">
      <component :is="component" :key="toolKey" />
    </main>
  </div>

  <div v-else class="tp-unknown">
    <p>{{ t('tools.unknown') }}</p>
    <el-button type="primary" size="small" @click="router.replace('/tools')">{{ t('tools.backToHub') }}</el-button>
  </div>
</template>

<style scoped>
.tp {
  height: 100vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background:
    radial-gradient(1100px 500px at 85% -10%, color-mix(in srgb, var(--nc-primary) 12%, transparent), transparent 60%),
    var(--nc-bg);
  color: var(--nc-text);
}

.tp-top {
  height: 58px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 0 20px;
  border-bottom: 1px solid var(--nc-border);
  background: var(--nc-surface);
  backdrop-filter: blur(10px);
}

.tp-back {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: 1px solid var(--nc-border);
  background: transparent;
  color: var(--nc-text-dim, #8a94a6);
  border-radius: 8px;
  padding: 6px 12px;
  font-size: 12.5px;
  cursor: pointer;
  transition: all 0.15s;
}

.tp-back:hover {
  color: var(--nc-primary);
  border-color: var(--nc-primary);
}

.tp-brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.tp-ico {
  width: 32px;
  height: 32px;
  border-radius: 9px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  background: linear-gradient(135deg, var(--nc-primary), color-mix(in srgb, var(--nc-primary) 55%, #8b5cf6));
}

.tp-title {
  margin: 0;
  font-size: 15.5px;
  font-weight: 700;
}

.tp-body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.tp-unknown {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  color: var(--nc-text-dim, #8a94a6);
}
</style>
