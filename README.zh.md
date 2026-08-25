# Next Chats

> [**English README**](./README.md)

基于 **DeepSeek Harness 原理** 的 B/S 多模态 AI 聊天平台：LLM + MCP（Model Context Protocol）+ Skill 插件化编排，前端由 **Cordis** 插件内核驱动 Three.js 轻量 3D 界面。

> 参照论文：*A Programming Paradigm for Spatiotemporal Composability* —— 一切皆插件，由 Cordis 驱动。

**多语言界面**：中文（简体）与 English 双语，默认英文；右上角 🌐 一键切换，选择保存在 `localStorage` 自动记住。

---

## 架构与流程（MERMAID）

### 数据模型（ER 图）

```mermaid
erDiagram
    APP_USER {
        Guid id PK
        string username UK 
        string display_name "显示名"
        string email "邮箱"
        string password_hash "PBKDF2 哈希"
        string password_salt 
        string status "状态"
        datetime created_at "创建时间"
        datetime last_login_at "最后登录"
    }
    APP_ROLE {
        Guid id PK 
        string name "角色名"
        string code UK
        string description 
        bool is_system "内置角色"
        datetime created_at
    }
    CHAT_SESSION {
        Guid id PK
        Guid user_id FK 
        string title "会话标题"
        string status 
        Guid llm_provider_id "使用的模型"
        string context_json "压缩摘要"
        datetime created_at
        datetime updated_at
        datetime last_message_at
    }
    CHAT_MESSAGE {
        Guid id PK
        Guid session_id FK
        Guid user_id FK 
        string role "角色"
        text content "正文"
        text reasoning "思考"
        string tool_calls_json "工具卡片"
        string status
        string model
        int prompt_tokens
        int completion_tokens
        string trace_id 
        string client_message_id UK "幂等"
        datetime created_at
    }
    LLM_PROVIDER {
        Guid id PK 
        string name "供应商"
        string kind "类型"
        string base_url 
        string api_key_encrypted "AES-GCM 加密"
        string model 
        int context_window "上下文窗口"
        int priority "优先级"
        bool enabled 
        bool is_vision "视觉支持"
        bool is_healthy "健康"
        datetime created_at
    }
    MCP_SERVER {
        Guid id PK 
        string name "MCP 服务器"
        string transport "传输方式"
        string endpoint "端点"
        string headers_json "请求头(加密)"
        string stdio_command
        bool enabled 
        bool is_vision "视觉支持"
        int timeout_seconds
        string description
        datetime created_at
    }
    MCP_CATALOG_ITEM {
        Guid id PK
        Guid mcp_server_id FK 
        string kind "类型(工具/提示/资源)"
        string name
        string description 
        string schema_json "参数 Schema"
        bool enabled "可单独禁用"
    }
    PROMPT {
        Guid id PK
        string name
        string description 
        text content "模板内容"
        bool enabled
        int version
        datetime created_at
    }
    SKILL {
        Guid id PK
        string name 
        string meta_tool_name "元工具名"
        text instruction "指令(懒加载)"
        bool enabled
        string model_override
        int max_nested_steps
    }
    TOOL_APPROVAL {
        Guid id PK
        string trace_id
        Guid user_id FK
        Guid session_id FK
        string mcp_server_name 
        string tool_name "危险工具"
        string arguments_json 
        string status "审批状态"
        datetime created_at 
        datetime decided_at "决策时间"
        datetime expires_at "过期时间"
    }
    AUDIT_LOG {
        Guid id PK
        string trace_id
        Guid user_id FK
        string category
        string action
        string target 
        string detail_json "脱敏细节"
        string ip
        bool is_suspicious
        datetime created_at
    }
    USER_SETTING {
        Guid id PK
        Guid user_id FK
        string key
        string value_json
    }
    TOKEN_USAGE_RECORD {
        Guid id PK
        string trace_id UK
        Guid user_id FK
        Guid session_id FK
        string provider_name
        string model
        int total_tokens 
        decimal cost "成本"
        int ttft_ms "首字时延"
        datetime created_at
    }

    APP_USER ||--o{ CHAT_SESSION : "拥有"
    APP_USER ||--o{ CHAT_MESSAGE : "发送"
    APP_USER }o--o{ APP_ROLE : "属于 (user_roles)"
    APP_USER ||--o{ USER_SETTING : "设置"
    APP_ROLE }o--o{ MCP_SERVER : "可用 (role_mcp_servers)"
    APP_ROLE }o--o{ PROMPT : "可用 (role_prompts)"
    APP_ROLE }o--o{ SKILL : "可用 (role_skills)"
    MCP_SERVER ||--o{ MCP_CATALOG_ITEM : "自动带出目录"
    CHAT_SESSION ||--o{ CHAT_MESSAGE : "包含"
    CHAT_SESSION }o--o| LLM_PROVIDER : "使用模型"
    TOOL_APPROVAL }o--|| APP_USER : "申请人"
    TOOL_APPROVAL }o--|| CHAT_SESSION : "所属会话"
    AUDIT_LOG }o--o| APP_USER : "操作者"
    TOKEN_USAGE_RECORD }o--o| APP_USER : "用量归属"
```

