<script setup lang="ts">
/** 按内置图标库 key 渲染 SVG 图标 */
import { computed } from 'vue'
import { FALLBACK_ICON, getIcon } from '@/tools/icons'

const props = defineProps<{ icon: string; size?: number }>()
const paths = computed(() => (getIcon(props.icon) ?? getIcon(FALLBACK_ICON))!.paths)
</script>

<template>
  <svg
    class="tool-icon"
    :width="size ?? 24"
    :height="size ?? 24"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.6"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(d, i) in paths" :key="i" :d="d" />
  </svg>
</template>

<style scoped>
.tool-icon {
  display: block;
  flex-shrink: 0;
}
</style>
