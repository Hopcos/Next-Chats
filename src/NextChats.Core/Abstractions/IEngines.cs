using NextChats.Core.Clients;
using NextChats.Core.Domain;

namespace NextChats.Core.Abstractions;

/// <summary>Prompt 模板引擎（{{变量}} / #if / #each 等轻量模板）</summary>
public interface IPromptTemplateEngine
{
    /// <summary>渲染模板</summary>
    string Render(string template, IReadOnlyDictionary<string, object?> variables);
}

/// <summary>策略引擎：危险操作拦截 → 审批流</summary>
public interface IPolicyEngine
{
    /// <summary>评估工具调用（返回 Allow / Deny / RequireApproval）</summary>
    PolicyVerdict Evaluate(string serverName, string toolName, string? argumentsJson);
}
