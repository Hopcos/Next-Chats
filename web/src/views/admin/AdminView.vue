<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

const collapsed = ref(localStorage.getItem('nextchats.admin.collapsed') === '1')

function toggleCollapse() {
  collapsed.value = !collapsed.value
  localStorage.setItem('nextchats.admin.collapsed', collapsed.value ? '1' : '0')
}

const menus = [
  { path: '/admin/llm', labelKey: 'admin.menu.llm', icon: '🧠' },
  { path: '/admin/mcp', labelKey: 'admin.menu.mcp', icon: '🔌' },
  { path: '/admin/prompts', labelKey: 'admin.menu.prompts', icon: '📝' },
  { path: '/admin/skills', labelKey: 'admin.menu.skills', icon: '🛠️' },
  { path: '/admin/users', labelKey: 'admin.menu.users', icon: '👤' },
  { path: '/admin/roles', labelKey: 'admin.menu.roles', icon: '🎭' },
  { path: '/admin/internal-auth', labelKey: 'admin.menu.internalAuth', icon: '🔐' },
  { path: '/admin/approvals', labelKey: 'admin.menu.approvals', icon: '🛡️' },
  { path: '/admin/audit', labelKey: 'admin.menu.audit', icon: '📜' },
  { path: '/admin/metrics', labelKey: 'admin.menu.metrics', icon: '📊' },
]

function isActive(path: string) {
  // 精确匹配：仅当前路由恰好是该菜单项时才高亮（/admin 会重定向到 /admin/llm，因此无需前缀匹配）
  return route.path === path
}
</script>

<template>
  <div class="admin-layout">
    <aside class="aside" :class="{ collapsed }">
      <div class="brand">
        <span v-if="!collapsed" class="brand-text">Next Chats <span class="nc-dim">{{ $t('common.admin') }}</span></span>
        <span v-else class="brand-text brand-mini">NC</span>
        <el-button class="collapse-btn" size="small" text :title="collapsed ? $t('admin.menu.expand') : $t('admin.menu.collapse')" @click="toggleCollapse">
          {{ collapsed ? '⮞' : '⮜' }}
        </el-button>
      </div>
      <nav>
        <div
          v-for="m in menus"
          :key="m.path"
          class="menu-item"
          :class="{ active: isActive(m.path) }"
          :title="collapsed ? $t(m.labelKey) : undefined"
          @click="router.push(m.path)"
        >
          <span class="menu-icon">{{ m.icon }}</span><span v-if="!collapsed" class="menu-label">{{ $t(m.labelKey) }}</span>
        </div>
      </nav>
      <div class="back">
        <el-button size="small" text @click="router.push('/')">
          <span v-if="!collapsed">{{ $t('admin.menu.back') }}</span><span v-else>🏠</span>
        </el-button>
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
  transition: width 0.2s;
}

.aside.collapsed {
  width: 52px;
  padding: 16px 6px;
}

.brand {
  font-size: 18px;
  font-weight: 700;
  padding: 0 10px 16px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.brand-mini {
  font-size: 14px;
}

.collapse-btn {
  margin-left: auto;
  color: var(--nc-text-dim);
  font-size: 13px;
}

.aside.collapsed .collapse-btn {
  margin-left: 0;
  width: 100%;
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

.aside.collapsed .menu-item {
  justify-content: center;
  padding: 9px 0;
}

.menu-label {
  overflow: hidden;
  white-space: nowrap;
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
