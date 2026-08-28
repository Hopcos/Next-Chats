<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'
import { getLang, setLang, type AppLang } from '@/i18n'
import SessionSidebar from '@/components/chat/SessionSidebar.vue'
import MessageList from '@/components/chat/MessageList.vue'
import ChatInputBar from '@/components/chat/ChatInputBar.vue'
import ChatSettingsDrawer from '@/components/chat/ChatSettingsDrawer.vue'

const router = useRouter()
const { t } = useI18n()
const drawerOpen = ref(false)
const editingTitle = ref(false)
const titleDraft = ref('')
let fallbackTimer: number | undefined

const current = computed(() => kernel.session.current)
const user = computed(() => kernel.auth.state.user)
const messages = computed(() => kernel.chat.messagesOf(kernel.session.state.currentId))

const lang = computed<AppLang>(() => getLang())
const langOptions = [
  { value: 'en' as AppLang, label: 'English' },
  { value: 'zh' as AppLang, label: '中文' },
]

onMounted(() => {
  void (async () => {
    await kernel.session.loadAll().catch(() => {})
    // 首次使用引导：没有任何会话时自动创建一个（否则输入问题后发送会因无可归属会话而不显示）
    if (kernel.session.state.sessions.length === 0) {
      await kernel.session.create().catch(() => {})
    }
  })()
  // 自愈兜底：若首屏加载后会话列表仍为空，稍后自动重试，避免“刷新后侧栏消失”
  fallbackTimer = window.setTimeout(() => {
    if (kernel.session.state.sessions.length === 0 && !kernel.session.state.loading) {
      void kernel.session.loadAll().catch(() => {})
    }
  }, 1500)
})

onUnmounted(() => {
  window.clearTimeout(fallbackTimer)
})

async function onNewSession() {
  await kernel.session.create()
}

async function openSettings() {
  drawerOpen.value = true
  // 先拉取服务端记忆，再刷新目录：避免“孤儿校准”基于过期本地值误清用户选择
  await kernel.settings.pullFromServer().catch(() => {})
  void kernel.catalog.load().catch(() => {})
}

function startRename() {
  if (!current.value) return
  titleDraft.value = current.value.title
  editingTitle.value = true
}

async function commitRename() {
  editingTitle.value = false
  const title = titleDraft.value.trim()
  if (current.value && title) {
    await kernel.session
      .rename(current.value.id, title)
      .catch((e) => kernel.notify.error((e as { message?: string }).message ?? t('chat.renameFailed'), (e as { code?: string }).code))
  }
}

async function onDeleteSession() {
  if (!current.value) return
  try {
    await ElMessageBox.confirm(t('chat.deleteSessionConfirm', { title: current.value.title }), t('chat.deleteSessionTitle'), { type: 'warning' })
  } catch {
    return
  }
  await kernel.session.remove(current.value.id)
}

function onRegenerate(messageId: string) {
  void kernel.chat.regenerate(messageId)
}

async function onRemoveMessage(msg: { id: string; content?: string; role?: string }) {
  const title = (msg.content || t('chat.untitled')).replace(/\s+/g, ' ').slice(0, 30)
  try {
    await ElMessageBox.confirm(t('chat.deleteMessageConfirm', { title }), t('chat.deleteMessageTitle'), { type: 'warning' })
  } catch {
    return
  }
  await kernel.chat.deleteFrom(msg.id)
}

async function logout() {
  await kernel.auth.logout()
  void router.push('/login')
}

const themeOptions = [
  { value: 'aurora', labelKey: 'chat.themeAurora' },
  { value: 'dawn', labelKey: 'chat.themeDawn' },
  { value: 'midnight', labelKey: 'chat.themeMidnight' },
]
</script>

<template>
  <div class="chat-layout">
    <SessionSidebar />

    <main class="chat-main">
      <header class="topbar">
        <div class="title-area">
          <template v-if="editingTitle">
            <el-input v-model="titleDraft" size="small" style="width: 280px" @keyup.enter="commitRename" @blur="commitRename" />
          </template>
          <template v-else>
            <h2 class="session-title" @dblclick="startRename">{{ current?.title ?? t('common.appName') }}</h2>
          </template>
        </div>

        <div class="actions">
          <el-button-group>
            <el-button size="small" class="act-btn" @click="onNewSession">
              <svg class="btn-ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M8 2v12M2 8h12" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" /></svg>
              {{ t('chat.newSession') }}
            </el-button>
            <el-button size="small" class="act-btn" @click="openSettings">
              <svg class="btn-ico" viewBox="0 0 16 16" aria-hidden="true"><path d="M8 5.2a2.8 2.8 0 1 0 0 5.6 2.8 2.8 0 0 0 0-5.6Z" fill="none" stroke="currentColor" stroke-width="1.4" /><path d="M8 1.5v2M8 12.5v2M2.1 4.2l1.7 1M12.2 10.8l1.7 1M1.5 8h2M12.5 8h2M2.1 11.8l1.7-1M12.2 5.2l1.7-1" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" /></svg>
              {{ t('chat.settings') }}
            </el-button>
          </el-button-group>

          <el-tooltip :content="t('chat.threeD')" placement="bottom">
            <el-switch
              :model-value="kernel.settings.state.threeEnabled"
              style="--el-switch-on-color: var(--nc-primary)"
              @change="(v: boolean) => kernel.settings.toggleThree(v)"
            />
          </el-tooltip>

          <el-select
            :model-value="kernel.theme.state.theme"
            size="small"
            style="width: 96px"
            @change="(v: string) => kernel.theme.set(v as never)"
          >
            <el-option v-for="tOpt in themeOptions" :key="tOpt.value" :label="t(tOpt.labelKey)" :value="tOpt.value" />
          </el-select>

          <el-select :model-value="lang" size="small" style="width: 96px" @change="(v: AppLang) => setLang(v)">
            <el-option v-for="l in langOptions" :key="l.value" :label="l.label" :value="l.value" />
          </el-select>

          <el-dropdown trigger="click">
            <el-avatar :size="26" class="avatar">{{ (user?.displayName ?? user?.username ?? '?').slice(0, 1) }}</el-avatar>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="router.push('/settings')">{{ t('chat.personalCatalog') }}</el-dropdown-item>
                <el-dropdown-item v-if="user?.isAdmin" @click="router.push('/admin')">{{ t('common.admin') }}</el-dropdown-item>
                <el-dropdown-item divided @click="logout">{{ t('common.logout') }}</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </header>

      <!-- :key 绑定会话 id：切换会话时整个列表重挂载 → 首屏强制滚到底部 -->
      <MessageList
        :key="kernel.session.state.currentId ?? 'none'"
        :session-id="kernel.session.state.currentId"
        :messages="messages"
        @regenerate="onRegenerate"
        @remove="onRemoveMessage"
      />
      <ChatInputBar />

      <ChatSettingsDrawer v-model="drawerOpen" />
    </main>
  </div>
</template>

<style scoped>
.chat-layout {
  display: flex;
  height: 100%;
}

.chat-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.topbar {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  border-bottom: 1px solid var(--nc-border);
  background: var(--nc-surface);
  backdrop-filter: blur(10px);
}

.session-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
}

.actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.avatar {
  cursor: pointer;
  background: var(--nc-primary);
  color: #04121f;
  font-weight: 700;
}

.act-btn .btn-ico {
  width: 13px;
  height: 13px;
  margin-right: 4px;
  vertical-align: -2px;
  flex-shrink: 0;
}

.act-btn {
  display: inline-flex;
  align-items: center;
}
</style>
