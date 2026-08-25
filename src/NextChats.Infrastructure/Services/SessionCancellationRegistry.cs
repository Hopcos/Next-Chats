using System.Collections.Concurrent;
using NextChats.Core.Abstractions;

namespace NextChats.Infrastructure.Services;

/// <summary>
/// 会话中断注册表（进程内实现）：LLM 推理过程中，用户点击“中断”即取消对应会话的令牌。
/// </summary>
public sealed class SessionCancellationRegistry : ISessionCancellationRegistry
{
    private readonly ConcurrentDictionary<(Guid UserId, Guid SessionId), CancellationTokenSource> _map = new();

    public CancellationToken Register(Guid userId, Guid sessionId)
    {
        var cts = new CancellationTokenSource();
        var old = _map.AddOrUpdate((userId, sessionId), cts, (_, existing) =>
        {
            // 并发重复注册：保留旧令牌（已链接），新令牌由旧令牌链接
            return existing;
        });
        return old.Token;
    }

    public bool Cancel(Guid userId, Guid sessionId)
    {
        if (_map.TryGetValue((userId, sessionId), out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放
            }
            return true;
        }
        return false;
    }

    public void Unregister(Guid userId, Guid sessionId, CancellationToken token)
    {
        if (_map.TryRemove((userId, sessionId), out var cts))
        {
            cts.Dispose();
        }
    }
}
