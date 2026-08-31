<script setup lang="ts">
/**
 * 聊天画板：鼠标作图，确认后把画布内容（PNG base64）代入聊天输入框。
 * - 支持画笔大小 / 颜色 / 撤销 / 擦除 / 清屏；可展开放大与折叠缩小
 * - 每次落笔后自动快照到 localStorage，重新打开恢复上次画的内容
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'confirm', dataUrl: string): void
}>()

const { t } = useI18n()

// ---- 画布（逻辑尺寸固定，CSS 自适应缩放；坐标换算按 bounding rect） ----
const W = 900
const H = 540
const canvasEl = ref<HTMLCanvasElement | null>(null)
const LS_KEY = 'nc.board.snapshot'
let ctx: CanvasRenderingContext2D | null = null

// ---- 工具状态 ----
const color = ref('#111827')
const size = ref(4)
const eraser = ref(false)
const sizeLarge = ref(false)
const undoStack: ImageData[] = []
const MAX_UNDO = 40

const SWATCHES = ['#111827', '#e11d48', '#f97316', '#facc15', '#22c55e', '#0ea5e9', '#2563eb', '#8b5cf6', '#ec4899', '#ffffff']

// ---- 绘制状态 ----
let drawing = false
let lastX = 0
let lastY = 0

function setupCanvas() {
  if (!canvasEl.value) return
  ctx = canvasEl.value.getContext('2d')
  if (!ctx) return
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'
  resetToWhite()
  loadSnapshot()
}

function resetToWhite() {
  if (!ctx) return
  ctx.save()
  ctx.globalCompositeOperation = 'source-over'
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, W, H)
  ctx.restore()
}

/** 撤销栈：每笔开始前记录当前画像；undo 弹出恢复 */
function pushUndo() {
  if (!ctx) return
  undoStack.push(ctx.getImageData(0, 0, W, H))
  if (undoStack.length > MAX_UNDO) undoStack.shift()
}

function undo() {
  if (!ctx || undoStack.length === 0) return
  ctx.putImageData(undoStack.pop()!, 0, 0)
  saveSnapshot()
}

function clearAll() {
  if (!ctx) return
  pushUndo()
  resetToWhite()
  saveSnapshot()
}

function toggleEraser() {
  setEraser(!eraser.value)
}

function setEraser(on: boolean) {
  eraser.value = on
  if (ctx) ctx.globalCompositeOperation = on ? 'destination-out' : 'source-over'
}

// ---- 指针绘制（坐标换算：逻辑尺寸 / 实际渲染尺寸） ----
function toPos(e: PointerEvent) {
  const rect = canvasEl.value!.getBoundingClientRect()
  return {
    x: ((e.clientX - rect.left) * W) / rect.width,
    y: ((e.clientY - rect.top) * H) / rect.height,
  }
}

function strokeStart(e: PointerEvent) {
  if (!ctx) return
  pushUndo()
  drawing = true
  const p = toPos(e)
  lastX = p.x
  lastY = p.y
  ctx.beginPath()
  ctx.moveTo(p.x, p.y)
  ctx.lineWidth = size.value
  ctx.strokeStyle = eraser.value ? 'rgba(0,0,0,1)' : color.value
  ctx.stroke()
  canvasEl.value?.setPointerCapture(e.pointerId)
}

function strokeMove(e: PointerEvent) {
  if (!drawing || !ctx) return
  const p = toPos(e)
  ctx.beginPath()
  ctx.moveTo(lastX, lastY)
  ctx.lineTo(p.x, p.y)
  ctx.lineWidth = size.value
  ctx.strokeStyle = eraser.value ? 'rgba(0,0,0,1)' : color.value
  ctx.stroke()
  lastX = p.x
  lastY = p.y
}

function strokeEnd() {
  if (!drawing) return
  drawing = false
  saveSnapshot()
}

// ---- 持久化：每次内容变化落 localStorage，重新打开恢复 ----
function saveSnapshot() {
  if (!ctx) return
  try {
    const snapshot = canvasEl.value!.toDataURL('image/png')
    if (snapshot.length > 512) localStorage.setItem(LS_KEY, snapshot)
  } catch {
    /* 存储满/隐私模式：忽略 */
  }
}

function loadSnapshot() {
  try {
    const snapshot = localStorage.getItem(LS_KEY)
    if (!snapshot) return
    const img = new Image()
    img.onload = () => {
      if (ctx) {
        ctx.save()
        ctx.globalCompositeOperation = 'source-over'
        ctx.drawImage(img, 0, 0, W, H)
        ctx.restore()
      }
    }
    img.src = snapshot
  } catch {
    /* 忽略 */
  }
}

