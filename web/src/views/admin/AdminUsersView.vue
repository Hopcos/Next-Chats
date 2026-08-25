<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import type { RoleDto, UserDto } from '@/api/types'
import { kernel } from '@/kernel'

const { t } = useI18n()

const users = ref<UserDto[]>([])
const roles = ref<RoleDto[]>([])
const loading = ref(false)
const dialogOpen = ref(false)
const editingId = ref<string | null>(null)

const form = reactive({
  username: '', displayName: '', email: '', password: '', status: 'Active', roleIds: [] as string[],
})

async function load() {
  loading.value = true
  try {
    const [u, r] = await Promise.all([
      http.get<UserDto[]>('/api/admin/users'),
      http.get<RoleDto[]>('/api/admin/roles'),
    ])
    users.value = u
    roles.value = r
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.users.loadFailed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { username: '', displayName: '', email: '', password: '', status: 'Active', roleIds: [] })
  dialogOpen.value = true
}

function openEdit(row: UserDto) {
  editingId.value = row.id
  Object.assign(form, {
    username: row.username, displayName: row.displayName ?? '', email: row.email ?? '',
    password: '', status: row.status, roleIds: row.roles.map((r) => r.id),
  })
  dialogOpen.value = true
}

async function save() {
  try {
    const body = {
      username: form.username, displayName: form.displayName, email: form.email,
      password: form.password || undefined, status: form.status, roleIds: form.roleIds,
    }
    if (editingId.value) {
      await http.put(`/api/admin/users/${editingId.value}`, body)
    } else {
      await http.post('/api/admin/users', body)
    }
    dialogOpen.value = false
    kernel.notify.success(t('admin.users.saved'))
    await load()
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.users.saveFailed'), (e as { code?: string }).code)
  }
}

async function remove(row: UserDto) {
  try {
    await ElMessageBox.confirm(t('admin.users.deleteConfirm', { username: row.username }), t('common.delete'), { type: 'warning' })
  } catch {
    return
  }
  await http.delete(`/api/admin/users/${row.id}`)
  await load()
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h3>{{ t('admin.users.title') }}</h3>
      <el-button type="primary" @click="openCreate">{{ t('admin.users.create') }}</el-button>
    </div>
    <el-table :data="users" v-loading="loading" size="small" stripe>
      <el-table-column prop="username" :label="t('admin.users.username')" width="140" />
      <el-table-column prop="displayName" :label="t('admin.users.displayName')" width="120" />
      <el-table-column prop="email" :label="t('admin.users.email')" min-width="160" />
      <el-table-column :label="t('admin.users.roles')" min-width="160">
        <template #default="{ row }">
          <el-tag v-for="r in row.roles" :key="r.id" size="small" :type="r.code === 'admin' ? 'danger' : 'primary'" style="margin: 1px">
            {{ r.name }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('common.status')" width="80">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Active' ? 'success' : 'info'" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('common.actions')" width="140" fixed="right">
        <template #default="{ row }">
          <el-button size="small" text @click="openEdit(row)">{{ t('common.edit') }}</el-button>
          <el-button size="small" text type="danger" @click="remove(row)">{{ t('common.delete') }}</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogOpen" :title="editingId ? t('admin.users.editTitle') : t('admin.users.addTitle')" width="480px">
      <el-form label-width="90px" label-position="left">
        <el-form-item :label="t('admin.users.username')" required><el-input v-model="form.username" /></el-form-item>
        <el-form-item :label="t('admin.users.displayName')"><el-input v-model="form.displayName" /></el-form-item>
        <el-form-item :label="t('admin.users.email')"><el-input v-model="form.email" /></el-form-item>
        <el-form-item :label="t('admin.users.password')">
          <el-input v-model="form.password" type="password" show-password :placeholder="editingId ? t('admin.users.passwordPlaceholderEdit') : t('admin.users.passwordPlaceholderNew')" />
        </el-form-item>
        <el-form-item :label="t('common.status')">
          <el-select v-model="form.status">
            <el-option :label="t('admin.users.statusActive')" value="Active" />
            <el-option :label="t('admin.users.statusDisabled')" value="Disabled" />
          </el-select>
        </el-form-item>
        <el-form-item :label="t('admin.users.roles')">
          <el-select v-model="form.roleIds" multiple>
            <el-option v-for="r in roles" :key="r.id" :label="r.name" :value="r.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogOpen = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="save">{{ t('common.save') }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
}
</style>
