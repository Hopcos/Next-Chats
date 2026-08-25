using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NextChats.Core.Abstractions;
using NextChats.Core.Agents;
using NextChats.Core.Clients;
using NextChats.Core.Configuration;
using NextChats.Core.Domain;
using NextChats.Core.Entities;
using NextChats.Core.Localization;

namespace NextChats.Core.Services;

/// <summary>
/// 编排层（Orchestration）：
///  有效交集工具收集（角色绑定 ∩ 用户启用 ∩ Server 启用 ∩ 项级启用）
///  → 匹配激活 Skills（懒加载元工具）
///  → 构建 Prompt（模板引擎渲染 + 注入上下文）
///  → 执行推理循环（ReAct）
///  → 持久化消息/用量/审计（trace_id 贯穿）。
/// </summary>
public sealed class ChatOrchestrator : IChatOrchestrator
{
    private const string SettingProvider = "chat.providerId";
    private const string SettingModel = "chat.modelId";
    private const string SettingPrompt = "chat.promptId";
    private const string SettingMcpServers = "chat.mcpServers";
    private const string SettingSkills = "chat.skills";

    private static readonly decimal PricePer1KInput = 0.0005m;   // 默认 $0.5/M（可通过 provider.ExtraJson 覆盖）
    private static readonly decimal PricePer1KOutput = 0.0015m;

    private readonly IConfigStore _config;
    private readonly IChatStore _chat;
    private readonly IMcpDriver _mcp;
    private readonly ISkillExecutionEngine _skills;
    private readonly IAgentLoopEngine _loop;
    private readonly IPromptTemplateEngine _templates;
    private readonly IAuditLogger _audit;
    private readonly ISecurityService _security;
    private readonly ISessionCancellationRegistry _cancellations;
    private readonly ICacheService _cache;
    private readonly IOptions<SecurityOptions> _securityOptions;
    private readonly ILogger _logger;

    public ChatOrchestrator(
        IConfigStore config,
        IChatStore chat,
        IMcpDriver mcp,
        ISkillExecutionEngine skills,
        IAgentLoopEngine loop,
        IPromptTemplateEngine templates,
        IAuditLogger audit,
        ISecurityService security,
        ISessionCancellationRegistry cancellations,
        ICacheService cache,
        IOptions<SecurityOptions> securityOptions,
        ILogger<ChatOrchestrator> logger)
    {
        _config = config;
        _chat = chat;
        _mcp = mcp;
        _skills = skills;
        _loop = loop;
        _templates = templates;
        _audit = audit;
        _security = security;
        _cancellations = cancellations;
        _cache = cache;
        _securityOptions = securityOptions;
        _logger = logger;
    }

    public async IAsyncEnumerable<AgentEvent> StreamAsync(ChatStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var trace = $"trc_{Guid.NewGuid():N}"[..24];
        var lang = request.Lang ?? "en";
        _logger.LogInformation("[Orchestrator] start trace={Trace} user={User} session={Session}", trace, request.UserId, request.SessionId);
        var session = await _chat.GetSessionAsync(request.UserId, request.SessionId, ct);
        if (session is null)
        {
            yield return AgentEvent.Error("SESSION_NOT_FOUND", Texts.Get("SESSION_NOT_FOUND", lang), trace);
            yield break;
        }

        // ---------- 写操作幂等 ----------
        if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            var key = IdempotencyKey(request.UserId, request.ClientMessageId);
            var cached = await _chat.GetIdempotencyAsync(request.UserId, key, ct);
            if (cached is not null)
            {
                yield return new AgentEvent { Kind = "duplicate", TraceId = trace, MessageId = ExtractString(cached.ResponseJson, "messageId") };
                yield return AgentEvent.Done(new JsonUsage(), 0, 0, 0, trace);
                yield break;
            }
        }

