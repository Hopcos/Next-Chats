using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextChats.Core.Abstractions;
using NextChats.Core.Domain;
using NextChats.Core.Entities;
using NextChats.Infrastructure.Data;

namespace NextChats.Infrastructure.Services;

/// <summary>
/// 审计日志：完整上下文落库（Detail 脱敏后存储），支持 trace_id 贯穿。
/// </summary>
public sealed class AuditLogger(IDbContextFactory<NextChatsDbContext> dbFactory, ISecurityService security, ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task RecordAsync(
        AuditCategory category,
        string action,
        string traceId,
        Guid? userId = null,
        string? target = null,
        object? detail = null,
        string? ip = null,
        string? userAgent = null,
        bool isSuspicious = false,
        CancellationToken ct = default)
    {
        try
        {
            var detailJson = detail is null
                ? null
                : security.MaskSecrets(JsonSerializer.Serialize(detail, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.AuditLogs.Add(new AuditLog
            {
                TraceId = traceId,
                UserId = userId,
                Category = category,
                Action = action,
                Target = target,
                DetailJson = detailJson,
                Ip = ip,
                UserAgent = userAgent,
                IsSuspicious = isSuspicious,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "审计日志写入失败 trace={Trace}", traceId);
        }
    }
}
