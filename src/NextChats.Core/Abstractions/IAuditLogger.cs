using NextChats.Core.Domain;

namespace NextChats.Core.Abstractions;

/// <summary>审计日志（完整上下文落库；对外只暴露友好文案）</summary>
public interface IAuditLogger
{
    Task RecordAsync(
        AuditCategory category,
        string action,
        string traceId,
        Guid? userId = null,
        string? target = null,
        object? detail = null,
        string? ip = null,
        string? userAgent = null,
        bool isSuspicious = false,
        CancellationToken ct = default);
}
