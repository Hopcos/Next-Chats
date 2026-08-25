using System.Text.Json;
using System.Text.Json.Nodes;
using NextChats.Core.Abstractions;
using NextChats.Core.Localization;

namespace NextChats.Core.Services;

/// <summary>对外展示脱敏小工具（不暴露 Endpoint、Header、Key）</summary>
public static class LogSanitizer
{
    public static string MaskUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return Texts.Get("MCP_NOT_CONFIGURED", "en");
        try
        {
            var u = new Uri(uri);
            return $"{u.Scheme}://{u.Host}{(u.Port > 0 && u.Port != 80 && u.Port != 443 ? ":" + u.Port : "")}{u.AbsolutePath}";
        }
        catch (UriFormatException)
        {
            return Texts.Get("MCP_INVALID_URI", "en");
        }
    }

    public static string? MaskHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return null;
        try
        {
            var node = JsonNode.Parse(headersJson) as System.Text.Json.Nodes.JsonObject;
            if (node is null) return headersJson;
            foreach (var key in node.Select(kv => kv.Key).ToList())
            {
                var value = node[key]?.ToString() ?? "";
                node[key] = value.Length <= 8 ? "****" : value[..3] + "****" + value[^3..];
            }
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return headersJson;
        }
    }
}
