using NextChats.Core.Abstractions;
using NextChats.Core.Clients;
using NextChats.Core.Entities;

namespace NextChats.Core.Abstractions;

/// <summary>
/// SKILL 执行引擎：懒加载（元工具只暴露名称/描述，指令在调用时注入，防止 Token 爆炸）；
/// 每个 Skill 暴露成元工具，由模型决定调用。
/// </summary>
public interface ISkillExecutionEngine
{
    /// <summary>把启用的 Skill 转成元工具定义（追加到工具列表，随每轮请求下发）</summary>
    IReadOnlyList<LlmToolDef> BuildMetaTools(IEnumerable<Skill> skills);

    /// <summary>执行 Skill：懒加载完整指令 → 装配 Prompt → 嵌套 LLM 调用（可指定模型）→ 返回结果文本</summary>
    Task<(bool Success, string Result, string? Error)> ExecuteAsync(Skill skill, string input, string traceId, CancellationToken ct);
}
