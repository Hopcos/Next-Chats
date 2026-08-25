<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { kernel } from '@/kernel'
import { getLang } from '@/i18n'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import enLocale from 'element-plus/es/locale/lang/en'
import type { Language } from 'element-plus/es/locale'
import ThreeBackground from '@/components/ThreeBackground.vue'
import ApprovalDialog from '@/components/ApprovalDialog.vue'

const route = useRoute()

const showThreeBg = computed(() => kernel.settings.state.threeEnabled && route.name !== 'admin')

// Element Plus 组件语言跟随应用语言（localStorage 持久化）
const elLocale = computed<Language>(() => (getLang() === 'zh' ? zhCn : enLocale))
</script>

<template>
  <ThreeBackground v-if="showThreeBg" />
  <el-config-provider :locale="elLocale">
    <div class="app-shell">
      <router-view />
      <ApprovalDialog />
    </div>
  </el-config-provider>
</template>

<style scoped>
.app-shell {
  position: relative;
  z-index: 1;
  height: 100%;
}
</style>
