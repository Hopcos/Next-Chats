<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { musicSources, type MusicTrack } from '@/utils/musicSources'

/**
 * 顶部音乐播放器：源手工选择（最左）+ 播放列表（可点选曲目）+ 播放/暂停、上一首、下一首。
 * 多公开源自动切换兜底：当前源拉取失败 / 曲目加载超时 / 播放出错 → 自动切下一源。
 * 默认源为国内免费曲库（网易云公开歌单直链）。所有 UI 状态仅存在于本组件（localStorage 不落库）。
 */
const audio = ref<HTMLAudioElement>()
const playing = ref(false)
const loading = ref(false)
const dead = ref(false) // 当前源本轮不可用
const sourceIdx = ref(0)
const tracks = ref<MusicTrack[]>([])
const trackIdx = ref(0)

const sources = musicSources
const sourceName = computed(() => sources[sourceIdx.value]?.name ?? '')
const current = computed(() => tracks.value[trackIdx.value])
const titleText = computed(() => {
  if (dead.value) return '音乐源暂不可用，点播放重试'
  if (!current.value) return tracks.value.length ? '已加载列表，点击播放' : '音乐播放器'
  const t = current.value
  return t.artist ? `${t.title} · ${t.artist}` : t.title
})

let loadTimer = 0
/** 自动切换已尝试过的源（防死循环）；用户手工选源时不限 */
const tried = new Set<number>()

function clearLoadTimer() {
  if (loadTimer) {
    clearTimeout(loadTimer)
    loadTimer = 0
  }
}

async function ensureTracks(idx: number): Promise<boolean> {
  if (tried.has(idx)) return false
  tried.add(idx)
  loading.value = true
  try {
    const list = await sources[idx].fetchTracks()
    const ok = list.filter((t) => t && t.url)
    if (!ok.length) throw new Error('empty track list')
    tracks.value = ok
    sourceIdx.value = idx
    trackIdx.value = 0
    loading.value = false
    return true
  } catch {
    loading.value = false
    return false
  }
}

function playTrack(i: number) {
  const a = audio.value
  const t = tracks.value[i]
  if (!a || !t) return
  clearLoadTimer()
  a.src = t.url
  a.play().catch(() => {/* 播放被策略阻断时等待用户手势 */})
  // 8s 内未就绪视为该源不通 → 自动切换
  loadTimer = window.setTimeout(() => {
    if (audio.value && audio.value.readyState < 2) onTrackFail()
  }, 8000)
}

function onTrackFail() {
  clearLoadTimer()
  const a = audio.value
  if (a) a.src = ''
  // 自动兜底才允许连续切源；用户手工选了不可用的源时给出提示而不是连环跳走
  if (tried.size > 0) {
    // 同源换下一首；曲目用完则切换下一源
    if (trackIdx.value + 1 < tracks.value.length) {
      trackIdx.value++
      playTrack(trackIdx.value)
    } else {
      nextSource()
    }
  } else {
    dead.value = true
    playing.value = false
  }
}

async function nextSource() {
  tracks.value = []
  let idx = sourceIdx.value + 1
  while (idx < sources.length) {
    if (await ensureTracks(idx)) {
      playTrack(0)
      return
    }
    idx++
  }
  // 所有源都试过且失败
  tried.clear()
  sourceIdx.value = 0
  dead.value = true
  playing.value = false
}

async function start() {
  if (tracks.value.length) {
    playTrack(trackIdx.value)
    return
  }
  tried.clear()
  dead.value = false
  if (await ensureTracks(0)) playTrack(0)
  else nextSource()
}

function toggle() {
  const a = audio.value
  if (!a) return
  if (dead.value) {
    // 重试：清掉失败标记，走一次完整流程
    dead.value = false
    tried.clear()
    void start()
    return
  }
  if (playing.value) {
    a.pause()
    playing.value = false
    return
  }
  if (a.src) {
    a.play().catch(() => {})
    return
  }
  void start()
}

function next() {
  if (tracks.value.length && trackIdx.value + 1 < tracks.value.length) {
    trackIdx.value++
    playTrack(trackIdx.value)
    return
  }
  void nextSource()
}

function prev() {
  const a = audio.value
  if (!tracks.value.length) return
  if (a && a.currentTime > 3 && !a.paused) {
    a.currentTime = 0
    return
  }
  trackIdx.value = trackIdx.value > 0 ? trackIdx.value - 1 : tracks.value.length - 1
  playTrack(trackIdx.value)
}

/** 手工选择音乐源：立即加载并播放该源第一首 */
async function pickSource(i: number) {
  if (i === sourceIdx.value && tracks.value.length && !dead.value) return
  dead.value = false
  tried.clear() // 手工选择不限制该源
  const a = audio.value
  if (a) a.src = ''
  tracks.value = []
  sourceIdx.value = i
  trackIdx.value = 0
  loading.value = true
  try {
    const list = await sources[i].fetchTracks()
    const ok = list.filter((t) => t && t.url)
    if (!ok.length) throw new Error('empty track list')
    tracks.value = ok
    loading.value = false
    playTrack(0)
  } catch {
    loading.value = false
    dead.value = true
    playing.value = false
  }
}

