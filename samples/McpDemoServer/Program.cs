using System.ComponentModel;
using ModelContextProtocol.Server;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// MCP Server（Streamable HTTP Transport，遵循最新 MCP 规范）
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<DemoTools>();

var app = builder.Build();

app.MapGet("/", () => Results.Text("MCP Demo Server is running. MCP endpoint: /mcp"));
app.MapMcp("/mcp");

app.Run();

/// <summary>演示工具集：普通工具 + 危险工具（触发审批流）</summary>
public class DemoTools
{
    [McpServerTool]
    [Description("返回当前 UTC 时间")]
    public static string GetCurrentTime() => DateTimeOffset.UtcNow.ToString("O");

    [McpServerTool]
    [Description("返回问候语")]
    public static string SayHello([Description("人名")] string name) => $"Hello, {name}! 现在是 {DateTimeOffset.UtcNow:HH:mm:ss} UTC。";

    [McpServerTool]
    [Description("回显输入内容（演示工具结果回灌给模型）")]
    public static string Echo([Description("任意文本")] string input) => $"echo: {input}";

    [McpServerTool]
    [Description("计算两个数之和")]
    public static double Add([Description("加数")] double a, [Description("被加数")] double b) => a + b;

    [McpServerTool]
    [Description("【危险】删除全部记录（仅演示，必须确认）——会进入审批流")]
    public static string DeleteAll([Description("确认删除")] bool confirm)
        => confirm ? "已执行删除操作（演示数据已清空）" : "未确认，未执行任何操作";

    [McpServerTool]
    [Description("模拟一个失败的请求（演示 MCP 报错不中断会话）")]
    public static string MaybeFail([Description("是否必然失败")] bool forceFail)
    {
        if (forceFail) throw new InvalidOperationException("模拟的服务器内部错误");
        return "本次调用成功";
    }

    [McpServerTool]
    [Description("识别图片内容（视觉）：接收名称为 image_source 的标准 base64 图像数据，返回识别文本")]
    public static string RecognizeImage([Description("标准 base64 编码的图像数据（参数名约定 image_source）")] string image_source)
    {
        // 标准 base64 校验；非法数据返回错误文本（由调用方回灌给模型）
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(image_source);
        }
        catch (FormatException)
        {
            return $"[vision] image_source 不是有效的标准 base64，无法识别（收到 {image_source.Length} 字符）";
        }
        var mime = image_source.StartsWith("/9j/") ? "image/jpeg"
            : image_source.StartsWith("iVBORw0KG") ? "image/png"
            : image_source.StartsWith("R0lGOD") ? "image/gif"
            : image_source.StartsWith("UklGR") ? "image/webp"
            : "unknown";
        var preview = image_source.Length <= 32 ? image_source : image_source[..32] + "…";
        return $"[vision] 已接收图片（{mime}，base64 {image_source.Length} 字符 ≈ {bytes.Length} 字节，预览 {preview}）。"
            + "演示服务器无法真实识别内容，已按标准 image_source base64 完成图片接收与元信息解析。";
    }
}

/// <summary>演示提示词集</summary>
public class DemoPrompts
{
    [McpServerPrompt]
    [Description("把文本翻译成英文")]
    public static string Translate([Description("原文")] string text) => $"Translate the following to English:\n{text}";
}
