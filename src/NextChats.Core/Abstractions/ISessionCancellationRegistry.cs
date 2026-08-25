namespace NextChats.Core.Abstractions;

/// <summary>
/// 会话中断注册表（进程内内存实现；后续横向扩展时可替换为 Redis 发布订阅/分布式锁）。
/// LLM 推理过程中用户可中断：同一个 SessionId 的请求取消即触发中断。
/// </summary>
public interface ISessionCancellationRegistry
{
    /// <summary>注册会话的取消令牌（并发时只有一个生效；重复注册返回既有令牌并链接新令牌）</summary>
    CancellationToken Register(Guid userId, Guid sessionId);

    /// <summary>请求中断（中断按钮）</summary>
    bool Cancel(Guid userId, Guid sessionId);

    void Unregister(Guid userId, Guid sessionId, CancellationToken token);
}