        // ---------- 输入 Injection 检测及过滤 ----------
        // 话题级重新生成：以历史中该条 user 提问作为本轮输入（不追加新 user 消息）
        var regenMessage = request.RegenerateFromMessageId is { } regenId
            ? (await _chat.ListMessagesAsync(request.UserId, session.Id, ct))
                .FirstOrDefault(m => m.Id == regenId && m.Role == ChatRole.User)
            : null;
        if (request.RegenerateFromMessageId is not null && regenMessage is null)
        {
            yield return AgentEvent.Error("MESSAGE_NOT_FOUND", Texts.Get("MESSAGE_NOT_FOUND", lang), trace);
            yield break;
        }
        var rawInput = regenMessage?.Content ?? request.UserInput;
        var (sanitized, flagged, hints) = _security.SanitizeUserInput(rawInput);
        await _audit.RecordAsync(
            flagged ? AuditCategory.Security : AuditCategory.Chat,
            flagged ? "INPUT_INJECTION_FLAGGED" : "CHAT.START",
            trace, request.UserId, session.Id.ToString(),
            flagged ? new { hints, originalLength = request.UserInput.Length } : null,
            isSuspicious: flagged, ct: ct);

        if (flagged && !_securityOptions.Value.ProceedOnInjection)
        {
            yield return AgentEvent.Error("INPUT_FLAGGED", Texts.Get("INPUT_FLAGGED", lang), trace);
            yield break;
        }
        var userInput = sanitized;

        // ---------- 图片附件（标准 base64，命名约定 image_source；多张逐个识别为文本） ----------
        // 识别通过 MCP 视觉工具完成（参数名 image_source = 标准 base64）；识别结果以 context 事件展示。
        // 无论识别成功与否，都不把 base64 原文拼进发给模型的文本（模型无法直接“看图”，避免浪费上下文并误导模型去解析 base64）。
        const int MaxImages = 6;
        const int MaxImageBase64Chars = 5_000_000;
        if (request.Images is { Count: > 0 } images)
        {
            if (images.Count > MaxImages)
            {
                yield return AgentEvent.Error("IMAGE_TOO_MANY", Texts.Get("IMAGE_TOO_MANY", lang, MaxImages), trace);
                yield break;
            }
            foreach (var img in images)
            {
                if (string.IsNullOrWhiteSpace(img.Base64) || img.Base64.Length > MaxImageBase64Chars || !IsStandardBase64(img.Base64))
                {
                    yield return AgentEvent.Error("IMAGE_INVALID", Texts.Get("IMAGE_INVALID", lang), trace);
                    yield break;
                }
            }
        }

        _logger.LogInformation("[Orchestrator] sanitized trace={Trace} flagged={Flagged}", trace, flagged);

        // ---------- 持久化用户消息（重新生成模式：该条提问已在库中，不再追加） ----------
        var userMessage = regenMessage;
        if (userMessage is null)
        {
            userMessage = await _chat.AppendMessageAsync(new ChatMessage
            {
                SessionId = session.Id,
                UserId = request.UserId,
                Role = ChatRole.User,
                Content = userInput,
                Status = MessageStatus.Complete,
                TraceId = trace,
                ClientMessageId = request.ClientMessageId,
            }, ct);
        }

        // ---------- 有效交集工具收集 ----------
        var (roleMcpIds, rolePromptIds, roleSkillIds) = await _config.GetRoleBindingsAsync(request.UserId, ct);
        var settings = await _config.GetUserSettingsAsync(request.UserId, ct);

        var preferredProviderId = request.ProviderId ?? ParseGuid(GetSetting(settings, SettingProvider));
        var preferredModelId = request.ModelId ?? ParseGuid(GetSetting(settings, SettingModel));
        var requestedMcp = request.McpServerIds ?? ParseGuids(GetSetting(settings, SettingMcpServers));
        var requestedSkills = request.SkillIds ?? ParseGuids(GetSetting(settings, SettingSkills));

        var servers = (await _config.GetEnabledMcpServersAsync(ct))
            .Where(s => roleMcpIds.Contains(s.Id) && (requestedMcp.Count == 0 || requestedMcp.Contains(s.Id)))
            .ToList();
        var enabledSkills = (await _config.GetEnabledSkillsAsync(ct))
            .Where(s => roleSkillIds.Contains(s.Id) && (requestedSkills.Count == 0 || requestedSkills.Contains(s.Id)))
            .ToList();