/** 播放列表点选曲目 */
function pickTrack(i: number) {
  if (i < 0 || i >= tracks.value.length) return
  if (i === trackIdx.value && audio.value && !audio.value.paused) return
  trackIdx.value = i
  playTrack(i)
}

function onPlaying() {
  playing.value = true
  clearLoadTimer()
}

onBeforeUnmount(() => {
  clearLoadTimer()
  audio.value?.pause()
})
</script>

<template>
  <div class="music-player" :class="{ playing }" aria-label="音乐播放器">
    <div class="mp-left">
      <!-- 音乐源手工选择（最左） -->
      <el-popover trigger="click" :width="200" popper-class="mp-popper">
        <template #reference>
          <button class="mp-btn mp-src-btn" type="button" title="切换音乐源" aria-label="切换音乐源">
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="9" />
              <path d="M3 12h18M12 3a14.5 14.5 0 0 1 0 18M12 3a14.5 14.5 0 0 0 0 18" />
            </svg>
            <span class="mp-src-name">{{ sourceName }}</span>
          </button>
        </template>
        <div class="mp-src-list" role="listbox" aria-label="音乐源">
          <button
            v-for="(s, i) in sources"
            :key="s.name"
            type="button"
            class="mp-src-item"
            :class="{ cur: i === sourceIdx }"
            role="option"
            :aria-selected="i === sourceIdx"
            @click="pickSource(i)"
          >
            <span class="mp-src-item-name">{{ s.name }}</span>
            <svg v-if="i === sourceIdx" viewBox="0 0 24 24" width="13" height="13" fill="currentColor" aria-hidden="true"><path d="m9 16.2-3.5-3.5L4 14.2 9 19.2 20 8.2 18.5 6.8z" /></svg>
          </button>
        </div>
      </el-popover>

      <!-- 播放列表 -->
      <el-popover trigger="click" :width="280" popper-class="mp-popper">
        <template #reference>
          <button class="mp-btn" type="button" title="播放列表" aria-label="播放列表">
            <svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M8 6h13M8 12h13M8 18h13" />
              <circle cx="4" cy="6" r="1.4" fill="currentColor" stroke="none" />
              <circle cx="4" cy="12" r="1.4" fill="currentColor" stroke="none" />
              <circle cx="4" cy="18" r="1.4" fill="currentColor" stroke="none" />
            </svg>
          </button>
        </template>
        <div class="mp-plist">
          <p v-if="!tracks.length" class="mp-plist-empty">{{ loading ? '加载列表中…' : '列表为空，请先选择音乐源或点击播放' }}</p>
          <button
            v-for="(tr, i) in tracks"
            :key="tr.url + i"
            type="button"
            class="mp-pl-item"
            :class="{ cur: i === trackIdx }"
            @click="pickTrack(i)"
          >
            <svg v-if="i === trackIdx" viewBox="0 0 24 24" width="12" height="12" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z" /></svg>
            <span class="mp-pl-main">
              <span class="mp-pl-title">{{ tr.title }}</span>
              <span v-if="tr.artist" class="mp-pl-art">{{ tr.artist }}</span>
            </span>
          </button>
        </div>
      </el-popover>

      <div class="mp-controls">
        <button class="mp-btn" type="button" title="上一首" aria-label="上一首" @click="prev">
          <svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M6 6h2v12H6zm3.5 6 8.5 6V6z" /></svg>
        </button>
        <button class="mp-btn mp-play" type="button" :title="playing ? '暂停' : '播放'" aria-label="播放或暂停" @click="toggle">
          <svg v-if="playing" viewBox="0 0 24 24" width="17" height="17" fill="currentColor" aria-hidden="true"><path d="M6 5h4v14H6zm8 0h4v14h-4z" /></svg>
          <svg v-else viewBox="0 0 24 24" width="17" height="17" fill="currentColor" aria-hidden="true"><path d="M8 5v14l11-7z" /></svg>
        </button>
        <button class="mp-btn" type="button" title="下一首" aria-label="下一首" @click="next">
          <svg viewBox="0 0 24 24" width="15" height="15" fill="currentColor" aria-hidden="true"><path d="M16 6h2v12h-2zM6 18l8.5-6L6 6z" /></svg>
        </button>
      </div>

      <span class="mp-eq" :class="{ on: playing }" aria-hidden="true">
        <i /><i /><i />
      </span>
      <div class="mp-meta">
        <span class="mp-title">{{ titleText }}</span>
        <span class="mp-src">{{ dead ? '' : sourceName + (loading ? ' · 连接中…' : '') }}</span>
      </div>
    </div>

    <audio ref="audio" preload="none" @playing="onPlaying" @ended="next" @error="onTrackFail" @canplay="clearLoadTimer" />
  </div>
