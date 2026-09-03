<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { musicSources, type MusicTrack } from '@/utils/musicSources'

/**
 * 顶部音乐播放器：播放/暂停、上一首、下一首。
 * 多公开源自动切换：当前源拉取失败 / 曲目加载超时 / 播放出错 → 自动切下一源。
 * 默认源为国内免费曲库（网易云公开歌单直链）。
 */
const audio = ref<HTMLAudioElement>()
const playing = ref(false)
const loading = ref(false)
const dead = ref(false) // 本轮所有源均已尝试且不可用
const sourceIdx = ref(0)
const tracks = ref<MusicTrack[]>([])
const trackIdx = ref(0)

const sourceName = computed(() => musicSources[sourceIdx.value]?.name ?? '')
const current = computed(() => tracks.value[trackIdx.value])
const titleText = computed(() => {
  if (dead.value) return '音乐源暂不可用，点播放重试'
  if (!current.value) return '音乐播放器'
  const t = current.value
  return t.artist ? `${t.title} · ${t.artist}` : t.title
})

let loadTimer = 0
/** 本轮已尝试过的源索引（防全部失败后死循环重复拉取同一源） */
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
    const list = await musicSources[idx].fetchTracks()
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
  // 同源换下一首；曲目用完则切换下一源
  if (trackIdx.value + 1 < tracks.value.length) {
    trackIdx.value++
    playTrack(trackIdx.value)
  } else {
    nextSource()
  }
}

async function nextSource() {
  tracks.value = []
  let idx = sourceIdx.value + 1
  while (idx < musicSources.length) {
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
    start()
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
  start()
}

function next() {
  if (tracks.value.length && trackIdx.value + 1 < tracks.value.length) {
    trackIdx.value++
    playTrack(trackIdx.value)
    return
  }
  nextSource()
}

function prev() {
  const a = audio.value
  if (!tracks.value.length) return
  // 播放中且已过 3s → 回到本曲开头；否则上一首
  if (a && a.currentTime > 3 && !a.paused) {
    a.currentTime = 0
    return
  }
  trackIdx.value = trackIdx.value > 0 ? trackIdx.value - 1 : tracks.value.length - 1
  playTrack(trackIdx.value)
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
  gap: 12px;
  min-width: 0;
  flex: 1;
}

/* 均衡器跳动指示（播放中） */
.mp-eq {
  display: inline-flex;
  align-items: flex-end;
  gap: 2px;
  width: 16px;
  height: 14px;
  flex-shrink: 0;
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
  gap: 6px;
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

@media (prefers-reduced-motion: reduce) {
  .mp-eq.on i {
    animation: none;
  }
}
</style>
