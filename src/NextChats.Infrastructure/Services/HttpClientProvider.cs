using NextChats.Core.Abstractions;

namespace NextChats.Infrastructure.Services;

/// <summary>基于 IHttpClientFactory 的命名客户端提供者（llm / mcp）</summary>
public sealed class HttpClientProvider(IHttpClientFactory factory) : IHttpClientProvider
{
    public HttpClient Create(string name) => factory.CreateClient(name);
}
