<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { kernel } from '@/kernel'
import type { AuthProviderDto } from '@/api/types'

const { t } = useI18n()
const router = useRouter()
const route = useRoute()
const loading = ref(false)
const authProviders = ref<AuthProviderDto[]>([])

const form = reactive({ username: '', password: '', authType: 'default' })

onMounted(async () => {
  try {
    authProviders.value = await kernel.auth.fetchAuthProviders()
  } catch {
    authProviders.value = []
  }
})

async function submit() {
  if (!form.username || !form.password) {
    ElMessage.warning(t('login.needCredentials'))
    return
  }
  loading.value = true
  try {
    await kernel.auth.login(form.username, form.password, form.authType)
    await kernel.settings.pullFromServer()
    await kernel.catalog.load()
    ElMessage.success(t('login.success'))
    const redirect = (route.query.redirect as string) || '/'
    void router.push(redirect)
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('login.failed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-wrap">
    <div class="login-card">
      <div class="logo">
        <svg class="logo-star" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z" />
        </svg>
        Next <strong>Chats</strong>
      </div>
      <el-form label-position="top" @keyup.enter="submit">
        <el-form-item v-if="authProviders.length">
          <el-radio-group v-model="form.authType" class="auth-types">
            <el-radio value="default">{{ t('login.authDefault') }}</el-radio>
            <el-radio v-for="p in authProviders" :key="p.name" :value="p.name">{{ p.name }}</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item :label="t('login.username')">
          <el-input v-model="form.username" :placeholder="t('login.username')" autocomplete="username" />
        </el-form-item>
        <el-form-item :label="t('login.password')">
          <el-input v-model="form.password" type="password" show-password :placeholder="t('login.password')" autocomplete="current-password" />
        </el-form-item>
        <el-button type="primary" class="submit" :loading="loading" @click="submit">{{ t('login.submit') }}</el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.login-wrap {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.login-card {
  width: 360px;
  padding: 36px 32px 24px;
  border-radius: 18px;
  background: var(--nc-surface);
  border: 1px solid var(--nc-border);
  backdrop-filter: blur(14px);
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.45);
}

.logo {
  font-size: 26px;
  letter-spacing: 0.5px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-bottom: 26px;
}

/* 发光五角星标识：主题色填充 + 同色光晕，与下方单选原点同色 */
.logo-star {
  width: 15px;
  height: 15px;
  fill: var(--el-color-primary);
  filter: drop-shadow(0 0 8px var(--el-color-primary));
  flex: none;
}

.auth-types {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
}

.submit {
  width: 100%;
  margin-top: 4px;
}
</style>