### 系统架构

```mermaid
flowchart LR
    subgraph Web["Web（Vue 3 + Cordis）"]
        UI[聊天界面 / 3D 主题] --> K[内核插件]
        K -->|SSE / REST| HTTP[Vite 代理 → /api]
    end

    subgraph Api["NextChats.Api（ASP.NET Core）"]
        Ctl[控制器 / RBAC / 审计] --> Mod[错误本地化中间件]
        Ctl --> Orch{{ChatOrchestrator 编排}}
        Ctl --> Adm[管理端：供应商 / MCP / Prompt / Skill / RBAC / 审批]
    end

    Orch --> Loop{{AgentLoopEngine 推理循环}}
    Loop -->|ReAct 思考→行动→观察| Router[LLM Router 路由]
    Router --> OpenAI[OpenAI 兼容客户端]
    Router --> Mock[Mock 客户端：按语言输出]
    Orch --> Driver[MCP Driver]
    Driver --> MCP1[(MCP Server：演示 5300)]
    Driver --> MCP2[(更多 MCP Server…)]
    Orch --> Skills[Skill 执行引擎]
    Orch --> Policy[策略引擎 / 审批协调器]

    Api --> DB[(SQLite：配置 / 聊天 / 审计)]
    UI --> V3[Three.js 场景]

    style Orch fill:#e3f2fd
    style Loop fill:#fff3e0
```

### 聊天（ReAct）流程 —— SSE 流式

```mermaid
sequenceDiagram
    participant U as 用户（浏览器）
    participant K as Cordis 内核
    participant C as ChatController
    participant O as ChatOrchestrator
    participant L as AgentLoopEngine
    participant LLM as LLM 客户端
    participant M as MCP Driver / Skill

    U->>K: 输入消息（可附图片）
    K->>C: POST /api/chat/stream（SSE）
    C->>O: ChatStreamRequest（lang / images）
    O-->>C: 会话不存在 / 注入拦截？（error 事件）
    O->>L: AgentRunRequest（工具 / 上下文窗口）
    loop ReAct 轮次
        L->>LLM: 流式提示词 + 工具
        LLM-->>L: 思考 / 正文 / 工具调用分片
        L-->>U: thinking_start · thinking_delta · text_delta（SSE）
        alt 工具调用
            L->>Policy: 策略评估（允许 / 拒绝 / 审批）
            Policy-->>U: approval_updated（pending）→ 用户批准
            L->>M: 执行工具（审批通过 / 参数）
            M-->>L: 工具结果
            L-->>U: tool_start · tool_result（SSE）
        end
    end
    L-->>U: done（用量：tokens / ttft / cost）
    O->>DB: 持久化助手消息 + 用量 + 审计（trace_id）
    O-->>C: SSE end 事件
    C-->>K: 事件流 → 界面渲染
```

### 图片 / 视觉流程（image_source 标准 base64）

```mermaid
sequenceDiagram
    participant U as 用户（浏览器）
    participant B as 输入条 ChatInputBar
    participant C as ChatController
    participant O as ChatOrchestrator
    participant V as 视觉 MCP 工具（image_source）
    participant LLM as LLM（Mock / 真实）
    participant P as 供应商 · MCP 视觉标记

    U->>C: GET /api/chat/vision-config
    C->>P: provider.IsVision ∨ 已绑定 MCP.IsVision
    P-->>U: supported: true → 显示上传按钮 + Ctrl+V 粘贴

    U->>B: 粘贴（Ctrl+V）或上传 N 张图片
    B->>B: 校验类型/大小 → dataURL → base64
    B->>C: POST /api/chat/stream { message, images[] }
    C->>O: ChatStreamRequest.Images（标准 base64）
    loop 每张图片（多张逐个识别为文本）
        O->>V: 调用视觉工具 { image_source: base64 }
        V-->>O: 识别文本（逐张）
    end
    O->>LLM: system + 历史 + [识别文本] + 用户消息（含 <image_source> 块）
    LLM-->>U: 基于识别文本的流式回答（SSE）
```

## 技术栈

| 层 | 技术 |
| --- | --- |
| 后端 | .NET 10 · ASP.NET Core · EF Core（现 SQLite → 可切 MySQL 8）|
| MCP | **ModelContextProtocol SDK 2.2.0**（最新稳定版）· Streamable HTTP 传输 · STDIO 可扩展 |
| 前端 | Vue 3 · TypeScript · Vite · Element Plus · Three.js · **Cordis ^3.18.1**（插件内核）|
| 安全 | JWT · PBKDF2-SHA256(210k) 密码 · AES-256-GCM 密钥加密 · 审计日志脱敏 |
| 国际化 | vue-i18n 10（中英双语，默认英文）· 后端错误经 `X-Lang` 请求头本地化 |

