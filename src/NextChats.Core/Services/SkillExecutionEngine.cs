using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NextChats.Core.Abstractions;
using NextChats.Core.Clients;
using NextChats.Core.Entities;
using NextChats.Core.Localization;

namespace NextChats.Core.Services;

/// <summary>
/// SKILL 执行引擎：
///  - 懒加载：元工具只下名称/描述（占 Token 极小）；完整指令在模型调用时才注入，防止 Token 爆炸。
///  - 每个 Skill 暴露为元工具（meta tool），由模型决定是否调用。
///  - 执行：取指令模板 → {{input}} 注入 → 嵌套 LLM 调用（可用 ModelOverride 指定模型）。
/// </summary>
public sealed class SkillExecutionEngine : ISkillExecutionEngine
{
    private readonly ILlmRouter _router;
    private readonly IPromptTemplateEngine _templates;
    private readonly ILogger _logger;

    public SkillExecutionEngine(ILlmRouter router, IPromptTemplateEngine templates, ILogger<SkillExecutionEngine> logger)
    {
        _router = router;
        _templates = templates;
        _logger = logger;
    }

    public IReadOnlyList<LlmToolDef> BuildMetaTools(IEnumerable<Skill> skills)
    {
        var defs = new List<LlmToolDef>();
        foreach (var skill in skills)
        {
            var schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["input"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = Texts.Get("SKILL_INPUT_DESC", "en"),
                    },
                    ["target"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = Texts.Get("SKILL_TARGET_DESC", "en"),
                    },
                },
                ["required"] = new JsonArray("input"),
            };
            defs.Add(new LlmToolDef(skill.MetaToolName, skill.Description ?? Texts.Get("SKILL_DESC_FALLBACK", "en", skill.Name), schema));
        }
        return defs;
    }

    /// <summary>懒加载执行：此刻才读取/注入完整指令并调用嵌套 LLM</summary>
    public async Task<(bool Success, string Result, string? Error)> ExecuteAsync(Skill skill, string input, string traceId, CancellationToken ct)
    {
        try
        {
            // 懒加载：指令模板在调用时才渲染（防止 Token 爆炸）
            var instruction = _templates.Render(skill.Instruction, new Dictionary<string, object?>
            {
                ["input"] = input,
                ["skill"] = skill.Name,
                ["trace_id"] = traceId,
            });

            var client = await _router.SelectClientAsync(null, null, null, ct);
            var request = new LlmRequest
            {
                Messages =
                [
                    LlmChatMessage.System(instruction),
                    LlmChatMessage.User(input),
                ],
                Stream = false,
                Model = skill.ModelOverride,
                MaxTokens = 2048,
            };
            var result = await client.CompleteAsync(request, ct);
            return (true, result.Message.Content ?? Texts.Get("SKILL_NO_OUTPUT", "en"), null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skill {Skill} 执行失败 trace={TraceId}", skill.Name, traceId);
            var msg = ex is LlmUnavailableException || ex is LlmHttpException ? Texts.Get("SKILL_LLM_UNAVAILABLE", "en") : ex.Message;
            return (false, "", msg);
        }
    }
}
