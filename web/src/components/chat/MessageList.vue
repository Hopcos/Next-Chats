<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { UiMessage } from '@/kernel/plugins'
import MessageItem from '@/components/chat/MessageItem.vue'
import TopicRail from '@/components/chat/TopicRail.vue'

const { t } = useI18n()
const props = defineProps<{ messages: UiMessage[] }>()
const emit = defineEmits<{ regenerate: [messageId: string]; remove: [message: UiMessage] }>()

const scroller = ref<HTMLDivElement | null>(null)
const stickToBottom = ref(true)
let hasShownOnce = false

async function scrollBottom() {
  await nextTick()
  const el = scroller.value
  if (el && stickToBottom.value) {
    el.scrollTop = el.scrollHeight
  }
}

/** 首屏/历史加载完成：无视 onScroll 干扰，强制一次性“即时”滚到底（禁用 smooth 动画避免中途被中断） */
async function scrollBottomInstant() {
  await nextTick()
  const el = scroller.value
  if (!el || !props.messages.length) return
  stickToBottom.value = true
  el.style.scrollBehavior = 'auto'
  el.scrollTop = el.scrollHeight
  el.style.scrollBehavior = ''
}

function onScroll() {
  const el = scroller.value
  if (!el) return
  stickToBottom.value = el.scrollHeight - el.scrollTop - el.clientHeight < 80
}

watch(
  () => props.messages,
  async () => {
    // 首次出现内容（挂载即有历史 / 历史加载完成）：强制即时滚到底
    if (!hasShownOnce && props.messages.length > 0) {
      hasShownOnce = true
      await scrollBottomInstant()
      return
    }
    if (props.messages.length === 0) return
    await scrollBottom()
  },
  { deep: true, immediate: true },
)

// ---- 内容高度变化即时跟随：打字机揭示阶段只改 MessageItem 内部文本，不触发上方 watch ----
// 用 MutationObserver 兜底：只要内容区 DOM 高度在长（打字机/思考滚动），且贴底，就持续滚底
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