        var unifiedTools = new List<UnifiedTool>();
        _logger.LogInformation("[Orchestrator] ready trace={Trace} servers={Servers} skills={Skills}", trace, servers.Count, enabledSkills.Count);
        foreach (var server in servers)
        {
            try
            {
                unifiedTools.AddRange(_mcp.GetEnabledTools(server));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集 MCP 工具失败 trace={Trace} server={Server}", trace, server.Name);
            }
        }
        foreach (var skill in enabledSkills)
        {
            unifiedTools.Add(new UnifiedTool("skill", skill.MetaToolName, skill.Description ?? skill.Name, null, IsSkill: true));
        }
        var skillByName = enabledSkills.ToDictionary(s => s.MetaToolName, StringComparer.OrdinalIgnoreCase);

        // ---------- MCP 视觉：多张逐个识别为文本（工具参数名 image_source = 标准 base64） ----------
        var visionLines = new List<string>();
        if (request.Images is { Count: > 0 })
        {
            foreach (var server in servers.Where(s => s.IsVision))
            {
                // 视觉工具选择：精确名优先（describe_image / vision / recognize_image / read_image_text），
                // 再按命名约定兜底，但必须排除批量/元数据/切换类工具
                // （batch_describe_images 参数为 wrapperArguments 批量结构、compare_images 多图、get_image_info 只读元数据、switch_vision_backend 切换后端），
                // 否则会向单张 image_source 校验失败的工具传参。
                var allTools = _mcp.GetEnabledTools(server).ToList();
                var visionTool =
                    allTools.FirstOrDefault(t => t.Name is "vision" or "recognize_image") ??
                    allTools.FirstOrDefault(t => t.Name.Equals("describe_image", StringComparison.OrdinalIgnoreCase)) ??
                    allTools.FirstOrDefault(t => t.Name.Equals("read_image_text", StringComparison.OrdinalIgnoreCase)) ??
                    allTools.FirstOrDefault(t =>
                        t.Name.Contains("image", StringComparison.OrdinalIgnoreCase)
                        && !t.Name.StartsWith("batch_", StringComparison.OrdinalIgnoreCase)
                        && !t.Name.StartsWith("compare_", StringComparison.OrdinalIgnoreCase)
                        && !t.Name.StartsWith("switch_", StringComparison.OrdinalIgnoreCase)
                        && !t.Name.StartsWith("get_", StringComparison.OrdinalIgnoreCase)
                        && !t.Name.StartsWith("list_", StringComparison.OrdinalIgnoreCase));
                if (visionTool is null) continue;
                for (var i = 0; i < request.Images.Count; i++)
                {
                    var img = request.Images[i];
                    var args = JsonSerializer.Serialize(new { image_source = img.Base64 });
                    try
                    {
                        var r = await _mcp.CallToolAsync(server, visionTool.Name, args, trace + $"_img{i}", lang, ct);
                        if (!r.Success)
                        {
                            _logger.LogWarning("MCP 视觉识别失败 trace={Trace} server={Server} tool={Tool} error={Error}",
                                trace, server.Name, visionTool.Name, r.ErrorMessage);
                        }
                        visionLines.Add(r.Success
                            ? $"[image {i + 1}] {truncate(r.ResultText, 500)}"
                            : $"[image {i + 1}] {Texts.Get("IMAGE_RECOGNITION_FAILED", lang)}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "MCP 视觉识别失败 trace={Trace} server={Server}", trace, server.Name);
                        visionLines.Add($"[image {i + 1}] {Texts.Get("IMAGE_RECOGNITION_FAILED", lang)}");
                    }
                }
            }
        }
        if (visionLines.Count > 0)
        {
            // 视觉识别结果为文本上下文，前端以 context 事件展示（多张逐个识别）
            yield return AgentEvent.ContextEvent("vision", string.Join("\n", visionLines), trace);
        }
        else if (request.Images is { Count: > 0 })
        {
            // 有图片但未产生任何识别结果：当前绑定集合中没有可用视觉工具（或全部 fallback），明确提示
            yield return AgentEvent.ContextEvent("vision", Texts.Get("IMAGE_NO_VISION_TOOL", lang), trace);
        }

        // ---------- 构建 Prompt（模板渲染） ----------
        var selectedPrompts = new List<Prompt>();
        if (request.PromptId is { } pid && rolePromptIds.Contains(pid))
        {
            var p = await _config.GetPromptAsync(pid, ct);
            if (p is { Enabled: true }) selectedPrompts.Add(p);
        }
        if (selectedPrompts.Count == 0)
        {
            var defaults = (await _config.GetEnabledPromptsAsync(ct)).Where(p => rolePromptIds.Contains(p.Id)).Take(2).ToList();
            selectedPrompts.AddRange(defaults);
        }

        var toolDescs = string.Join("\n", unifiedTools.Select(t => $"- {t.Name}: {truncate(t.Description, 120)}"));
        var skillDescs = string.Join("\n", enabledSkills.Select(s => $"- {s.MetaToolName}: {truncate(s.Description ?? s.Name, 120)}{Texts.Get("SKILL_LAZY_HINT", lang)}"));
        var templateVars = new Dictionary<string, object?>
        {
            ["user"] = new { name = session.User?.DisplayName ?? Texts.Get("USER_DISPLAY_FALLBACK", lang), id = request.UserId },
            ["session_id"] = session.Id,
            ["time"] = DateTimeOffset.Now,
            ["tools"] = string.IsNullOrEmpty(toolDescs) ? Texts.Get("TOOLS_NONE", lang) : toolDescs,
            ["skills"] = string.IsNullOrEmpty(skillDescs) ? Texts.Get("SKILLS_NONE", lang) : skillDescs,
            ["trace_id"] = trace,
        };

        var systemBlocks = new List<string>();
        foreach (var p in selectedPrompts)
        {
            try
            {
                var rendered = _templates.Render(p.Content, templateVars);
                if (!string.IsNullOrWhiteSpace(rendered)) systemBlocks.Add(rendered);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prompt 渲染失败 trace={Trace} prompt={Prompt}", trace, p.Name);
            }
        }
        if (systemBlocks.Count == 0)
        {
            systemBlocks.Add(Texts.Get("DEFAULT_SYSTEM", lang));
        }
        var systemPrompt = string.Join("\n\n---\n\n", systemBlocks);

        // ---------- 会话历史（按用户隔离） ----------
        var history = await _chat.ListMessagesAsync(request.UserId, session.Id, ct);
        var llmHistory = history
            .Where(m => m.Id != userMessage.Id && m.Role is ChatRole.User or ChatRole.Assistant && !string.IsNullOrWhiteSpace(m.Content))
            .TakeLast(40)
            .Select(m => m.Role == ChatRole.User
                ? LlmChatMessage.User(m.Content!)
                : LlmChatMessage.Assistant(m.Content!, reasoning: m.Reasoning)) // 思考链随历史回传（携带 tools 时官方要求，否则 400）
            .ToList();

        var initialMessages = new List<LlmChatMessage> { LlmChatMessage.System(systemPrompt) };
        initialMessages.AddRange(llmHistory);
        if (visionLines.Count > 0)
        {
            initialMessages.Add(LlmChatMessage.Assistant(string.Join("\n", visionLines)));
        }
        initialMessages.Add(LlmChatMessage.User(userInput));

        // ---------- 推理循环（ReAct） ----------
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var registryToken = _cancellations.Register(request.UserId, session.Id);
        using var registryLink = CancellationTokenSource.CreateLinkedTokenSource(registryToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, registryToken);

        var finalText = new System.Text.StringBuilder();
        var finalReasoning = new System.Text.StringBuilder();
        var toolTrace = new List<JsonObject>();
        var assistantStatus = MessageStatus.Complete;
        string? model = null;
        LlmUsage? totalUsage = null;
        var failureCode = (string?)null;
        var failureMessage = (string?)null;

        var request2 = new AgentRunRequest
        {
            TraceId = trace,
            UserId = request.UserId,
            SessionId = session.Id,
            InitialMessages = initialMessages,
            Tools = unifiedTools,
            PreferredProviderId = preferredProviderId,
            PreferredModelId = request.ModelId,
            ContextWindow = await GetContextWindowAsync(preferredProviderId, request.ModelId, ct),
            Lang = lang,
            // 思考模式：前端全局开关（默认启用）+ 强度（默认 high）；映射在客户端统一执行
            ThinkingEnabled = request.ThinkingEnabled ?? true,
            ThinkingEffort = ParseEffort(request.ThinkingEffort) ?? NextChats.Core.Domain.LlmThinkingEffort.High,
            ToolExecutor = (tool, args, t, tct) => ExecuteToolAsync(tool, args, t, tct, servers, skillByName, lang),
        };

        try
        {
            await foreach (var ev in _loop.RunAsync(request2, linked.Token))
            {
                switch (ev.Kind)
                {
                    case "text_delta":
                        finalText.Append(ev.Text);
                        break;
                    case "thinking_delta":
                        finalReasoning.Append(ev.Text);
                        if (finalReasoning.Length > 40_000) finalReasoning.Length = 40_000;
                        break;
                    case "tool_start":
                        toolTrace.Add(new JsonObject
                        {
                            ["server"] = ev.ServerName,
                            ["tool"] = ev.ToolName,
                            ["args"] = ev.ArgumentsJson is null ? null : JsonNode.Parse(ev.ArgumentsJson),
                            ["approvalId"] = ev.ApprovalId?.ToString(),
                            ["approvalStatus"] = ev.ApprovalStatus,
                        });
                        break;
                    case "tool_result":
                    case "tool_error":
                    {
                        var last = toolTrace.LastOrDefault(t => t["tool"]?.GetValue<string>() == ev.ToolName);
                        if (last is not null)
                        {
                            last["success"] = ev.Success;
                            last["durationMs"] = ev.DurationMs;
                            last["preview"] = ev.ResultPreview is null ? null : truncate(ev.ResultPreview, 400);
                            last["errorCode"] = ev.ErrorCode;
                        }
                        break;
                    }
                    case "error":
                        failureCode ??= ev.Code;
                        failureMessage ??= ev.Message;
                        assistantStatus = ev.Code == "INTERRUPTED" ? MessageStatus.Stopped : MessageStatus.Failed;
                        break;
                    case "done":
                        totalUsage = ev.Usage is null ? null : new LlmUsage(ev.Usage.PromptTokens, ev.Usage.CompletionTokens);
                        model ??= ev.Model;
                        break;
                }
                yield return ev;
            }
        }
        finally
        {
            _cancellations.Unregister(request.UserId, session.Id, registryToken);
        }

        // ---------- 持久化助手消息 + 用量 + 幂等 ----------
        var assistantMessage = await _chat.AppendMessageAsync(new ChatMessage
        {
            SessionId = session.Id,
            UserId = request.UserId,
            Role = ChatRole.Assistant,
            Content = finalText.Length > 0 ? finalText.ToString() : null,
            Reasoning = finalReasoning.Length > 0 ? finalReasoning.ToString() : null,
            ToolCallsJson = toolTrace.Count > 0 ? JsonSerializer.Serialize(toolTrace) : null,
            Status = assistantStatus,
            Model = model,
            PromptTokens = totalUsage?.PromptTokens ?? 0,
            CompletionTokens = totalUsage?.CompletionTokens ?? 0,
            TotalTokens = (totalUsage?.PromptTokens ?? 0) + (totalUsage?.CompletionTokens ?? 0),
            TraceId = trace,
        }, ct);

        // 老数据默认标题兼容：空串/旧"新会话"都按未命名处理（前端展示 fallback 文案）
        session.Title = string.IsNullOrWhiteSpace(session.Title) || session.Title == "新会话"
            ? (userInput.Length > 20 ? userInput[..20] + "…" : userInput)
            : session.Title;
        session.LastMessageAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _chat.UpdateSessionAsync(session, ct);

        await _chat.RecordUsageAsync(new TokenUsageRecord
        {
            TraceId = trace,
            UserId = request.UserId,
            SessionId = session.Id,
            ProviderName = model ?? "unknown",
            Model = model,
            PromptTokens = totalUsage?.PromptTokens ?? 0,
            CompletionTokens = totalUsage?.CompletionTokens ?? 0,
            TotalTokens = totalUsage?.TotalTokens ?? 0,
            Cost = EstimateCost(totalUsage),
            TtftMs = 0,
            TotalMs = 0,
            ToolCalls = toolTrace.Count,
            ToolErrorCount = toolTrace.Count(t => t.TryGetPropertyValue("errorCode", out var e) && e is not null),
            ApprovalCount = toolTrace.Count(t => t.TryGetPropertyValue("approvalId", out var a) && a is not null),
            Rounds = 0,
        }, ct);

        await _chat.StoreIdempotencyAsync(request.UserId,
            IdempotencyKey(request.UserId, request.ClientMessageId ?? Guid.NewGuid().ToString("N")),
            JsonSerializer.Serialize(new { messageId = assistantMessage.Id, sessionId = session.Id }), ct);

        await _audit.RecordAsync(
            assistantStatus == MessageStatus.Failed ? AuditCategory.Security : AuditCategory.Chat,
            assistantStatus == MessageStatus.Failed ? "CHAT.FAILED" : "CHAT.COMPLETE",
            trace, request.UserId, session.Id.ToString(),
            new { status = assistantStatus.ToString(), model, promptTokens = totalUsage?.PromptTokens ?? 0, completionTokens = totalUsage?.CompletionTokens ?? 0, failureCode, failureMessage },
            ct: ct);
    }

