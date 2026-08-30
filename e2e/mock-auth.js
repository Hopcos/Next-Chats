// Mock 内部鉴权中心（仅用于 E2E）：监听 127.0.0.1:53131
//   POST /login          -> 密码 secret123 返回 {"sessionID":"S-xxx"}，否则 401
//   GET  /login-check    -> 返回 {"data":{"sessionID":"S-abc"}}（验证点路径 + Equals 规则）
//   GET  /login-badjson  -> 返回 200 但非 JSON 文本（验证无效响应走凭据错误）
const http = require('http');

const server = http.createServer((req, res) => {
  const send = (status, obj) => {
    res.writeHead(status, { 'Content-Type': 'application/json' });
    res.end(typeof obj === 'string' ? obj : JSON.stringify(obj));
  };

  if (req.url === '/login' && (req.method === 'POST' || req.method === 'PUT')) {
    let body = '';
    req.on('data', (c) => (body += c));
    req.on('end', () => {
      let parsed = {};
      try {
        parsed = JSON.parse(body);
      } catch {
        /* ignore */
      }
      if (parsed.password === 'secret123') {
        send(200, { sessionID: 'S-' + Math.random().toString(36).slice(2), status: 'ok' });
      } else {
        send(401, { error: 'bad_credentials' });
      }
    });
    return;
  }
  if (req.url === '/login-check' && req.method === 'GET') {
    send(200, { data: { sessionID: 'S-abc' } });
    return;
  }
  if (req.url === '/login-badjson') {
    res.writeHead(200, { 'Content-Type': 'text/plain' });
    res.end('this is not json');
    return;
  }
  send(404, { error: 'not_found' });
});

server.listen(53131, '127.0.0.1', () => console.log('mock auth center listening on 127.0.0.1:53131'));