## 目录结构

```
next-chats/
├── src/
│   ├── NextChats.Core/           # 领域模型、编排层、引擎、驱动（可移植，无 ASP.NET 依赖）
│   ├── NextChats.Infrastructure/ # EF Core 数据 + 缓存 + 安全服务（可替换实现）
│   └── NextChats.Api/            # Web API（SSE 流、管理端、RBAC、审计、指标、i18n）
├── samples/McpDemoServer/        # 演示 MCP Server（Streamable HTTP）
└── web/                          # Vue 3 + Cordis 前端
    ├── src/i18n/                 # en / zh 分域语言包
    └── scripts/smoke*.mjs        # 端到端冒烟脚本（Node fetch 流式读取）
```

## 快速开始

```bash
# 1. 启动演示 MCP Server（端口 5300）
dotnet run --project samples/McpDemoServer -c Debug

# 2. 启动 API（端口 5210；首次启动自动建库 + 种子数据）
dotnet run --project src/NextChats.Api -c Debug

# 3. 启动前端（Vite 代理 /api → 5210）
cd web && npm install && npm run dev
# 打开 http://localhost:5173 （种子管理员：admin / admin123）
```

## 核心设计

### 编排层（Core）
- **有效交集工具集**：`角色绑定 ∩ 用户启用 ∩ MCP 全局启用` 取交集，逐用户隔离。
- **活跃 Skill 匹配**：Skill 以「元工具」形式暴露给模型（`skill_<slug>`），指令按需懒加载，避免 token 爆炸。
- **Prompt 构建**：模板引擎（`{{var}}` / `#if` / `#each` / `#section`）渲染系统提示。
- **ReAct 循环（AgentLoopEngine）**：生产者-消费者 Channel 架构；LLM 流式事件与循环主流程解耦，可安全中断。

### 引擎（Core）
- **LLM Router**：多供应商优先级 + 轮询 + 故障转移（mark-unhealthy 熔断），OpenAI 兼容 / Mock 双客户端。
- **MCP 驱动**：严格最新 MCP 规范（引用 [MCP 2026-07 修订](https://blog.modelcontextprotocol.io/posts/2026-07-28/)）；连接池复用；调用重试（指数退避）；
  **MCP 错误进循环** —— 工具错误作为工具结果回灌给模型用于重试/解释，会话绝不因此中断。
- **策略引擎**：`Allow → Deny → RequireApproval` 三级裁决 + 危险操作符号名启发式（如 `delete_all`）；审批支持 pending/approved/rejected/expired。
- **上下文管理**：token 估算 → 压缩（LLM 摘要）→ 截断，绝不超过模型长度上限，全程不中断会话。

### 服务端统一配置（管理端）
- LLM 供应商（启用开关 / model / endpoint / api-key 加密 / temperature / context-window）
- MCP 服务器（Name / Endpoint / Headers JSON 加密保存；**「获取」自动带出** tools/prompts/resources 并以 Schema 落库；逐项禁用）
- Prompt 多套、Skill 多套（可插拔懒加载）
- RBAC：用户 / 角色；角色 ↔ MCP / Prompt / Skill 绑定

### 可观测性与成本
- `trace_id`（`trc_...`）贯穿 Orchestration / LLM / MCP / 审计；token 进出统计；指标：TTFT、tokens、cost、工具时延、审批数（`/api/admin/metrics/usage`）。
- 幂等写入：（UserId, ClientMessageId）唯一键；关键聊天日志持久化，重启不丢消息。

### 日志三通道
- **给用户**：友好文案 + 错误码（无 stack / endpoint / header）
- **给模型**：工具错误作为 tool result 回灌（可重试、可解释）
- **给日志**：完整上下文（含脱敏后的）

## 国际化
- 前端：`vue-i18n`，语言包按域拆分于 `web/src/i18n/locales/{en,zh}/`，默认英文。
- 后端：所有用户可见文案集中在单一字典 `src/NextChats.Core/Localization/Texts.cs`（中英双语），代码中不再硬编码界面文案；错误响应经 `X-Lang` 请求头（或 `Accept-Language`）本地化，非浏览器客户端（如 `curl -H "X-Lang: zh"`）同样获得对应语言；种子数据（角色/Prompt/Skill/演示供应商）为英文。
- 本文档与 [README.md（英文）](./README.md) 顶部互链。

## 冒烟验证（web/scripts/）

```bash
node scripts/smoke.mjs          # 登录 → 建会话 → SSE 流式对话 → 消息持久化 → 指标
node scripts/smoke-tools.mjs    # ReAct 工具调用（tool:echo）→ 危险工具审批流（danger:delete_all）
```

## MCP 参考
- [What's new in MCP (2026-07-28)](https://blog.modelcontextprotocol.io/posts/2026-07-28/)
- ModelContextProtocol C# SDK 2.2.0：`McpClient.CreateAsync` + `HttpClientTransport`（Streamable HTTP）/ `StdioClientTransport`