    /// <summary>统一工具执行器：Skill 元工具 → SkillExecutionEngine；MCP 工具 → IMcpDriver</summary>
    private async Task<McpToolResult> ExecuteToolAsync(UnifiedTool tool, string? args, string traceId, CancellationToken ct,
        IReadOnlyList<McpServer> servers, Dictionary<string, Skill> skillByName, string lang)
    {
        if (tool.IsSkill)
        {
            if (!skillByName.TryGetValue(tool.Name, out var skill))
            {
                return new McpToolResult(false, "", Texts.Get("SKILL_NOT_FOUND", lang), "SKILL_NOT_FOUND", 0, 1);
            }
            var input = ExtractString(args ?? "{}", "input") ?? args ?? "";
            var (ok, result, error) = await _skills.ExecuteAsync(skill, input, traceId, ct);
            return new McpToolResult(ok, result, error, ok ? null : "SKILL_ERROR", 0, 1);
        }

        var server = servers.FirstOrDefault(s => s.Name == tool.ServerName);
        if (server is null)
        {
            return new McpToolResult(false, "", Texts.Get("MCP_SERVER_NOT_FOUND", lang), "SERVER_NOT_FOUND", 0, 1);
        }
        return await _mcp.CallToolAsync(server, tool.Name, args, traceId, lang, ct);
    }

