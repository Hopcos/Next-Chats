<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { http } from '@/api/http'
import type { LlmProviderDto, McpServerDto, PromptDto, RoleDto, SkillDto } from '@/api/types'
import { kernel } from '@/kernel'

const { t } = useI18n()

/** 当前账号只读：仅可查看后台，写操作全部禁用 */
const ro = computed(() => kernel.auth.state.user?.isReadonly ?? false)

const roles = ref<RoleDto[]>([])
const allMcps = ref<McpServerDto[]>([])
const allPrompts = ref<PromptDto[]>([])
const allSkills = ref<SkillDto[]>([])
const allProviders = ref<LlmProviderDto[]>([])
const loading = ref(false)
const bindDialog = ref(false)
const activeRole = ref<RoleDto | null>(null)

const bindings = reactive({ mcpServerIds: [] as string[], promptIds: [] as string[], skillIds: [] as string[], modelIds: [] as string[] })

async function createRole() {
  try {
    const { value: name } = await ElMessageBox.prompt(t('admin.roles.roleNamePrompt'), t('admin.roles.addTitle'), {
      inputPlaceholder: t('admin.roles.namePlaceholder'),
      confirmButtonText: t('admin.roles.next'),
    })
    const { value: code } = await ElMessageBox.prompt(t('admin.roles.roleCodePrompt'), t('admin.roles.addTitle'), {
      inputPlaceholder: t('admin.roles.codePlaceholder'),
      confirmButtonText: t('admin.roles.createBtn'),
    })
    if (!name || !code) {
      kernel.notify.warning(t('admin.roles.nameAndCodeRequired'))
      return
    }
    await http.post('/api/admin/roles', { name, code, description: undefined })
    kernel.notify.success(t('admin.roles.roleCreated'))
    await load()
  } catch {
    /* 用户取消 */
  }
}

async function load() {
  loading.value = true
  try {
    const [r, m, p, s, l] = await Promise.all([
      http.get<RoleDto[]>('/api/admin/roles'),
      http.get<McpServerDto[]>('/api/admin/mcp-servers'),
      http.get<PromptDto[]>('/api/admin/prompts'),
      http.get<SkillDto[]>('/api/admin/skills'),
      http.get<LlmProviderDto[]>('/api/admin/llm-providers'),
    ])
    roles.value = r
    allMcps.value = m
    allPrompts.value = p
    allSkills.value = s
    allProviders.value = l
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.roles.loadFailed'), (e as { code?: string }).code)
  } finally {
    loading.value = false
  }
}

function openBindings(row: RoleDto) {
  activeRole.value = row
  bindings.mcpServerIds = [...row.mcpServerIds]
  bindings.promptIds = [...row.promptIds]
  bindings.skillIds = [...row.skillIds]
  bindings.modelIds = [...row.modelIds]
  bindDialog.value = true
}

async function saveBindings() {
  const role = activeRole.value
  if (!role) return
  try {
    await http.put(`/api/admin/roles/${role.id}/bindings`, bindings)
    bindDialog.value = false
    kernel.notify.success(t('admin.roles.bindingsSaved'))
    await load()
  } catch (e) {
    kernel.notify.error((e as { message?: string }).message ?? t('admin.roles.saveFailed'), (e as { code?: string }).code)
  }
}

async function remove(row: RoleDto) {
  if (row.isSystem) {
    kernel.notify.warning(t('admin.roles.systemRoleProtected'))
    return
  }
  try {
    await ElMessageBox.confirm(t('admin.roles.deleteConfirm', { name: row.name }), t('common.delete'), { type: 'warning' })
  } catch {
    return
  }
  await http.delete(`/api/admin/roles/${row.id}`)
  await load()
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h3>{{ t('admin.roles.title') }}</h3>
      <el-button type="primary" :disabled="ro" @click="createRole">{{ t('admin.roles.create') }}</el-button>
    </div>

    <el-table :data="roles" v-loading="loading" size="small" stripe>
      <el-table-column prop="name" :label="t('common.name')" width="140" />
      <el-table-column prop="code" :label="t('admin.roles.code')" width="120">
        <template #default="{ row }"><span class="nc-mono">{{ row.code }}</span></template>
      </el-table-column>
      <el-table-column prop="description" :label="t('common.description')" min-width="180" show-overflow-tooltip />
      <el-table-column :label="t('admin.roles.bindings')" width="200">
        <template #default="{ row }">
          <el-tag size="small" type="warning">MCP {{ row.mcpServerIds.length }}</el-tag>
          <el-tag size="small" type="primary" style="margin-left: 4px">Prompt {{ row.promptIds.length }}</el-tag>
          <el-tag size="small" type="success" style="margin-left: 4px">SKILL {{ row.skillIds.length }}</el-tag>
          <el-tag size="small" type="info" style="margin-left: 4px">LLM {{ row.modelIds.length }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('admin.roles.system')" width="70">
        <template #default="{ row }">
          <el-tag v-if="row.isSystem" size="small">{{ t('admin.roles.builtin') }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column :label="t('common.actions')" width="160" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain :disabled="ro" @click="openBindings(row)">{{ t('admin.roles.bind') }}</el-button>
          <el-button size="small" text type="danger" :disabled="ro" @click="remove(row)">{{ t('common.delete') }}</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="bindDialog" :title="t('admin.roles.bindingsTitle', { name: activeRole?.name ?? '' })" width="620px">
      <h4 class="sec">{{ t('admin.roles.secMcp') }}</h4>
      <el-checkbox-group v-model="bindings.mcpServerIds" class="vert">
        <el-checkbox v-for="m in allMcps" :key="m.id" :value="m.id">
          {{ t('admin.roles.serverWithTools', { name: m.name, count: m.toolCount }) }}
        </el-checkbox>
      </el-checkbox-group>

      <h4 class="sec">{{ t('admin.roles.secPrompts') }}</h4>
      <el-checkbox-group v-model="bindings.promptIds" class="vert">
        <el-checkbox v-for="p in allPrompts" :key="p.id" :value="p.id">{{ p.name }}</el-checkbox>
      </el-checkbox-group>

      <h4 class="sec">{{ t('admin.roles.secSkills') }}</h4>
      <el-checkbox-group v-model="bindings.skillIds" class="vert">
        <el-checkbox v-for="s in allSkills" :key="s.id" :value="s.id">{{ s.name }}</el-checkbox>
      </el-checkbox-group>

      <h4 class="sec">{{ t('admin.roles.secModels') }}</h4>
      <el-checkbox-group v-model="bindings.modelIds" class="vert">
        <template v-for="pr in allProviders" :key="pr.id">
          <div v-if="pr.models.length" class="prov">{{ pr.name }}</div>
          <el-checkbox v-for="m in pr.models" :key="m.id" :value="m.id" :disabled="!m.enabled">
            {{ m.name }}<span v-if="pr.name" class="muted"> · {{ pr.name }}</span>
          </el-checkbox>
        </template>
      </el-checkbox-group>

      <template #footer>
        <el-button @click="bindDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" :disabled="ro" @click="saveBindings">{{ t('admin.roles.saveBindings') }}</el-button>
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

.sec {
  margin: 14px 0 8px;
}

.vert {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
}

.prov {
  font-weight: 600;
  margin: 8px 0 2px;
  color: var(--nc-text-2, #606266);
}

.muted {
  opacity: 0.65;
  font-size: 12px;
}
</style>
