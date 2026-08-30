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

const form = reactive({ username: 'admin', password: 'admin123', authType: 'default' })

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
        <span class="logo-dot" />
        Next <strong>Chats</strong>
      </div>
      <p class="tagline nc-dim">{{ t('login.tagline') }}</p>
      <el-form label-position="top" @keyup.enter="submit">
        <el-form-item v-if="authProviders.length" :label="t('login.authType')">
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
      <p class="hint nc-dim">{{ t('login.hint') }}</p>
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
  gap: 8px;
}

.logo-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--nc-primary);
  box-shadow: 0 0 14px var(--nc-primary);
}

.tagline {
  margin: 6px 0 24px;
  font-size: 13px;
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

.hint {
  font-size: 12px;
  text-align: center;
  margin-top: 20px;
}
</style>
