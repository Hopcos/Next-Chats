using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NextChats.Core.Abstractions;

namespace NextChats.Infrastructure.Services;

/// <summary>
/// 内存缓存实现（后续可扩展 Redis 实现同一接口，横向扩展时仅替换依赖注入）。
/// </summary>
public sealed class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        return Task.FromResult(cache.TryGetValue(key, out var value) && value is T typed ? typed : default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        cache.Set(key, value, ttl ?? DefaultTtl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (cache is MemoryCache mc)
        {
            // 无公开枚举 API：通过删除已知 key 缓存组的方式由调用方传入精确 key 集合；
            // 这里用编译期已知前缀配合 GetKeys 反射不可靠，因此基于 MemoryCache 的 Clear 组策略：
            // 简单实现：遍历内部 entries 成本高，改为在 MemoryCache 之上维护前缀索引不划算——
            // 这里采用 Clear() 全量失效（配置变更低频，代价可接受）。
            // 如需更细粒度，可换 Redis（SCAN + DEL）。
            mc.Compact(1.0);
        }
        return Task.CompletedTask;
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (cache.TryGetValue(key, out var existing) && existing is T typed)
        {
            return typed;
        }

        var value = await factory(ct);
        cache.Set(key, value, ttl ?? DefaultTtl);
        return value;
    }
}
