using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextChats.Core.Abstractions;
using NextChats.Core.Clients;

namespace NextChats.Api.Controllers;

/// <summary>
/// 工具专用 LLM 端点（沉浸式工具调用）。
/// 与聊天链路完全隔离：**无任何持久化**（不落会话/消息/usage/审计），
/// 单次补全、无状态、固定非思考模式；模型可见性沿用聊天的角色绑定白名单。
/// </summary>
[ApiController]
[Authorize]
[Route("api/tools")]
public sealed class ToolLlmController(IConfigStore config, ILlmRouter router) : ApiControllerBase
{
    private const int MaxPromptChars = 6000;
    private const int MaxSystemChars = 4000;

    public sealed record CompleteRequest(Guid ModelId, string? SystemPrompt, string Prompt, int? MaxTokens);

    /// <summary>无状态单轮补全：system + prompt → text（不建会话、不落库；工具侧内容仅存在于浏览器 localStorage）</summary>
    [HttpPost("llm/complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Prompt)) return BadRequest(Err("INPUT_EMPTY"));
        if (req.Prompt.Length > MaxPromptChars) return BadRequest(Err("INPUT_TOO_LARGE", MaxPromptChars));
        if (req.SystemPrompt is { Length: > MaxSystemChars }) return BadRequest(Err("INPUT_TOO_LARGE", MaxSystemChars));

        // 与聊天一致的模型白名单语义：admin 或系统未做角色绑定时全量可用，否则仅限绑定模型
        var (_, _, _, modelIds) = await config.GetRoleBindingsAsync(UserId, ct);
        var isAdmin = await config.IsAdminAsync(UserId, ct);
        var restricted = !isAdmin && modelIds.Length > 0;

        var providers = await config.GetActiveProvidersAsync(ct);
        var model = providers.SelectMany(p => p.Models).FirstOrDefault(m => m.Id == req.ModelId && m.Enabled);
        if (model is null) return NotFound(Err("MODEL_NOT_AVAILABLE"));
        if (restricted && !modelIds.Contains(model.Id)) return StatusCode(StatusCodes.Status403Forbidden, Err("MODEL_NOT_AUTHORIZED"));

        var client = await router.SelectClientAsync(null, req.ModelId, Lang, ct, restricted ? modelIds.ToArray() : null);
        var messages = new List<LlmChatMessage>();
        if (!string.IsNullOrWhiteSpace(req.SystemPrompt)) messages.Add(LlmChatMessage.System(req.SystemPrompt));
        messages.Add(LlmChatMessage.User(req.Prompt));

        var request = new LlmRequest
        {
            Messages = messages,
            Stream = false,
            MaxTokens = Math.Clamp(req.MaxTokens ?? 2048, 1, 4096),
            // ThinkingEnabled 默认 false：工具调用固定非思考模式，不占用思考 token
        };
        var result = await client.CompleteAsync(request, ct);
        return Ok(new { text = result.Message.Content ?? "" });
    }
}
