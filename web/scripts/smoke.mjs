// 冒烟测试：登录 → 建会话 → SSE 流式对话 → 读消息
const BASE = 'http://127.0.0.1:5210'

async function main() {
  const login = await fetch(`${BASE}/api/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: 'admin', password: 'admin123' }),
  }).then((r) => r.json())
  console.log('LOGIN:', login.user.username, 'admin=' + login.user.isAdmin)
  const H = { Authorization: `Bearer ${login.token}`, 'Content-Type': 'application/json' }

  const sess = await fetch(`${BASE}/api/chat/sessions`, { method: 'POST', headers: H, body: JSON.stringify({ title: 'node-smoke' }) }).then((r) => r.json())
  console.log('SESSION:', sess.id)

  const res = await fetch(`${BASE}/api/chat/stream`, {
    method: 'POST', headers: H,
    body: JSON.stringify({ sessionId: sess.id, message: '你好，介绍一下你自己', clientMessageId: crypto.randomUUID() }),
  })
  console.log('STREAM HTTP:', res.status)
  if (!res.ok || !res.body) { console.log('ERR', await res.text()); return }
  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let buf = ''
  const kinds = {}
  let doneEv = null, firstText = '', textLen = 0, thinkingLen = 0
  let toolStart = 0
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buf += decoder.decode(value, { stream: true })
    let i = buf.indexOf('\n\n')
    while (i >= 0) {
      const chunk = buf.slice(0, i); buf = buf.slice(i + 2)
      for (const line of chunk.split('\n')) {
        if (!line.startsWith('data:')) continue
        const ev = JSON.parse(line.slice(5).trim())
        kinds[ev.kind] = (kinds[ev.kind] ?? 0) + 1
        if (ev.kind === 'text_delta') { if (!firstText) firstText = ev.text; textLen += (ev.text ?? '').length }
        if (ev.kind === 'thinking_delta') thinkingLen += (ev.text ?? '').length
        if (ev.kind === 'tool_start') toolStart++
        if (ev.kind === 'done') doneEv = ev
        if (ev.kind === 'error') console.log('EVENT ERROR:', ev.code, ev.message)
      }
      i = buf.indexOf('\n\n')
    }
  }
  console.log('KINDS:', JSON.stringify(kinds))
  console.log('DONE:', doneEv && `tokens=${doneEv.totalTokens} ttft=${doneEv.ttftMs}ms total=${doneEv.totalMs}ms model=${doneEv.model}`)
  console.log('TEXT:', textLen, 'chars, first chunk:', JSON.stringify(firstText), '| thinking chars:', thinkingLen, '| tool starts:', toolStart)

  const msgs = await fetch(`${BASE}/api/chat/sessions/${sess.id}/messages`, { headers: H }).then((r) => r.json())
  console.log('MSGS:', msgs.length, msgs.map((m) => `${m.role}:${m.status}:${(m.content ?? '').slice(0, 12)}...`).join(' | '))

  const metrics = await fetch(`${BASE}/api/admin/metrics/usage`, { headers: H }).then((r) => r.json())
  console.log('METRICS:', JSON.stringify(metrics.totals))
}

main().catch((e) => { console.error('FAILED:', e.message); process.exit(1) })
