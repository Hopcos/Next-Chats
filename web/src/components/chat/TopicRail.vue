<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import type { UiMessage } from '@/kernel/plugins'

/**
 * 话题导航条：消息区左侧垂直竖轨，一个话题（user 提问）一根横线。
 * hover 显示话题标题（限字数），点击平滑滚动到对应话题；随滚动高亮当前话题。
 */
const props = defineProps<{ messages: UiMessage[]; scroller: HTMLElement | null }>()

const topics = computed(() => props.messages.filter((m) => m.role === 'user'))
const activeIndex = ref(-1)

function titleOf(m: UiMessage): string {
  const s = (m.content || '').replace(/\s+/g, ' ').trim()
  return s.length > 46 ? s.slice(0, 46) + '…' : s
}

/** 横条宽度：话题越多每条越短（14~44px），整体保持紧凑 */
function lineWidth(i: number): string {
  const n = topics.value.length
  const w = Math.max(14, Math.min(44, Math.round(320 / Math.max(1, n))))
  return w + 'px'
}

function jump(i: number) {
  const m = topics.value[i]
  if (!m) return
  const sc = props.scroller
  if (!sc) return
  const el = document.getElementById('topic-' + m.id)
  if (!el) return
  // 立即高亮目标话题（scroll 事件随后跟上滚动跟随）
  activeIndex.value = i
  // 只滚动消息列表（scroller）：scrollIntoView 会连带滚动侧栏/页面等所有祖先滚动容器（引发布局错乱）
  const top = el.getBoundingClientRect().top - sc.getBoundingClientRect().top + sc.scrollTop - 12
  sc.scrollTo({ top, behavior: 'smooth' })
}

let raf = 0
function onScroll() {
  if (raf) return
  raf = requestAnimationFrame(() => {
    raf = 0
    const sc = props.scroller
    if (!sc) return
    // 当前话题 = 视口 1/3 高度以上、最近的 user 提问（位置一律按 scroller 内坐标计算）
    const scTop = sc.getBoundingClientRect().top
    const mid = sc.scrollTop + sc.clientHeight * 0.35
    let idx = -1
    for (let i = 0; i < topics.value.length; i++) {
      const el = document.getElementById('topic-' + topics.value[i].id)
      if (!el) continue
      const relTop = el.getBoundingClientRect().top - scTop + sc.scrollTop
      if (relTop <= mid) idx = i
    }
    activeIndex.value = idx
  })
}

// 仅当话题（user 提问）数量真正增长时才定位到最后一项：
// 流式回复/其它消息更新会重建 topics 数组引用，若不判数量，会把用户手动滚动后的高亮强行拽回底部
let lastTopicCount = 0
watch(
  topics,
  (list) => {
    const n = list.length
    const grew = n > lastTopicCount
    lastTopicCount = n
    if (grew && n > 0) activeIndex.value = n - 1
  },
  { immediate: true },
)

onMounted(() => {
  props.scroller?.addEventListener('scroll', onScroll, { passive: true })
})

onUnmounted(() => {
  props.scroller?.removeEventListener('scroll', onScroll)
  if (raf) cancelAnimationFrame(raf)
})
</script>

<template>
  <div v-if="topics.length > 1" class="topic-rail" aria-hidden="true">
    <div v-for="(tp, i) in topics" :key="tp.id" class="rail-line-wrap" @click="jump(i)">
      <el-tooltip :content="titleOf(tp)" placement="left" :show-after="300" :offset="8">
        <div class="rail-line" :class="{ active: i === activeIndex }" :style="{ '--rail-w': lineWidth(i) }" />
      </el-tooltip>
    </div>
  </div>
</template>

<style scoped>
.topic-rail {
  position: absolute;
  right: 6px;
  top: 50%;
  transform: translateY(-50%);
  /* 固定高度（非 max-height）+ overflow → 滚动条必然出现，底部话题始终可达 */
  height: min(78vh, 640px);
  display: flex;
  flex-direction: column;
  gap: 10px;
  z-index: 6;
  padding: 6px 2px;
  pointer-events: auto;
  overflow-y: auto;
  scrollbar-width: thin;
}

/* 首尾弹性 spacer：话题少时竖轨内容垂直居中；话题很多时 spacer 收缩为 0，顶部/底部全程可滚动 */
.topic-rail::before,
.topic-rail::after {
  content: '';
  flex: 1 1 auto;
  min-height: 6px;
  flex-shrink: 1;
}

.topic-rail::-webkit-scrollbar {
  width: 3px;
}

.topic-rail::-webkit-scrollbar-thumb {
  background: var(--nc-border);
  border-radius: 2px;
}

.rail-line-wrap {
  cursor: pointer;
  line-height: 0;
  /* 加宽热区：横条最宽 44px，热区留足横向余量，方便 hover/点击 */
  width: 46px;
  display: flex;
  justify-content: center;
  padding: 2px 0;
}

/* 横条：水平条，宽度随话题数缩放（--rail-w 内联注入），hover 加长 */
.rail-line {
  flex-shrink: 0;
  height: 3px;
  width: var(--rail-w, 20px);
  border-radius: 2px;
  background: var(--nc-border);
  transition: width 0.18s ease, background 0.15s, box-shadow 0.15s, transform 0.15s;
}

/* hover（整行热区）：横条加长，更易看清与选中 */
.rail-line-wrap:hover .rail-line {
  width: min(calc(var(--rail-w, 20px) * 1.7), 46px);
  background: color-mix(in srgb, var(--nc-primary) 55%, transparent);
}

.rail-line.active {
  background: var(--nc-primary);
  box-shadow: 0 0 6px color-mix(in srgb, var(--nc-primary) 60%, transparent);
}

.rail-line-wrap:hover .rail-line.active {
  box-shadow: 0 0 8px color-mix(in srgb, var(--nc-primary) 70%, transparent);
}
</style>
