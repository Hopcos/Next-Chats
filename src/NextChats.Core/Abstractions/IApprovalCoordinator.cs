using NextChats.Core.Domain;
using NextChats.Core.Entities;

namespace NextChats.Core.Abstractions;

/// <summary>
/// 工具审批协调器：危险操作 → 生成 pending 审批 → Agent 循环等待决策（超时自动 expired）。
/// 进程内 Memory 实现；分布式场景可换 Redis 信号量。
/// </summary>
public interface IApprovalCoordinator
{
    Task<ToolApproval> CreateAsync(Guid userId, Guid sessionId, string traceId,
        string serverName, string toolName, object? arguments, TimeSpan timeout, CancellationToken ct);

    /// <summary>等待审批决策（超时返回 null → 标记 Expired）</summary>
    Task<ApprovalDecision?> WaitForDecisionAsync(Guid approvalId, TimeSpan timeout, CancellationToken ct);

    /// <summary>用户/管理员决策 → 唤醒等待中的 Agent 循环</summary>
    Task<bool> NotifyDecisionAsync(Guid approvalId, ApprovalDecision decision, string decidedBy, string? reason, CancellationToken ct);
}
