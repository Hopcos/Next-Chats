using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;
using NextChats.Core.Localization;

namespace NextChats.Core.Services;

/// <summary>
/// 审批协调器（进程内内存实现）：pending 审批 → Agent 循环等待决策（超时自动 Expired）。
/// 分布式场景可替换为：审批行写入 DB + Redis pub/sub 通知。
/// </summary>
public sealed class ApprovalCoordinator : IApprovalCoordinator
{
    private readonly IChatStore _store;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ApprovalDecision>> _waiters = new();

    public ApprovalCoordinator(IChatStore store, ILogger<ApprovalCoordinator> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ToolApproval> CreateAsync(Guid userId, Guid sessionId, string traceId,
        string serverName, string toolName, object? arguments, TimeSpan timeout, CancellationToken ct)
    {
        var approval = new ToolApproval
        {
            UserId = userId,
            SessionId = sessionId,
            TraceId = traceId,
            McpServerName = serverName,
            ToolName = toolName,
            ArgumentsJson = arguments is null ? null : System.Text.Json.JsonSerializer.Serialize(arguments),
            Status = ApprovalStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow + timeout,
        };
        await _store.CreateApprovalAsync(approval, ct);
        _waiters[approval.Id] = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        _logger.LogInformation("审批已创建 {ApprovalId} trace={TraceId} tool={Server}.{Tool}", approval.Id, traceId, serverName, toolName);
        return approval;
    }

    public async Task<ApprovalDecision?> WaitForDecisionAsync(Guid approvalId, TimeSpan timeout, CancellationToken ct)
    {
        if (!_waiters.TryGetValue(approvalId, out var tcs))
        {
            return null;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var delayTask = Task.Delay(timeout, linked.Token);
        var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

        if (completed == tcs.Task)
        {
            var decision = await tcs.Task.ConfigureAwait(false);
            return decision;
        }

        // 超时 → 标记 Expired
        _waiters.TryRemove(approvalId, out _);
        var approval = await _store.GetApprovalAsync(approvalId, ct).ConfigureAwait(false);
        if (approval is { Status: ApprovalStatus.Pending })
        {
            approval.Status = ApprovalStatus.Expired;
            approval.DecidedAt = DateTimeOffset.UtcNow;
            approval.Reason = Texts.Get("APPROVAL_EXPIRED_REASON", "en");
            await _store.UpdateApprovalAsync(approval, CancellationToken.None).ConfigureAwait(false);
        }
        return null;
    }

    public async Task<bool> NotifyDecisionAsync(Guid approvalId, ApprovalDecision decision, string decidedBy, string? reason, CancellationToken ct)
    {
        var approval = await _store.GetApprovalAsync(approvalId, ct);
        if (approval is null || approval.Status != ApprovalStatus.Pending)
        {
            return false;
        }

        approval.Status = decision == ApprovalDecision.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.DecidedBy = decidedBy;
        approval.Reason = reason;
        approval.DecidedAt = DateTimeOffset.UtcNow;
        await _store.UpdateApprovalAsync(approval, ct);

        if (_waiters.TryRemove(approvalId, out var tcs))
        {
            tcs.TrySetResult(decision);
        }
        return true;
    }
}