    private async Task<int> GetContextWindowAsync(Guid? preferredProviderId, Guid? preferredModelId, CancellationToken ct)
    {
        var server = (await _config.GetActiveProvidersAsync(ct))
            .Where(p => p.Enabled && p.IsHealthy)
            .OrderBy(p => p.Priority)
            .FirstOrDefault(p => p.Id == (preferredProviderId ?? Guid.Empty));
        if (server is null)
        {
            server = (await _config.GetActiveProvidersAsync(ct)).Where(p => p.Enabled && p.IsHealthy).OrderBy(p => p.Priority).FirstOrDefault();
        }
        var model = server?.Models.Where(m => m.Enabled).OrderBy(m => m.Priority)
            .FirstOrDefault(m => m.Id == (preferredModelId ?? Guid.Empty))
            ?? server?.Models.Where(m => m.Enabled).OrderBy(m => m.Priority).FirstOrDefault();
        return model?.ContextWindow ?? 128_000;
    }

    private static bool IsStandardBase64(string base64)
    {
        try
        {
            _ = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string IdempotencyKey(Guid userId, string clientMessageId) => $"{userId}:{clientMessageId}";

    private static string? ExtractString(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        catch (JsonException)
        {
            // 忽略
        }
        return null;
    }

    private static decimal EstimateCost(LlmUsage? usage)
    {
        if (usage is null) return 0;
        return usage.PromptTokens * PricePer1KInput / 1000 + usage.CompletionTokens * PricePer1KOutput / 1000;
    }

    private static string? GetSetting(IDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) ? value : null;

    private static Guid? ParseGuid(string? s) => Guid.TryParse(s, out var g) ? g : null;

    private static NextChats.Core.Domain.LlmThinkingEffort? ParseEffort(string? v) =>
        Enum.TryParse<NextChats.Core.Domain.LlmThinkingEffort>(v, true, out var e) ? e : null;

    private static List<Guid> ParseGuids(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return [];
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(s);
            return arr?.Select(Guid.Parse).ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? Texts.Get("NO_DESCRIPTION", "en") : (s.Length <= max ? s : s[..max] + "…");
}
