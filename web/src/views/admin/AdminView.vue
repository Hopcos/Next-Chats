<script setup lang="ts">
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const menus = [
  { path: '/admin/llm', labelKey: 'admin.menu.llm', icon: '🧠' },
  { path: '/admin/mcp', labelKey: 'admin.menu.mcp', icon: '🔌' },
  { path: '/admin/prompts', labelKey: 'admin.menu.prompts', icon: '📝' },
  { path: '/admin/skills', labelKey: 'admin.menu.skills', icon: '🛠️' },
  { path: '/admin/users', labelKey: 'admin.menu.users', icon: '👤' },
  { path: '/admin/roles', labelKey: 'admin.menu.roles', icon: '🎭' },
  { path: '/admin/approvals', labelKey: 'admin.menu.approvals', icon: '🛡️' },
  { path: '/admin/audit', labelKey: 'admin.menu.audit', icon: '📜' },
  { path: '/admin/metrics', labelKey: 'admin.menu.metrics', icon: '📊' },
]

function isActive(path: string) {
  return route.path === path || (path === '/admin/llm' && route.path.startsWith('/admin'))
}
</script>

<template>
  <div class="admin-layout">
    <aside class="aside">
      <div class="brand">Next Chats <span class="nc-dim">{{ $t('common.admin') }}</span></div>
      <nav>
        <div
          v-for="m in menus"
          :key="m.path"
          class="menu-item"
          :class="{ active: isActive(m.path) }"
          @click="router.push(m.path)"
        >
          <span class="menu-icon">{{ m.icon }}</span>{{ $t(m.labelKey) }}
        </div>
      </nav>
      <div class="back">
        <el-button size="small" text @click="router.push('/')">{{ $t('admin.menu.back') }}</el-button>
      </div>
    </aside>
    <main class="content nc-scroll">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
.admin-layout {
  display: flex;
  height: 100%;
}

.aside {
  width: 200px;
  border-right: 1px solid var(--nc-border);
  background: var(--nc-surface);
  backdrop-filter: blur(10px);
  display: flex;
  flex-direction: column;
  padding: 16px 10px;
}

.brand {
  font-size: 18px;
  font-weight: 700;
  padding: 0 10px 16px;
}

.menu-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  border-radius: 9px;
  font-size: 13.5px;
  cursor: pointer;
  margin-bottom: 2px;
}

.menu-item:hover {
  background: rgba(148, 163, 184, 0.12);
}

.menu-item.active {
  background: color-mix(in srgb, var(--nc-primary) 20%, transparent);
  color: var(--nc-primary);
  font-weight: 600;
}

.menu-icon {
  width: 18px;
  text-align: center;
}

.back {
  margin-top: auto;
  padding: 8px 6px 0;
}

.content {
  flex: 1;
  overflow-y: auto;
  padding: 20px 28px;
}
</style>
