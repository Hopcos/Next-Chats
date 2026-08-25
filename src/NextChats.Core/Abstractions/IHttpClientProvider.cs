namespace NextChats.Core.Abstractions;

/// <summary>
/// HttpClient 提供者：由 Infrastructure 用 IHttpClientFactory 实现（命名客户端，含超时/重试策略），
/// 避免 Core 直接依赖 IHttpClientFactory 包。
/// </summary>
public interface IHttpClientProvider
{
    HttpClient Create(string name);
}