</template>

<style scoped>
.music-player {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 12px;
  height: 44px;
  padding: 0 14px 0 12px;
  border-bottom: 1px solid var(--nc-border);
  background: color-mix(in srgb, var(--nc-surface) 82%, transparent);
  color: var(--nc-text-dim);
  user-select: none;
  min-width: 0;
}

.mp-left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  flex: 1;
}

/* 源选择按钮（最左）：图标 + 当前源名 */
.mp-src-btn {
  gap: 4px;
  padding: 0 8px;
  width: auto;
  border: 1px solid transparent;
}

.mp-src-btn:hover {
  border-color: color-mix(in srgb, var(--nc-primary) 40%, transparent);
}

.mp-src-name {
  font-size: 11.5px;
  max-width: 96px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 均衡器跳动指示（播放中） */
.mp-eq {
  display: inline-flex;
  align-items: flex-end;
  gap: 2px;
  width: 16px;
  height: 14px;
  flex-shrink: 0;
  margin-left: 2px;
}

.mp-eq i {
  width: 3px;
  height: 30%;
  border-radius: 1.5px;
  background: var(--nc-text-faint, var(--nc-text-dim));
  transition: height 0.3s ease;
}

.mp-eq.on i {
  background: var(--nc-primary);
  animation: mp-eq-bounce 1s ease-in-out infinite;
}

.mp-eq.on i:nth-child(1) {
  animation-delay: 0s;
}

.mp-eq.on i:nth-child(2) {
  animation-delay: 0.18s;
}

.mp-eq.on i:nth-child(3) {
  animation-delay: 0.36s;
}

@keyframes mp-eq-bounce {
  0%,
  100% {
    height: 22%;
  }
  50% {
    height: 88%;
  }
}

.mp-meta {
  display: flex;
  flex-direction: column;
  min-width: 0;
  line-height: 1.15;
}

.mp-title {
  font-size: 12.5px;
  color: var(--nc-text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mp-src {
  font-size: 10.5px;
  opacity: 0.65;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mp-controls {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.mp-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--nc-text-dim);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  padding: 0;
}

.mp-btn:hover {
  color: var(--nc-primary);
  background: color-mix(in srgb, var(--nc-primary) 12%, transparent);
}

.mp-btn:active {
  transform: scale(0.92);
}

.mp-play {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--nc-primary);
  color: var(--nc-primary-contrast, #04121f);
  box-shadow: 0 2px 8px color-mix(in srgb, var(--nc-primary) 45%, transparent);
}

.mp-play:hover {
  background: color-mix(in srgb, var(--nc-primary) 85%, #000);
  color: var(--nc-primary-contrast, #04121f);
}

/* 窄窗口：收紧间距，隐藏源名文字只留图标 */
@media (max-width: 900px) {
  .mp-left {
    gap: 5px;
  }

  .mp-src-name {
    max-width: 60px;
  }
}

@media (max-width: 640px) {
  .mp-src-name {
    display: none;
  }

  .mp-src-btn {
    padding: 0 4px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .mp-eq.on i {
    animation: none;
  }
}
</style>

<!-- popover 内容 teleport 到 body，样式不能 scoped -->
<style>
.mp-popper {
  --mp-panel-bg: var(--nc-surface, #fff);
  --mp-panel-border: var(--nc-border, #e2e8f0);
  --mp-panel-text: var(--nc-text-dim, #94a3b8);
  background: var(--mp-panel-bg);
  border: 1px solid var(--mp-panel-border);
  border-radius: 10px;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.18);
  padding: 6px;
}

.mp-popper .el-popper__arrow::before {
  background: var(--mp-panel-bg);
  border-color: var(--mp-panel-border);
}

.mp-src-list,
.mp-plist {
  max-height: 300px;
  overflow-y: auto;
}

.mp-plist-empty {
  margin: 0;
  padding: 12px 10px;
  font-size: 12px;
  color: var(--mp-panel-text);
  text-align: center;
}

.mp-src-item,
.mp-pl-item {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 6px;
  border: none;
  background: transparent;
  color: var(--mp-panel-text);
  font-size: 12.5px;
  text-align: left;
  padding: 7px 9px;
  border-radius: 7px;
  cursor: pointer;
  transition: all 0.12s;
}

.mp-src-item:hover,
.mp-pl-item:hover {
  background: color-mix(in srgb, var(--nc-primary, #6d5efc) 12%, transparent);
  color: var(--nc-primary, #6d5efc);
}

.mp-src-item.cur,
.mp-pl-item.cur {
  background: color-mix(in srgb, var(--nc-primary, #6d5efc) 16%, transparent);
  color: var(--nc-primary, #6d5efc);
  font-weight: 600;
}

.mp-src-item-name {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mp-pl-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.mp-pl-title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mp-pl-art {
  font-size: 11px;
  opacity: 0.72;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
