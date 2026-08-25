using System.Text;
using System.Text.Json;

namespace NextChats.Core.Services;

/// <summary>极简 SSE 解析器：用于解析 OpenAI 兼容 /chat/completions 流式响应</summary>
public static class SseParser
{
    /// <summary>逐事件读取 "data: ..." 负载；data: [DONE] 表示结束</summary>
    public static async IAsyncEnumerable<string> ReadDataAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataLines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.Length == 0)
            {
                if (dataLines.Count > 0)
                {
                    var data = string.Join('\n', dataLines);
                    dataLines.Clear();
                    yield return data;
                }
                continue;
            }
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line.Length > 5 ? line[5..].TrimStart() : "");
            }
            // 忽略注释/事件名/其它字段
        }
        if (dataLines.Count > 0)
        {
            yield return string.Join('\n', dataLines);
        }
    }

    public static async Task<string?> ReadFirstDataAsync(Stream stream, CancellationToken ct)
    {
        await foreach (var data in ReadDataAsync(stream, ct))
        {
            if (data != "[DONE]") return data;
        }
        return null;
    }

    public static JsonDocument? Parse(string data)
    {
        if (data == "[DONE]") return null;
        try { return JsonDocument.Parse(data); } catch (JsonException) { return null; }
    }
}
