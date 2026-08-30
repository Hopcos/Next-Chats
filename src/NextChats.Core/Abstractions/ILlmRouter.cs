using NextChats.Core.Clients;
using NextChats.Core.Entities;

namespace NextChats.Core.Abstractions;

/// <summary>
/// LLM Router 规则引擎：按 启用开关 → 健康度 → 优先级 → 轮询 选择供应商；
/// 调用失败时自动故障转移到下一个可用供应商（标记健康度）。
/// </summary>
public interface ILlmRouter
{
    /// <summary>选择一个可用的 Provider（fallback 链）</summary>
    Task<LlmProvider> SelectAsync(Guid? preferredId = null, CancellationToken ct = default);

    /// <summary>获取客户端（失败自动 failover 到下一供应商）；lang 影响 Mock 等本地化输出；
    /// allowedModelIds 非空时为模型白名单（角色绑定），首选/默认模型均只从白名单内选择</summary>
    Task<ILlmClient> SelectClientAsync(Guid? preferredId = null, Guid? preferredModelId = null, string? lang = null, CancellationToken ct = default, Guid[]? allowedModelIds = null);

    /// <summary>获取指定 Provider 的客户端（不路由）；modelId 为空时用供应商内优先级最高的启用模型</summary>
    Task<ILlmClient> GetClientAsync(Guid providerId, Guid? modelId = null, CancellationToken ct = default);

    /// <summary>标记供应商失败（路由时跳过）</summary>
    Task MarkUnhealthyAsync(Guid providerId, string error, CancellationToken ct = default);

    /// <summary>手动健康检查（管理端）</summary>
    Task<(bool Ok, string? Error, int LatencyMs, string? ErrCode)> PingAsync(Guid providerId, CancellationToken ct = default);
}
