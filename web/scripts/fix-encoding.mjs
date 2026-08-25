// 重建 MCP 演示数据（UTF-8）：创建「演示服务器」→ 获取目录 → 绑定 admin 角色 → 危险审批验证中文显示
const BASE = 'http://127.0.0.1:5210'

async function main() {
  const login = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'admin123' }),
  }).then((r) => r.json())
  const H = { Authorization: `Bearer ${login.token}`, 'Content-Type': 'application/json' }
  console.log('登录:', login.user.username, 'isAdmin=' + login.user.isAdmin)

  // 1) 创建（中文名称走 UTF-8，模拟真实浏览器写入路径）
  const mcp = await fetch(`${BASE}/api/admin/mcp-servers`, {
    method: 'POST', headers: H,
    body: JSON.stringify({ name: '演示服务器', transport: 'Http', endpoint: 'http://127.0.0.1:5300/mcp', enabled: true, isVision: true, timeoutSeconds: 30 }),
  })
  console.log('创建 MCP:', mcp.status)
  const mcpId = JSON.parse(await mcp.text())

  // 2) 获取目录
  const fetchRes = await fetch(`${BASE}/api/admin/mcp-servers/${mcpId}/fetch`, { method: 'POST', headers: H, body: '{}' }).then((r) => r.json())
  console.log('发现:', fetchRes.items.length, '个工具')

  // 3) 绑定 admin 角色
  const roles = await fetch(`${BASE}/api/admin/roles`, { headers: H }).then((r) => r.json())
  const adminRole = roles.find((r) => r.code === 'admin')
  await fetch(`${BASE}/api/admin/roles/${adminRole.id}/bindings`, {
    method: 'PUT', headers: H, body: JSON.stringify({ mcpServerIds: [mcpId], promptIds: [], skillIds: [] }),
  })
  console.log('角色绑定完成')

  // 4) 校验：读取回来的名称必须与提交一致（中文不乱码）
  const all = await fetch(`${BASE}/api/admin/mcp-servers`, { headers: H }).then((r) => r.json())
  const srv = all.find((s) => s.id === mcpId)
  console.log('名称回读:', JSON.stringify(srv.name), '| 与提交一致:', srv.name === '演示服务器')

  // 5) 跑一次危险审批，验证审批中心 serverName 中文
  const sess = await fetch(`${BASE}/api/chat/sessions`, { method: 'POST', headers: H, body: JSON.stringify({ title: '编码验证' }) }).then((r) => r.json())
  const res = await fetch(`${BASE}/api/chat/stream`, {
    method: 'POST', headers: H,
    body: JSON.stringify({ sessionId: sess.id, message: 'danger:delete_all', clientMessageId: crypto.randomUUID() }),
  })
  const reader = res.body.getReader(); const dec = new TextDecoder(); let buf = ''
  while (true) {
    const { done, value } = await reader.read(); if (done) break
    buf += dec.decode(value, { stream: true })
    let i = buf.indexOf('\n\n')
    while (i >= 0) {
      const chunk = buf.slice(0, i); buf = buf.slice(i + 2)
      for (const line of chunk.split('\n')) {
        if (!line.startsWith('data:')) continue
        const ev = JSON.parse(line.slice(5).trim())
        if (ev.kind === 'approval_updated' && ev.approvalStatus === 'pending') {
          await fetch(`${BASE}/api/admin/approvals/${ev.approvalId}/decide`, { method: 'POST', headers: H, body: JSON.stringify({ approved: true }) })
          console.log('审批批准:', ev.approvalId)
        }
      }
      i = buf.indexOf('\n\n')
    }
  }

  const apps = await fetch(`${BASE}/api/admin/approvals`, { headers: H }).then((r) => r.json())
  const latest = apps[0]
  console.log('审批中心最新: serverName=[' + latest.mcpServerName + '] tool=' + latest.toolName + ' status=' + latest.status)
  console.log('审批显示中文:', latest.mcpServerName === '演示服务器' ? 'PASS ✅' : 'FAIL ❌')
}

main().catch((e) => { console.error('FAILED:', e.message); process.exit(1) })
