using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NextChats.Core.Abstractions;
using NextChats.Core.Configuration;
using NextChats.Core.Domain;

namespace NextChats.Core.Services;

/// <summary>
/// 策略引擎：危险操作拦截。
/// 规则链：白名单 Allow → 黑名单 Deny（直接拦截）→ 危险名单 RequireApproval（进审批流）。
/// 同时参考 MCP ToolAnnotations.DestructiveHint / ReadOnlyHint。
/// </summary>
public sealed partial class PolicyEngine : IPolicyEngine
{
    private readonly PolicyOptions _options;
    private readonly Regex[] _allow;
    private readonly Regex[] _deny;
    private readonly Regex[] _danger;

    public PolicyEngine(IOptions<PolicyOptions> options)
    {
        _options = options.Value;
        _allow = Compile(options.Value.AllowToolPatterns);
        _deny = Compile(options.Value.DenyToolPatterns);
        _danger = Compile(options.Value.DangerousToolPatterns);
    }

    public PolicyVerdict Evaluate(string serverName, string toolName, string? argumentsJson)
    {
        if (_allow.Any(r => r.IsMatch(toolName))) return PolicyVerdict.Allow;

        if (_deny.Any(r => r.IsMatch(toolName)))
        {
            return PolicyVerdict.Deny;
        }

        if (IsDangerousName(toolName) || _danger.Any(r => r.IsMatch(toolName)))
        {
            return PolicyVerdict.RequireApproval;
        }

        return PolicyVerdict.Allow;
    }

    private bool IsDangerousName(string toolName)
    {
        // 名称启发：含危险语义词
        if (toolName.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("drop", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("truncate", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("exec", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("send", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("transfer", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("approve", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("publish", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("deploy", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("rm_", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("drop_", StringComparison.OrdinalIgnoreCase))
        {
            return !toolName.StartsWith("mock.", StringComparison.OrdinalIgnoreCase); // mock 演示工具豁免
        }
        return false;
    }

    private static Regex[] Compile(IEnumerable<string> patterns) =>
        patterns.Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();
}
