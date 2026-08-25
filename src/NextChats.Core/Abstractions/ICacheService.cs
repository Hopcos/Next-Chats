namespace NextChats.Core.Abstractions;

/// <summary>
/// 缓存抽象：当前 Memory 实现，后续可无缝替换为 Redis（StackExchange.Redis 实现同一接口）。
/// 用于高频读（路由表、MCP 元数据、RBAC 绑定等）。
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>删除一批前缀匹配的键（配置变更时失效整组缓存）</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>T 若不存在则通过 factory 加载并写入缓存（防击穿）</summary>
    Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default);
}
