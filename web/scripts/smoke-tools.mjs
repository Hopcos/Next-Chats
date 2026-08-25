// 冒烟测试二：ReAct 工具调用（tool:/danger: 触发） + 审批流 + 中断
const BASE = 'http://127.0.0.1:5210'

async function main() {
  const login = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'admin123' }),
  }).then((r) => r.json())
  const H = { Authorization: `Bearer ${login.token}`, 'Content-Type': 'application/json' }

  async function stream(sessionId, message) {
    const res = await fetch(`${BASE}/api/chat/stream`, {
      method: 'POST', headers: H,
      body: JSON.stringify({ sessionId, message, clientMessageId: crypto.randomUUID() }),
    })
    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buf = ''
    const events = []
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buf += decoder.decode(value, { stream: true })
      let i = buf.indexOf('\n\n')
      while (i >= 0) {
        const chunk = buf.slice(0, i); buf = buf.slice(i + 2)
        for (const line of chunk.split('\n')) {
          if (!line.startsWith('data:')) continue
          events.push(JSON.parse(line.slice(5).trim()))
        }
        i = buf.indexOf('\n\n')
      }
    }
    return events
  }

  // 1) 工具调用（echo）
  let sess = await fetch(`${BASE}/api/chat/sessions`, { method: 'POST', headers: H, body: JSON.stringify({ title: 'tool-demo' }) }).then((r) => r.json())
  let evs = await stream(sess.id, 'tool:echo')
  const kinds1 = evs.map((e) => e.kind)
  console.log('TOOL ECHO kinds:', [...new Set(kinds1)].join(','))
  const toolResult = evs.find((e) => e.kind === 'tool_result')
  console.log('  echo result preview:', JSON.stringify(toolResult?.resultPreview))
  const done1 = evs.find((e) => e.kind === 'done')
  console.log('  done tokens:', done1?.totalTokens, 'ttft:', done1?.ttftMs, 'ms')

  // 2) 危险工具 → 审批流（收到 approval 后自动批准）
  sess = await fetch(`${BASE}/api/chat/sessions`, { method: 'POST', headers: H, body: JSON.stringify({ title: 'approval-demo' }) }).then((r) => r.json())
  const res2 = await fetch(`${BASE}/api/chat/stream`, {
    method: 'POST', headers: H,
    body: JSON.stringify({ sessionId: sess.id, message: 'danger:delete_all', clientMessageId: crypto.randomUUID() }),
  })
  const reader2 = res2.body.getReader()
  const decoder2 = new TextDecoder()
  let buf2 = ''
  let approvalId = null
  const kinds2 = []
  let preview2 = null
  while (true) {
    const { done, value } = await reader2.read()
    if (done) break
    buf2 += decoder2.decode(value, { stream: true })
    let i = buf2.indexOf('\n\n')
    while (i >= 0) {
      const chunk = buf2.slice(0, i); buf2 = buf2.slice(i + 2)
      for (const line of chunk.split('\n')) {
        if (!line.startsWith('data:')) continue
        const ev = JSON.parse(line.slice(5).trim())
        kinds2.push(ev.kind)
        if (ev.kind === 'approval_updated' && ev.approvalStatus === 'pending' && ev.approvalId) {
          approvalId = ev.approvalId
          console.log('  → 收到审批请求:', ev.approvalId, '工具 delete_all，批准中...')
          await fetch(`${BASE}/api/admin/approvals/${ev.approvalId}/decide`, {
            method: 'POST', headers: H, body: JSON.stringify({ approved: true, reason: '自动化冒烟批准' }),
          })
        }
        if (ev.kind === 'tool_result') preview2 = ev.resultPreview
      }
      i = buf2.indexOf('\n\n')
    }
  }
  console.log('DANGER kinds:', [...new Set(kinds2)].join(','))
  console.log('  approval id seen:', !!approvalId, '| tool result:', JSON.stringify(preview2))
  const done2 = kinds2.includes('done')
  console.log('  stream finished with done:', done2)

  // 3) 审计 + 审批中心查询
  const approvals = await fetch(`${BASE}/api/admin/approvals?status=Approved`, { headers: H }).then((r) => r.json())
  console.log('APPROVALS(approved):', approvals.length)
  const audit = await fetch(`${BASE}/api/admin/audit`, { headers: H }).then((r) => r.json())
  const actions = audit.map((a) => a.action)
  console.log('AUDIT actions:', [...new Set(actions)].join(','))
}

main().catch((e) => { console.error('FAILED:', e.message); process.exit(1) })
