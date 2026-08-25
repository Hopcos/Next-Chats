using NextChats.Core.Clients;
using NextChats.Core.Entities;

namespace NextChats.Core.Abstractions;

/// <summary>上下文管理：估算 Token → 超阈值先压缩（LLM 摘要）后截断（丢最旧轮）</summary>
public interface IContextManager
{
    int EstimateTokens(string text);

    int EstimateMessagesTokens(IEnumerable<LlmChatMessage> messages);

    /// <summary>是否需要压缩/截断</summary>
    bool NeedsCompression(IReadOnlyList<LlmChatMessage> messages, int contextWindow);

    /// <summary>压缩：对最旧的历史做 LLM 摘要（失败则退回截断策略）</summary>
    Task<IReadOnlyList<LlmChatMessage>> CompressAsync(IReadOnlyList<LlmChatMessage> messages, int contextWindow, IReadOnlyList<LlmToolDef>? tools, CancellationToken ct);

    /// <summary>截断：按配额丢弃最旧轮次，保证不超出长度限制</summary>
    IReadOnlyList<LlmChatMessage> Truncate(IReadOnlyList<LlmChatMessage> messages, int contextWindow);
}
