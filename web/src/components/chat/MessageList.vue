<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { UiMessage } from '@/kernel/plugins'
import MessageItem from '@/components/chat/MessageItem.vue'
import TopicRail from '@/components/chat/TopicRail.vue'

const { t } = useI18n()
const props = defineProps<{ messages: UiMessage[]; sessionId?: string | null }>()
const emit = defineEmits<{ regenerate: [messageId: string]; remove: [message: UiMessage]; favorite: [message: UiMessage] }>()

const scroller = ref<HTMLDivElement | null>(null)
const stickToBottom = ref(true)

/**
 * 无论以任何方式进入会话（首次 / 切换 / 刷新 / 历史加载完成）都无条件滚动到最底部。
 * 用 rAF 循环持续以最新 scrollHeight 定位，直到连续 5 帧贴底且高度不再变化才结束；
 * 不设帧数上限（防止长列表渲染耗帧导致提前停在半路），仅 10s 时间死兜底防死循环。
 */
async function scrollToBottomForce() {
  const el = scroller.value
  if (!el) return
  el.style.scrollBehavior = 'auto'
  const deadline = performance.now() + 10000
  let lastHeight = -1
  let stableFrames = 0
  const step = () => {
    const e = scroller.value
    if (!e) return
    const height = e.scrollHeight
    const gap = height - e.scrollTop - e.clientHeight
    if (gap <= 2 && height === lastHeight) {
      stableFrames++
      if (stableFrames >= 5 || performance.now() > deadline) {
        e.style.scrollBehavior = ''
        return
      }
    } else {
      stableFrames = 0
      lastHeight = height
    }
    e.scrollTo({ top: height, behavior: 'auto' })
    requestAnimationFrame(step)
  }
  step()
}

function onScroll() {
  const el = scroller.value
  if (!el) return
  stickToBottom.value = el.scrollHeight - el.scrollTop - el.clientHeight < 80
}

// 会话切换（key 重挂载的首个场景）即触发强制滚动；历史/新消息到达（空→非空）再次触发
let lastMessageCount = 0
watch(
  () => props.messages.length,
  async (count) => {
    const grewFromEmpty = lastMessageCount === 0 && count > 0
    lastMessageCount = count
    if (grewFromEmpty) await scrollToBottomForce()
  },
  { immediate: true },
)

watch(
  () => props.sessionId,
  () => {
    // 切换会话（或首次进入）：无条件强制滚动到底部（不管之前位置/缓存）
    void scrollToBottomForce()
  },
  { immediate: true },
)

// 打字机揭示阶段只改 MessageItem 内部文本，不触发上方 watch；内容区变高且贴底时持续跟随
let observer: MutationObserver | null = null
let scrollRaf = 0

onMounted(() => {
  observer = new MutationObserver(() => {
    if (scrollRaf) return
    scrollRaf = requestAnimationFrame(() => {
      scrollRaf = 0
      const el = scroller.value
      if (el && stickToBottom.value) el.scrollTop = el.scrollHeight
    })
  })
  if (scroller.value) {
    observer.observe(scroller.value, { childList: true, subtree: true, characterData: true, attributes: true })
  }
})

onUnmounted(() => {
  observer?.disconnect()
  if (scrollRaf) cancelAnimationFrame(scrollRaf)
})
</script>

<template>
  <div class="msg-wrap">
    <div ref="scroller" class="msg-list nc-scroll" @scroll="onScroll">
      <div v-if="messages.length === 0" class="welcome">
        <div class="hero">🚀 Next Chats</div>
        <p class="nc-dim">{{ t('chat.welcomeSlogan') }}</p>
        <p class="hint nc-dim">{{ t('chat.welcomeHint') }}</p>
      </div>

      <MessageItem
        v-for="m in messages"
        :key="m.id"
        :message="m"
        @regenerate="(id: string) => emit('regenerate', id)"
        @remove="(msg: UiMessage) => emit('remove', msg)"
        @favorite="(msg: UiMessage) => emit('favorite', msg)"
      />
      <div style="height: 12px" />
    </div>
    <TopicRail :messages="messages" :scroller="scroller" />
  </div>
</template>

<style scoped>
.msg-wrap {
  position: relative;
  flex: 1;
  display: flex;
  min-height: 0;
}

.msg-list {
  flex: 1;
  overflow-y: auto;
  padding: 10px 4%;
  scroll-behavior: smooth;
  /* 回答框宽度 = 剩余窗口（聊天内容区）的 80%，随窗口大小动态缩放 */
  --nc-msg-w: 80%;
}

.welcome {
  text-align: center;
  margin-top: 16vh;
}

.hero {
  font-size: 42px;
  font-weight: 800;
  letter-spacing: 1px;
  background: linear-gradient(120deg, var(--nc-primary), #a78bfa);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}

.hint {
  font-size: 12.5px;
}
</style>