// ---- 确认：白底合成后导出 PNG base64 并入聊天输入框 ----
function confirm() {
  if (!ctx) return
  const out = document.createElement('canvas')
  out.width = W
  out.height = H
  const oc = out.getContext('2d')!
  oc.fillStyle = '#ffffff'
  oc.fillRect(0, 0, W, H)
  oc.drawImage(canvasEl.value!, 0, 0)
  emit('confirm', out.toDataURL('image/png'))
  emit('update:visible', false)
}

function close() {
  emit('update:visible', false)
}

watch(
  () => props.visible,
  (v) => {
    if (v) setupCanvas()
  },
)
</script>

<template>
  <el-dialog
    :model-value="visible"
    :width="sizeLarge ? 'min(980px, 94vw)' : 'min(660px, 94vw)'"
    top="5vh"
    :show-close="false"
    append-to-body
    class="board-dialog"
    @update:model-value="(v: boolean) => emit('update:visible', v)"
  >
    <template #header>
      <div class="board-header">
        <span class="board-title">🎨 {{ t('chat.board') }}</span>
        <el-button
          text
          :title="sizeLarge ? t('chat.boardShrink') : t('chat.boardExpand')"
          :aria-label="sizeLarge ? t('chat.boardShrink') : t('chat.boardExpand')"
          @click="sizeLarge = !sizeLarge"
        >
          {{ sizeLarge ? '⤡' : '⤢' }}
        </el-button>
      </div>
    </template>

    <div class="board-body">
      <div class="board-toolbar">
        <span class="tool-group">
          <el-button-group>
            <el-button
              size="small"
              :type="!eraser ? 'primary' : 'default'"
              @click="setEraser(false)"
            >
              {{ t('chat.boardBrush') }}
            </el-button>
            <el-button
              size="small"
              :type="eraser ? 'primary' : 'default'"
              @click="setEraser(true)"
            >
              {{ t('chat.boardEraser') }}
            </el-button>
          </el-button-group>
        </span>

        <span class="tool-group">
          <span class="tool-label">{{ t('chat.boardSize') }}</span>
          <el-slider v-model="size" :min="2" :max="24" :step="1" class="size-slider" size="small" />
        </span>

        <span class="tool-group colors">
          <span class="tool-label">{{ t('chat.boardColor') }}</span>
          <button
            v-for="c in SWATCHES"
            :key="c"
            class="swatch"
            :class="{ active: !eraser && color === c }"
            :style="{ background: c }"
            :aria-label="c"
            @click="color = c; setEraser(false)"
          />
          <el-color-picker v-model="color" size="small" :predefine="SWATCHES" @change="setEraser(false)" />
        </span>

        <span class="tool-group">
          <el-button size="small" :disabled="undoStack.length === 0" @click="undo">↶ {{ t('chat.boardUndo') }}</el-button>
          <el-button size="small" @click="clearAll">🗑 {{ t('chat.boardClear') }}</el-button>
        </span>
      </div>

      <div class="board-canvas-wrap" :class="{ large: sizeLarge }">
        <canvas
          ref="canvasEl"
          :width="W"
          :height="H"
          class="board-canvas"
          @pointerdown="strokeStart"
          @pointermove="strokeMove"
          @pointerup="strokeEnd"
          @pointerleave="strokeEnd"
        />
      </div>

      <p class="board-hint nc-dim">{{ t('chat.boardHint') }}</p>
    </div>

    <template #footer>
      <el-button @click="close">{{ t('chat.boardCancel') }}</el-button>
      <el-button type="primary" @click="confirm">{{ t('chat.boardConfirm') }}</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.board-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.board-title {
  font-size: 15px;
  font-weight: 600;
}

.board-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.board-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 14px;
}

.tool-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.tool-label {
  font-size: 12px;
  color: var(--nc-text-dim);
  white-space: nowrap;
}

.size-slider {
  width: 110px;
  margin: 0 4px;
}

.colors {
  flex-wrap: wrap;
}

.swatch {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: 1px solid rgba(128, 128, 128, 0.45);
  cursor: pointer;
  padding: 0;
  flex: none;
}

.swatch.active {
  outline: 2px solid var(--el-color-primary);
  outline-offset: 1px;
}

.board-canvas-wrap {
  width: 100%;
  height: 300px;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--nc-border);
  background: #fff;
  cursor: crosshair;
}

.board-canvas-wrap.large {
  height: 560px;
}

.board-canvas {
  width: 100%;
  height: 100%;
  touch-action: none;
  display: block;
}

.board-hint {
  font-size: 12px;
  margin: 0;
}
</style>
